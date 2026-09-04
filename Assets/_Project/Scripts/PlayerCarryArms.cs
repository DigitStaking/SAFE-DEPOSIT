// PlayerCarryArms.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerCarryArms.cs
// Goes on: PlayerModel_FBX_VISUAL  (the SAME GameObject as the Animator).
//
// ========================================================================
// HANDS THAT GRIP THE SIDES OF WHATEVER YOU ARE ACTUALLY CARRYING.
//
// "can we do something like when you grab box your hands will go below the box
//  automatically... or we gonna need an animation for that"
//
// No animation, and an animation would be WORSE here - which is worth saying
// plainly, because the last several rounds went the other way.
//
// A grab clip is authored for ONE hand separation. This game's loot runs from
// a can to a filing cabinet, so a single clip means the hands float inside the
// small things and clip through the big ones. You would need a clip per size
// and it would still be wrong for anything in between.
//
// Measured bounds give every item the right grip for free:
//
//     item's world bounds  ->  two points, on its side faces near the top
//                          ->  IK the hands there, and the box hangs from them
//
// This is the same kind of problem the legs solved - "put this hand at that
// computed point" is GEOMETRY, and IK is excellent at geometry. The push kept
// failing because "look like a person shoving" is PERFORMANCE, which IK is bad
// at. Same tool, opposite suitability, and the difference is worth keeping in
// mind for everything that comes after this.
//
// It also settles something reported much earlier and never fixed - "the boxes
// or items don't move with hands". Once the hands are placed from the object's
// own bounds, the hands and the object agree by construction rather than by
// two offsets being tuned to match.
//
// ON THE REAL BODY, SO TEAMMATES SEE IT TOO. The viewmodel gets the same grip
// points afterwards, from the same calculation - but this half is the one
// somebody else can watch, and the one you can check in third person with P.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(35)]          // after FirstPersonHands (30), before PlayerPushArms (40)
[RequireComponent(typeof(Animator))]
public class PlayerCarryArms : MonoBehaviour
{
    [Header("Grip")]
    [Tooltip("How far out toward the item's edges the hands sit, 0 to 1. " +
             "1 puts them at the corners, which looks like a struggle; a little " +
             "inside reads as a confident hold.")]
    [Range(0.3f, 1.2f)] public float gripWidth = 0.85f;

    [Tooltip("WHERE ON THE BOX'S SIDE the fingers grip, 0 at the bottom edge " +
             "and 1 at the top. " +
             "Near the top, because that is how a person actually picks a crate " +
             "up: fingers hooked over the upper part of each side, and the box " +
             "HANGS from them. Palms flat underneath - which is what this did " +
             "first - means presenting the box on a tray at chest height, " +
             "which is why it ended up in the character's face.")]
    [Range(0f, 1f)] public float gripHeightOnBox = 0.78f;

    [Tooltip("How far INTO the box's side face the hands sit, in metres. A " +
             "little inside so the fingers read as gripping the surface rather " +
             "than floating a centimetre off it.")]
    public float gripInset = 0.02f;

    [Tooltip("How far toward the player the hands sit from the item's centre, " +
             "in metres. People carry a box with their hands on the NEAR half " +
             "of it, not reaching around the far side.")]
    public float gripToward = 0.06f;

    [Tooltip("Widest the hands will ever be placed apart, in metres. A vending " +
             "machine is wider than a person's arms and asking for its true " +
             "corners would just stretch them.")]
    public float maxGripWidth = 0.55f;

    [Header("Blend")]
    [Tooltip("Seconds to take hold and to let go. Snapping to the grip the " +
             "frame a pickup completes looks like the box teleports into your " +
             "hands.")]
    public float blendTime = 0.18f;

    [Tooltip("How strongly the hands are pulled to the item, 0 to 1.")]
    [Range(0f, 1f)] public float weight = 1f;

    [Header("Debug")]
    [Tooltip("Draw the two grip points in the Scene view while carrying.")]
    public bool drawGrips = false;

    Animator anim;
    PlayerCarry carry;
    PlayerMotor motor;

    float live;                  // eased weight actually applied
    Vector3 gripL, gripR;        // world, this frame
    bool haveGrips;

    void Awake()
    {
        anim = GetComponent<Animator>();
        carry = GetComponentInParent<PlayerCarry>();
        motor = GetComponentInParent<PlayerMotor>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;

        haveGrips = Grips(out gripL, out gripR);

        float want = haveGrips ? weight : 0f;
        live = Mathf.MoveTowards(live, want,
                                 blendTime <= 0f ? 1f : Time.deltaTime / blendTime);

        // ---- ZEROED, NOT SKIPPED ----
        //
        // An IK weight PERSISTS. A goal that is simply not written keeps
        // whatever it had, so letting go of a crate while only skipping the
        // write would leave both hands clamped around a box that is no longer
        // there. FirstPersonHands shipped exactly that bug and it took four
        // rounds to find; this writes the zero.
        if (live <= 0.001f)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, live);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, live);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, gripL);
        anim.SetIKPosition(AvatarIKGoal.RightHand, gripR);
    }

    /// <summary>
    /// Where the two hands should be, from the item's own measured bounds.
    ///
    /// Works for any size without being told the size: a can gets narrow
    /// hands low down, a cabinet gets wide hands, and neither needed a clip
    /// authored for it.
    /// </summary>
    bool Grips(out Vector3 left, out Vector3 right)
    {
        left = right = Vector3.zero;

        if (carry == null || !carry.IsCarrying) return false;

        var item = carry.Held;
        if (item == null) return false;

        var b = item.WorldBounds;

        // Sideways is the PLAYER'S right, flattened - the hands go to the
        // sides of the box as the player sees it, not to whichever way the
        // world's X axis happens to point.
        Vector3 side = motor != null ? motor.transform.right : transform.right;
        side.y = 0f;

        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        side.Normalize();

        Vector3 toward = motor != null ? -motor.transform.forward : -transform.forward;
        toward.y = 0f;
        if (toward.sqrMagnitude > 0.0001f) toward.Normalize();

        // ---- ON THE SIDES, NOT UNDERNEATH ----
        //
        // Nobody carries a crate on flat palms held out in front. You hook
        // your fingers over each side near the top and the box HANGS from
        // them, down by your waist. Palms-underneath is a waiter with a tray,
        // and it forces the box up to chest height to be reachable - which is
        // exactly how it ended up in the character's face.
        //
        // So the grip is at the box's own side faces, at a height taken from
        // the box itself rather than a fixed offset: a tall cabinet is gripped
        // near ITS top, a small crate near ITS top, and both look right without
        // being told which they are.
        float half = Mathf.Max(b.extents.x, b.extents.z) * gripWidth;
        half = Mathf.Min(half, maxGripWidth);
        half = Mathf.Max(0.05f, half - gripInset);

        float gripY = Mathf.Lerp(b.min.y, b.max.y, gripHeightOnBox);

        Vector3 centre = new Vector3(b.center.x, gripY, b.center.z)
                       + toward * gripToward;

        left = centre - side * half;
        right = centre + side * half;

        return true;
    }

    void OnDrawGizmos()
    {
        if (!drawGrips || !Application.isPlaying || !haveGrips) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(gripL, 0.035f);
        Gizmos.DrawSphere(gripR, 0.035f);
        Gizmos.DrawLine(gripL, gripR);
    }
}
