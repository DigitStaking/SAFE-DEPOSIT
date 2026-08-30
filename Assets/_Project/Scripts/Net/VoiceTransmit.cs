// VoiceTransmit.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/VoiceTransmit.cs
// Goes on: the Player root.
//
// ====================================================================
// PHASE 4 STEP 10 - WHICH KEY, AND THEREFORE WHICH CHANNEL.
//
// Two ways to talk, and they are not settings of one another:
//
//   V   YOUR VOICE      positional, muffled by concrete, dies in one room
//   U   THE RADIO       clear, building-wide, exactly one person at a time
//
// This decides which one is live. It is deliberately separate from the audio
// pipeline and from the capture layer, because it is the only part of the
// three that is a GAME RULE rather than plumbing - and it is testable right
// now, with no microphone and no Steam, which is why it exists before either.
//
// PUSH TO TALK ON BOTH, AND THAT IS A DESIGN CHOICE, NOT A DEFAULT
//
// Open-mic proximity would be more "realistic" and much worse: four people
// breathing into a horror game is four people who never hear the building.
// The silence between voices is the whole atmosphere, and a key press is what
// protects it.
//
// THE RADIO WINS WHEN BOTH ARE HELD
//
// If you are holding U you have decided to talk to the crew, and it would be
// absurd for that to be overruled by a key you are also leaning on. It also
// means one hand can hold both and the louder intent survives.
// ====================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class VoiceTransmit : MonoBehaviour
{
    public enum Channel { Silent, Proximity, Radio }

    /// <summary>What the local player is transmitting on, right now. The
    /// capture layer reads this and nothing else.</summary>
    public static Channel Local { get; private set; } = Channel.Silent;

    [Tooltip("Hold to speak normally. Positional, occluded by concrete, and " +
             "it dies inside the room you are standing in.")]
    public Key proximityKey = Key.V;

    [Tooltip("Hold to speak on the radio. Clear, no distance, and only one " +
             "person at a time. Requires a walkie-talkie.")]
    public Key radioKey = Key.U;

    void Update()
    {
        if (!PlayerRegistry.IsLocalFor(this)) return;

        var kb = PlayerRegistry.KeysOf(this);
        if (kb == null) { Local = Channel.Silent; return; }

        // Downed and lost people do not talk. Being on the floor bleeding out
        // is not the moment to be coordinating, and somebody the mafia has is
        // not in the building at all.
        var me = Crew.Of(this);
        if (me.IsDowned || me.Lost) { Local = Channel.Silent; return; }

        bool radio = kb[radioKey].isPressed &&
                     me.HasWalkie &&
                     WalkieChannel.Instance != null &&
                     WalkieChannel.Instance.HeldByMe;

        if (radio) { Local = Channel.Radio; return; }

        Local = kb[proximityKey].isPressed ? Channel.Proximity : Channel.Silent;
    }

    void OnGUI()
    {
        if (!PlayerRegistry.IsLocalFor(this)) return;
        if (!RunHudGate.ShouldDrawGameplayHud()) return;
        if (Local == Channel.Silent) return;

        // The radio draws its own ON AIR line, and two banners saying the same
        // thing in different words is how a HUD starts lying to people.
        if (Local == Channel.Radio) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(0.6f, 1f, 0.7f, 0.9f);

        GUI.Label(new Rect(0f, 112f, Screen.width, 20f),
                  "speaking - they can only hear you in this room", style);
    }
}
