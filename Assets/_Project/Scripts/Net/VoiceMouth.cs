// VoiceMouth.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/VoiceMouth.cs
// Goes on: the Player root, one per crewmate.
//
// ====================================================================
// PHASE 4 STEP 10 - MAKING A VOICE SOUND LIKE CONCRETE.
//
// PHASE4_SPEC is explicit that "realistic" is not a feeling here, it is four
// measurable things - and that ALL FOUR ARE UNITY AUDIO ON TOP OF A HUMAN
// VOICE, not a feature of any voice library. That is why this file exists
// before a single byte of Steam Voice does: none of it depends on where the
// samples come from.
//
// Steam Voice, Dissonance, a recording, a test tone - anything that can fill
// an AudioSource goes through here and comes out sounding like the building.
//
//   1 DISTANCE       spatialBlend 1, logarithmic, dead inside one room
//   2 OCCLUSION      a raycast to the listener; concrete drives a lowpass
//   3 REVERB         chosen by where the LISTENER stands
//   4 RADIO          band-pass and a little grit, ignoring distance entirely
//
// THE ONE THAT SELLS IT IS 2, AND IT IS ALMOST FREE HERE.
//
// Floors are five metres apart with a slab between them, so a raycast from a
// speaker one floor down ALWAYS hits something. That means two floors away is
// silent with no special case, and one floor away is a muffled thump you can
// very nearly identify - which is worse than hearing them clearly and worse
// than not hearing them at all. The spec calls that "almost" the horror, and
// it comes out of the geometry rather than out of a tuning pass.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VoiceMouth : MonoBehaviour
{
    [Header("1 - Distance")]
    [Tooltip("Roughly one room. A voice should die inside the space it was " +
             "spoken in, so that hearing somebody means they are with you.")]
    public float maxDistance = 14f;

    [Tooltip("Below this, no falloff at all - standing next to somebody is " +
             "just talking.")]
    public float minDistance = 1.6f;

    [Header("2 - Occlusion")]
    [Tooltip("What counts as concrete. Everything except players and loot, " +
             "which do not block sound in any way anybody would notice.")]
    public LayerMask occluders = ~0;

    [Tooltip("Cutoff with clear air between you. 22kHz is 'no filter'.")]
    public float openCutoff = 22000f;

    [Tooltip("Cutoff through one slab. 550Hz is the muffled thump - you can " +
             "tell it is a person and not what they said.")]
    public float wallCutoff = 550f;

    [Tooltip("How much quieter a wall makes them, on top of the muffling.")]
    [Range(0f, 1f)] public float wallVolume = 0.35f;

    [Tooltip("Seconds to slide between open and blocked. Instant switching " +
             "chatters when somebody walks past a doorway.")]
    public float glide = 0.12f;

    [Header("4 - Radio")]
    [Tooltip("Set by the walkie-talkie. Ignores distance and occlusion, and " +
             "sounds like a speaker instead of a person.")]
    public bool onRadio;

    public float radioLowCut = 400f;
    public float radioHighCut = 2800f;

    AudioSource source;
    AudioLowPassFilter lowPass;
    AudioHighPassFilter highPass;
    AudioDistortionFilter grit;

    float cutoff;
    float volumeScale = 1f;

    /// <summary>
    /// The AudioSource a voice provider should write into. Steam Voice fills
    /// this with decoded PCM; anything else can too.
    /// </summary>
    public AudioSource Source => source;

    void Awake()
    {
        source = GetComponent<AudioSource>();

        source.spatialBlend = 1f;                 // fully positional
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.playOnAwake = false;
        source.loop = true;
        source.dopplerLevel = 0f;                 // a voice is not a siren

        lowPass = GetComponent<AudioLowPassFilter>();
        if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();

        highPass = GetComponent<AudioHighPassFilter>();
        if (highPass == null) highPass = gameObject.AddComponent<AudioHighPassFilter>();

        grit = GetComponent<AudioDistortionFilter>();
        if (grit == null) grit = gameObject.AddComponent<AudioDistortionFilter>();

        cutoff = openCutoff;
    }

    void Update()
    {
        // My own voice is never played back to me. Hearing yourself a frame
        // late is the single most disorienting thing a voice system can do.
        if (PlayerRegistry.IsLocalFor(this))
        {
            source.mute = true;
            return;
        }

        source.mute = false;

        // ---- WHO IS ON THE RADIO IS ASKED, NOT SET ----
        //
        // A pushed flag would need clearing by whoever set it, and a client
        // that disconnected mid-transmission would leave a mouth stuck on the
        // radio forever. The channel already knows who holds it, so this asks.
        var radio = WalkieChannel.Instance;
        var owner = PlayerRegistry.OwnerOf(this);
        var netObj = owner != null
            ? owner.GetComponent<Unity.Netcode.NetworkObject>() : null;

        onRadio = radio != null && radio.IsSpawned && netObj != null &&
                  netObj.IsSpawned && radio.Holder.Value == netObj.OwnerClientId;

        if (onRadio) { ApplyRadio(); return; }

        ApplyProximity();
    }

    /// <summary>
    /// Distance and occlusion together.
    ///
    /// The raycast goes to the LISTENER'S EAR, not to their body's origin -
    /// the ear is at eye height and the body's origin is at the floor, and in
    /// a building made of floor slabs that difference decides whether the
    /// ray passes under a wall.
    /// </summary>
    void ApplyProximity()
    {
        highPass.enabled = false;
        grit.enabled = false;
        lowPass.enabled = true;

        var ear = PlayerRegistry.Local != null ? PlayerRegistry.Local.Eye : null;
        bool blocked = false;

        if (ear != null)
        {
            Vector3 mouth = transform.position + Vector3.up * 1.5f;
            Vector3 toEar = ear.position - mouth;

            // QueryTriggerInteraction.Ignore: trigger volumes are gameplay
            // regions, not walls, and treating them as concrete would mute
            // somebody standing in a doorway for no visible reason.
            blocked = Physics.Linecast(mouth, ear.position, out RaycastHit hit,
                                       occluders, QueryTriggerInteraction.Ignore)
                      && hit.collider.GetComponentInParent<PlayerMotor>() == null;
        }

        float wantCutoff = blocked ? wallCutoff : openCutoff;
        float wantVolume = blocked ? wallVolume : 1f;

        // Glided, not switched. Somebody walking past a doorway would
        // otherwise make the filter chatter, which sounds like a fault rather
        // than like a wall.
        float k = glide <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / glide);
        cutoff = Mathf.Lerp(cutoff, wantCutoff, k);
        volumeScale = Mathf.Lerp(volumeScale, wantVolume, k);

        lowPass.cutoffFrequency = cutoff;
        source.volume = volumeScale;
        source.spatialBlend = 1f;
    }

    /// <summary>
    /// The radio: band-passed, a little dirty, and NOT positional.
    ///
    /// spatialBlend 0 is the whole trick. A radio voice arrives in your head,
    /// not from a direction - which is exactly why it is worth buying, and
    /// exactly why it feels different from somebody shouting.
    /// </summary>
    void ApplyRadio()
    {
        lowPass.enabled = true;
        highPass.enabled = true;
        grit.enabled = true;

        lowPass.cutoffFrequency = radioHighCut;
        highPass.cutoffFrequency = radioLowCut;
        grit.distortionLevel = 0.12f;

        source.spatialBlend = 0f;
        source.volume = 1f;
    }
}
