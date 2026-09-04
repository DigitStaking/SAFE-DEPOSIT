// PushProfile.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PushProfile.cs
// Create with: Assets / Create / SAFE DEPOSIT / Push Profile
//
// ========================================================================
// A NAMED, REUSABLE SHOVE.
//
// "Push Profile: Heavy Door ... Later, when the player interacts with that
//  object, the game should know: This object -> Heavy Door Push Profile ->
//  load these hand settings -> play push"
//
// ---- WHERE THIS DELIBERATELY DIVERGES FROM THE GRIP LIBRARY ----
//
// The Grip Library has no profiles. Its data lives directly on each item's
// Carryable component, saved inside that one prefab, referenced by nothing.
// That was the right call there and it is written down at length in
// GripLibraryWindow: a table keyed by item name orphans on the first rename
// and gives a duplicated prefab no grip at all.
//
// Copying that here would be the wrong call, and it is worth being precise
// about why rather than just following the pattern.
//
//   A GRIP is a property of ONE OBJECT. Where your fingers go on this
//   particular crate is a fact about this crate's geometry. Two crates of
//   different sizes do not want the same answer, so there is nothing to
//   share and embedding it in the prefab is exactly right.
//
//   A PUSH is a property of a KIND OF INTERACTION. "Heavy door" is how you
//   shove a heavy door - every heavy door, and the vending machine too. You
//   asked for reuse in the same sentence you asked for profiles, and reuse is
//   the thing per-object data cannot do. Tuning twelve doors one at a time,
//   and re-tuning all twelve when the feel is wrong, is the failure mode.
//
// So: a ScriptableObject asset with a name, and Pushable on the object holding
// a reference to it. Edit the profile once, every door that points at it
// changes.
//
// ---- AND IT SOLVES THE PERSISTENCE PROBLEM FOR FREE ----
//
// The grip workflow needed a whole "Push to Player prefab" mechanism -
// LoadPrefabContents, copy field by field, SaveAsPrefabAsset - purely because
// play-mode edits to a scene object are discarded on Stop.
//
// A ScriptableObject is already an asset. Editing it during play mode edits
// the FILE. There is nothing to push, nothing to remember to click, and no way
// to lose an hour of tuning by pressing Stop out of habit. This is the same
// trick FirstPersonViewmodelSettings used to end exactly that complaint.
// ========================================================================

using UnityEngine;

[CreateAssetMenu(fileName = "PushProfile",
                 menuName = "SAFE DEPOSIT/Push Profile")]
public class PushProfile : ScriptableObject
{
    // ------------------------------------------------------------------
    // ONE HAND
    // ------------------------------------------------------------------

    /// <summary>
    /// Where one hand goes at FULL push, and what its fingers do.
    ///
    /// Everything here is an OFFSET FROM WHERE THE HAND ALREADY IS, not a
    /// position. That is not a stylistic choice - it is the whole reason the
    /// gesture cannot snap. At the start of a push the offset is zero, so the
    /// hand does not move at all on the first frame; there is nothing to snap
    /// FROM because it is already where it was.
    /// </summary>
    [System.Serializable]
    public class HandPush
    {
        [Tooltip("Use this hand in the shove. Off leaves the arm to the " +
                 "animation - a one-handed shove against a door frame, say.")]
        public bool used = true;

        [Tooltip("Where this hand travels to, in CAMERA SPACE, in metres: " +
                 "X right, Y up, Z forward. " +
                 "An OFFSET from wherever the hand already is, so zero means " +
                 "'do not move this hand' and the gesture always starts from " +
                 "the exact resting pose.")]
        public Vector3 offset = new Vector3(0f, 0f, 0.2f);

        [Tooltip("How this hand turns at full push, in degrees, on top of " +
                 "whatever the animation has it at. Also additive, for the " +
                 "same reason as the position.")]
        public Vector3 rotation = Vector3.zero;

        [Header("Fingers - 0 straight, 1 fully curled")]
        [Tooltip("A shove is an OPEN hand. These default low on purpose - " +
                 "curled fingers read as a punch, not a push.")]
        [Range(0f, 1f)] public float thumb = 0.1f;
        [Range(0f, 1f)] public float index = 0.05f;
        [Range(0f, 1f)] public float middle = 0.05f;
        [Range(0f, 1f)] public float ring = 0.08f;
        [Range(0f, 1f)] public float little = 0.12f;

        public void CopyFrom(HandPush o)
        {
            used = o.used;
            offset = o.offset;
            rotation = o.rotation;
            thumb = o.thumb; index = o.index; middle = o.middle;
            ring = o.ring; little = o.little;
        }

        /// <summary>Mirror this hand onto the other one. Most shoves are
        /// symmetric, and authoring the same pose twice is how the two end up
        /// subtly different.</summary>
        public void MirrorInto(HandPush o)
        {
            o.used = used;
            o.offset = new Vector3(-offset.x, offset.y, offset.z);
            o.rotation = new Vector3(rotation.x, -rotation.y, -rotation.z);
            o.thumb = thumb; o.index = index; o.middle = middle;
            o.ring = ring; o.little = little;
        }
    }

    // ------------------------------------------------------------------

    [Tooltip("What this profile is for, in words. Shown in the Push Library " +
             "and in the log when it is used - so a shove that looks wrong can " +
             "be traced to the profile that produced it without guessing.")]
    public string displayName = "Push";

    [TextArea(2, 4)]
    [Tooltip("Optional note to yourself. What this was tuned against.")]
    public string notes = "";

    [Header("Hands")]
    public HandPush left = new HandPush();
    public HandPush right = new HandPush();

    [Tooltip("How far apart the hands travel during the shove, in metres. Two " +
             "palms going out, not one fist. Added on top of each hand's own " +
             "offset, so it stays useful even after the hands are placed.")]
    public float spread = 0.04f;

    [Header("Palms")]
    [Tooltip("How the palms flatten at full push, in degrees. " +
             "MIRRORED between the hands - Z is usually the one that matters " +
             "and the sign flips itself for the left hand - so this is the " +
             "control for 'palms go flat' as one gesture, while each hand's " +
             "own rotation above is for correcting them individually.")]
    public Vector3 palmRotation = new Vector3(0f, 0f, 55f);

    [Header("Timing - in real seconds")]
    [Tooltip("Seconds to reach the pushing pose.")]
    public float duration = 0.18f;

    [Tooltip("Seconds to HOLD at full extension before coming back.")]
    public float hold = 0.08f;

    [Tooltip("Seconds to return to rest. Longer than the reach: arms are " +
             "thrown out and then relax.")]
    public float returnTime = 0.3f;

    /// <summary>Total length of the gesture in seconds.</summary>
    public float TotalLength =>
        Mathf.Max(0.01f, duration) + Mathf.Max(0f, hold) + Mathf.Max(0.01f, returnTime);

    // ------------------------------------------------------------------

    [ContextMenu("Restore recommended settings")]
    public void RestoreRecommended()
    {
        left = new HandPush();
        right = new HandPush();
        spread = 0.04f;
        palmRotation = new Vector3(0f, 0f, 55f);
        duration = 0.18f;
        hold = 0.08f;
        returnTime = 0.3f;
    }

    public void CopyFrom(PushProfile o)
    {
        if (o == null) return;

        left.CopyFrom(o.left);
        right.CopyFrom(o.right);
        spread = o.spread;
        palmRotation = o.palmRotation;
        duration = o.duration;
        hold = o.hold;
        returnTime = o.returnTime;
    }
}
