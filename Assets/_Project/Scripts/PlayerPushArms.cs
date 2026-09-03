// PlayerPushArms.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerPushArms.cs
// Goes on: PlayerModel_FBX_VISUAL  (the SAME GameObject as the Animator).
//
// ========================================================================
// PHASE 5 - THE SHOVE YOU CAN SEE.
//
// "i need a push animation with hands"
//
// Built the same way the legs were, and for the same reason: there is no push
// clip in Assets/_Project/Models, and a downloaded one would still be wrong.
// A Mixamo shove is authored for one distance, at one height, with the arms
// arriving wherever the animator decided - while a real shove has to reach the
// person actually in front of you, who might be a step away or at arm's
// length, on a ramp, or shorter than you.
//
// Two hands, forward, at whatever the target actually is. That is IK's job.
//
// THE SWING HAS THREE PARTS AND THEY ARE NOT THE SAME LENGTH
//
//   wind up   the hands draw back and in. Short - a shove is not a haymaker
//   thrust    fast, and this is where the impulse lands
//   recover   slowest. Arms fall back rather than snapping, which is what
//             stops it reading as a twitch
//
// The contact moment is PlayerPush.contactAt, so the hit and the hands cannot
// drift apart: one number moves both.
//
// WHY IT DOES NOT FIGHT FirstPersonHands
//
// That component owns the hands during normal play and holds them locked to
// the camera - but it returns early for anybody who is not the local player,
// so on a TEAMMATE'S body nothing is holding the arms at all.
//
// On your own body both want the hands, so this runs at execution order 40,
// after its 30. Unity calls OnAnimatorIK in execution order and the last
// writer wins, so during a shove this quietly overrules it and hands them back
// the moment the swing ends. Nothing had to be modified in that file.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(40)]          // after FirstPersonHands (30), so it wins
[RequireComponent(typeof(Animator))]
public class PlayerPushArms : MonoBehaviour
{
    [Header("Reach")]
    [Tooltip("How far in front of the chest the hands end up at full thrust, " +
             "in metres. Should be close to PlayerPush.range so the hands " +
             "arrive where the shove actually lands.")]
    public float reach = 0.72f;

    [Tooltip("How far apart the two hands are, in metres. Shoulder width - " +
             "both palms on a chest.")]
    public float spread = 0.24f;

    [Tooltip("Height of the shove above the body's origin, in metres. Chest " +
             "height on the person being pushed, not on you.")]
    public float height = 1.28f;

    [Header("The wind up")]
    [Tooltip("How far the hands draw BACK before the thrust, in metres. Small " +
             "- a shove is not a haymaker, and a big wind-up telegraphs it so " +
             "clearly that nobody would ever be caught by one.")]
    public float windBack = 0.16f;

    [Tooltip("How much of the swing is wind-up, 0 to 1. The rest is thrust " +
             "and recovery.")]
    [Range(0.05f, 0.5f)] public float windPart = 0.3f;

    [Header("Blend")]
    [Tooltip("How strongly the hands are pulled to the shove. 1 takes them " +
             "over completely for the length of the swing.")]
    [Range(0f, 1f)] public float weight = 1f;

    [Tooltip("Seconds to fade in and out at the ends of the swing, so the arms " +
             "are never snatched from or handed back to the clip abruptly.")]
    public float fade = 0.09f;

    [Tooltip("How much of the swing after the wind-up is the thrust itself, " +
             "0 to 1. The rest is the recovery. Larger means a longer, more " +
             "deliberate push; smaller means a snap.")]
    [Range(0.15f, 0.8f)] public float thrustPart = 0.52f;

    [Header("Aim")]
    [Tooltip("Follow where the player is LOOKING, up and down, instead of " +
             "shoving flat out of the chest. The body only carries yaw - it is " +
             "welded to the camera horizontally and knows nothing about pitch - " +
             "so without this the hands ignore the camera entirely whenever you " +
             "look up or down.")]
    public bool followCamera = true;

    [Tooltip("Degrees of look pitch the hands will follow, up or down. Clamped " +
             "because the IMPULSE stays roughly level on purpose - a shove is " +
             "for moving people sideways, not stapling them to the floor - and " +
             "hands that dived at your boots while the push went flat would " +
             "just look wrong.")]
    [Range(0f, 80f)] public float pitchLimit = 38f;

    [Header("Palm")]
    [Tooltip("Rotation of the RIGHT hand relative to the direction of the " +
             "shove. The left hand mirrors it on Z. " +
             "This exists because a hand bone has no standard orientation - " +
             "which axis runs along the fingers is a decision the rig made, " +
             "not something that can be derived. FirstPersonHands solves the " +
             "same problem the same way with its own Euler(0,0,+-75), and this " +
             "is the number to change if the palms arrive edge-on like a " +
             "karate chop instead of flat like a shove.")]
    public Vector3 palmEuler = new Vector3(0f, 0f, 90f);

    Animator anim;
    PlayerPush push;
    Transform body;
    PlayerMotor motor;

    void Awake()
    {
        anim = GetComponent<Animator>();

        // Found upward, like everything else on this model: the push lives on
        // the player root and this lives on the model beneath it.
        push = GetComponentInParent<PlayerPush>();
        body = push != null ? push.transform : transform;
        motor = GetComponentInParent<PlayerMotor>();
    }

    /// <summary>
    /// Which way this shove is aimed, pitch included.
    ///
    /// The body is welded to the camera in YAW and knows nothing about pitch,
    /// so shoving out of the body frame ignores the camera the moment anybody
    /// looks up or down.
    ///
    /// Locally the eye is the truth. On a TEAMMATE'S screen there is no eye to
    /// read, so it comes from the LookYaw and LookPitch already on the wire -
    /// the pair PlayerHeadlamp uses to point a crewmate's beam. That is the
    /// second customer for those two values, which is why they were worth
    /// replicating rather than deriving from the body.
    /// </summary>
    Quaternion Aim()
    {
        if (!followCamera) return Quaternion.LookRotation(body.forward, Vector3.up);

        float yaw = body.eulerAngles.y;
        float pitch = 0f;

        bool mine = motor != null && motor.IsLocal;

        if (mine && motor.Eye != null)
        {
            Vector3 e = motor.Eye.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }
        else if (motor != null)
        {
            var row = CrewMemberNet.ForSlot(motor.Slot);

            if (row != null)
            {
                yaw = row.LookYaw.Value;
                pitch = row.LookPitch.Value;
            }
        }

        // Euler angles arrive as 0..360, so a 20 degree look DOWN reads as 340
        // and would clamp to the limit instead of to a fifth of it.
        if (pitch > 180f) pitch -= 360f;

        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

        return Quaternion.Euler(pitch, yaw, 0f);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;
        if (push == null) return;

        float t = push.PushProgress;

        // Not swinging. Deliberately writes NOTHING - not even a zero weight -
        // because FirstPersonHands has already set the hands this frame and
        // zeroing them here would undo its work every single frame that
        // nobody was pushing.
        if (t < 0f) return;

        float w = weight * Ease(t);
        if (w <= 0.001f) return;

        Vector3 left = HandTarget(t, -1f);
        Vector3 right = HandTarget(t, +1f);

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, left);
        anim.SetIKPosition(AvatarIKGoal.RightHand, right);

        // ---- PALMS FLAT INTO THE SHOVE ----
        //
        // A hand bone has no standard orientation - which axis runs along the
        // fingers is a decision the rig made - so this cannot be derived and
        // has to be an offset from the direction of travel. Mirrored on Z
        // between the two hands, exactly as FirstPersonHands does it, because
        // a left hand is a right hand reflected.
        Quaternion look = Aim();

        Quaternion rightPalm = look * Quaternion.Euler(palmEuler);
        Quaternion leftPalm = look * Quaternion.Euler(palmEuler.x, palmEuler.y,
                                                      -palmEuler.z);

        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, w);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, w);
        anim.SetIKRotation(AvatarIKGoal.LeftHand, leftPalm);
        anim.SetIKRotation(AvatarIKGoal.RightHand, rightPalm);
    }

    /// <summary>
    /// Where one hand should be, this far through the swing.
    ///
    /// Built in the BODY's frame rather than the camera's, so the shove goes
    /// where the character is facing. Those are the same thing while you are
    /// pushing - the body is welded to the camera - but on a TEAMMATE'S screen
    /// only the body is known, and this has to look right there too.
    /// </summary>
    Vector3 HandTarget(float t, float side)
    {
        float forward = Extension(t);

        // Sideways and forward come from the AIM; the height still comes from
        // the body, because a shove leaves your shoulders wherever you happen
        // to be looking - it is the direction that pitches, not the chest.
        Quaternion aim = Aim();

        Vector3 offset = aim * new Vector3(side * spread * 0.5f, 0f, forward);

        return body.position + Vector3.up * height + offset;
    }

    /// <summary>
    /// How far forward the hands are, in metres. Negative during the wind-up.
    /// </summary>
    float Extension(float t)
    {
        if (t < windPart)
        {
            // Drawing back. Eased so the pull is soft and the release is not.
            float k = t / Mathf.Max(0.001f, windPart);
            return Mathf.Lerp(0f, -windBack, Smooth(k));
        }

        float rest = (t - windPart) / Mathf.Max(0.001f, 1f - windPart);

        // ---- OUT FIRMLY, BACK SLOWLY ----
        //
        // A single symmetrical curve gives a shove that retracts as hard as it
        // extends, which reads as a puppet being pulled. Real arms are thrown
        // out and then relax, so the return is deliberately the longer half.
        //
        // The thrust used to be a hard square-out over a tenth of a second,
        // which is the "really fast" that was reported: the arms crossed the
        // whole distance in about three frames, so there was no travel to see
        // at all - just hands appearing at the far end. Eased instead of
        // squared, and over a much longer slice.
        if (rest < thrustPart)
        {
            float k = rest / thrustPart;
            return Mathf.Lerp(-windBack, reach, Smooth(k));
        }

        float back = (rest - thrustPart) / (1f - thrustPart);
        return Mathf.Lerp(reach, 0f, Smooth(back));
    }

    /// <summary>
    /// Fade in at the start of the swing and out at the end, in swing-fraction
    /// rather than seconds, so a longer armTime fades proportionally.
    /// </summary>
    float Ease(float t)
    {
        float f = Mathf.Clamp01(fade / Mathf.Max(0.01f, push.armTime));
        if (f <= 0.001f) return 1f;

        return Mathf.Min(Mathf.Clamp01(t / f), Mathf.Clamp01((1f - t) / f));
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);
}
