// PlayerCarryArms.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerCarryArms.cs
// Goes on: PlayerModel_FBX_VISUAL  (the SAME GameObject as the Animator).
//
// ========================================================================
// HANDS THAT GRIP THE SIDES OF WHATEVER YOU ARE ACTUALLY CARRYING.
//
// TWO LEVELS OF CONTROL, and it is worth knowing which one to reach for:
//
//   HERE, on the character       how this person grips ANYTHING.
//                                Per-hand offsets, palm angles, default
//                                finger curl. Change once, every item follows.
//
//   Carryable, on the item       how THIS OBJECT is gripped, overriding the
//                                measurement. Two hand positions and ten
//                                finger curls, saved in the prefab.
//
// Start here. Only reach for the item when this cannot be made right for it -
// which is the whole point of Auto mode existing.
//
// ---- WHY ONE HAND LOOKED RIGHT AND THE OTHER DID NOT ----
//
// "ONE HAND IS GOOD AND OTHER NOT GOOD"
//
// The grip used to be centred on the item's RENDERER BOUNDS centre, but
// PlayerCarry positions the item by its TRANSFORM. On any prop whose mesh
// pivot is not dead centre - a corner pivot, an off-centre child mesh, which
// is most imported art - those two are not the same point. The bounds then sit
// off to one side of the chest, both grip points shift the same way, and one
// arm reaches out comfortably while the other has to cross the body.
//
// So the sideways component is now taken from the HOLD ANCHOR - the point we
// asked the item to be at, which is centred on the body by construction -
// while the size still comes from the bounds. Symmetric whatever the pivot is
// doing.
//
// The other half was that nothing set hand ROTATION at all. The palms kept
// whatever the walk cycle had them at, which reads acceptably on one side and
// inside out on the other. They are placed now, and mirrored.
//
// ---- WHY MEASUREMENT AND NOT CLIPS ----
//
// A grab clip is authored for ONE hand separation. This game's loot runs from
// a can to a filing cabinet, so a single clip means the hands float inside the
// small things and clip through the big ones.
//
// This is the same kind of problem the legs solved - "put this hand at that
// computed point" is GEOMETRY, and IK is excellent at geometry. The push kept
// failing because "look like a person shoving" is PERFORMANCE, which IK is bad
// at. Same tool, opposite suitability.
//
// ON THE REAL BODY, SO TEAMMATES SEE IT TOO. The viewmodel gets the same grip
// points afterwards, from the same calculation.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(35)]          // after FirstPersonHands (30), before PlayerPushArms (40)
[RequireComponent(typeof(Animator))]
public class PlayerCarryArms : MonoBehaviour
{
    [Header("Auto grip - used unless the item overrides it")]
    [Tooltip("How far out toward the item's edges the hands sit, 0 to 1. " +
             "1 puts them at the corners, which looks like a struggle; a little " +
             "inside reads as a confident hold.")]
    [Range(0.3f, 1.2f)] public float gripWidth = 0.85f;

    [Tooltip("WHERE ON THE ITEM'S SIDE the fingers grip, 0 at the bottom edge " +
             "and 1 at the top. " +
             "Near the top, because that is how a person picks a crate up: " +
             "fingers hooked over the upper part of each side, and the box " +
             "HANGS from them. Palms flat underneath is a waiter with a tray, " +
             "and a tray has to be held up at chest height to be reachable at " +
             "all - which is why it kept ending up in the character's face.")]
    [Range(0f, 1f)] public float gripHeightOnBox = 0.78f;

    [Tooltip("How far INTO the item's side face the hands sit, in metres.")]
    public float gripInset = 0.02f;

    [Tooltip("How far toward the player the hands sit from the item's centre, " +
             "in metres. People carry a box with their hands on the NEAR half " +
             "of it, not reaching around the far side.")]
    public float gripToward = 0.06f;

    [Tooltip("Widest the hands will ever be placed apart, in metres. A vending " +
             "machine is wider than a person's arms and asking for its true " +
             "corners would just stretch them.")]
    public float maxGripWidth = 0.55f;

    [Tooltip("Centre the grip on the BODY rather than on the item's renderer " +
             "bounds. " +
             "This is the fix for one hand looking right and the other not. A " +
             "prop whose mesh pivot is off-centre has bounds that sit to one " +
             "side of your chest, which pushes both hands the same way and " +
             "makes one arm cross the body. Leave this on unless you have a " +
             "reason.")]
    public bool centreOnBody = true;

    [Header("Per hand - this character's own grip")]
    [Tooltip("Extra offset for the LEFT hand only, in the PLAYER'S space: " +
             "X sideways, Y up, Z forward, in metres.")]
    public Vector3 leftHandOffset = Vector3.zero;

    [Tooltip("Extra offset for the RIGHT hand only, in the PLAYER'S space.")]
    public Vector3 rightHandOffset = Vector3.zero;

    [Header("Palms")]
    [Tooltip("Rotate the palms to face the item instead of leaving them at " +
             "whatever the walk cycle had. " +
             "Off is how this shipped first, and it is why one hand looked " +
             "inside out.")]
    public bool useHandRotation = true;

    [Tooltip("Palm angle correction for the LEFT hand, in degrees, on top of " +
             "facing the item. " +
             "Hand bone axes differ per rig, so this is the number you tune " +
             "by eye - the same way palmEuler was tuned for the push. Nudge " +
             "one axis at a time.")]
    public Vector3 leftPalmEuler = new Vector3(-90f, 0f, 0f);

    [Tooltip("Palm angle correction for the RIGHT hand, in degrees.")]
    public Vector3 rightPalmEuler = new Vector3(-90f, 0f, 0f);

    [Tooltip("How strongly the palms are turned, 0 to 1. Drop it below 1 to " +
             "let the animation show through.")]
    [Range(0f, 1f)] public float rotationWeight = 1f;

    [Header("Fingers - the default, when the item does not say")]
    [Tooltip("Close the fingers around what is being carried. Needs " +
             "HandFingerCurl on this same object.")]
    public bool curlFingers = true;

    [Range(0f, 1f)] public float thumbCurl = 0.35f;
    [Range(0f, 1f)] public float indexCurl = 0.8f;
    [Range(0f, 1f)] public float middleCurl = 0.85f;
    [Range(0f, 1f)] public float ringCurl = 0.85f;
    [Range(0f, 1f)] public float littleCurl = 0.8f;

    [Header("Blend")]
    [Tooltip("Seconds to take hold and to let go. Snapping to the grip the " +
             "frame a pickup completes looks like the box teleports into your " +
             "hands.")]
    public float blendTime = 0.18f;

    [Tooltip("How strongly the hands are pulled to the item, 0 to 1.")]
    [Range(0f, 1f)] public float weight = 1f;

    [Header("Debug")]
    [Tooltip("Draw the two grip points and their palm directions in the Scene " +
             "view while carrying. The fastest way to tell whether a hand is " +
             "in the wrong PLACE or at the wrong ANGLE.")]
    public bool drawGrips = false;

    Animator anim;
    PlayerCarry carry;
    PlayerMotor motor;
    HandFingerCurl fingers;

    float live;                              // eased weight actually applied
    Vector3 posL, posR;                      // world, this frame
    Quaternion rotL = Quaternion.identity, rotR = Quaternion.identity;
    bool useL, useR;
    bool haveGrips;

    void Awake()
    {
        anim = GetComponent<Animator>();
        carry = GetComponentInParent<PlayerCarry>();
        motor = GetComponentInParent<PlayerMotor>();
        fingers = GetComponent<HandFingerCurl>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;

        haveGrips = Grips();

        float want = haveGrips ? weight : 0f;
        live = Mathf.MoveTowards(live, want,
                                 blendTime <= 0f ? 1f : Time.deltaTime / blendTime);

        // ---- ZEROED, NOT SKIPPED ----
        //
        // An IK weight PERSISTS. A goal that is simply not written keeps
        // whatever it had, so letting go of a crate while only skipping the
        // write would leave both hands clamped around a box that is no longer
        // there. FirstPersonHands shipped exactly that bug and it took four
        // rounds to find; this writes the zero. Fingers are the same - they
        // have to be told to open.
        if (live <= 0.001f)
        {
            Release(AvatarIKGoal.LeftHand);
            Release(AvatarIKGoal.RightHand);
            if (fingers != null) fingers.ClearAll();
            return;
        }

        Place(AvatarIKGoal.LeftHand, useL, posL, rotL);
        Place(AvatarIKGoal.RightHand, useR, posR, rotR);

        Curl();
    }

    void Release(AvatarIKGoal goal)
    {
        anim.SetIKPositionWeight(goal, 0f);
        anim.SetIKRotationWeight(goal, 0f);
    }

    void Place(AvatarIKGoal goal, bool used, Vector3 pos, Quaternion rot)
    {
        // A hand this item does not use is left entirely to the animation -
        // that is how a one-handed carry works, and it still has to be
        // released rather than skipped.
        if (!used) { Release(goal); return; }

        anim.SetIKPositionWeight(goal, live);
        anim.SetIKPosition(goal, pos);

        if (!useHandRotation) { anim.SetIKRotationWeight(goal, 0f); return; }

        anim.SetIKRotationWeight(goal, live * rotationWeight);
        anim.SetIKRotation(goal, rot);
    }

    /// <summary>
    /// Close the fingers - from the item's own numbers if it has them, from
    /// this character's defaults otherwise.
    /// </summary>
    void Curl()
    {
        if (fingers == null) return;

        if (!curlFingers) { fingers.ClearAll(); return; }

        var item = carry != null ? carry.Held : null;

        if (item != null && item.HasCustomGrip)
        {
            var l = item.leftGrip;
            var r = item.rightGrip;

            if (useL) fingers.SetCurl(true, l.thumb, l.index, l.middle, l.ring, l.little);
            else fingers.ClearCurl(true);

            if (useR) fingers.SetCurl(false, r.thumb, r.index, r.middle, r.ring, r.little);
            else fingers.ClearCurl(false);

            return;
        }

        fingers.SetCurl(true, thumbCurl, indexCurl, middleCurl, ringCurl, littleCurl);
        fingers.SetCurl(false, thumbCurl, indexCurl, middleCurl, ringCurl, littleCurl);
    }

    /// <summary>
    /// Where the two hands go this frame. Fills the posL/posR/rotL/rotR
    /// fields rather than returning them, because there are now four of them
    /// plus two flags and an out-parameter list that long is worse than a
    /// field.
    /// </summary>
    bool Grips()
    {
        useL = useR = false;

        if (carry == null || !carry.IsCarrying) return false;

        var item = carry.Held;
        if (item == null) return false;

        // ---- THE ITEM KNOWS BEST, IF IT WAS TOLD ----
        //
        // Custom points live in the item's own space, so they follow it
        // however it is turned and however it is scaled. Nothing to measure
        // and nothing to guess.
        if (item.HasCustomGrip)
        {
            item.WorldGrips(out posL, out rotL, out posR, out rotR);

            useL = item.leftGrip.used;
            useR = item.rightGrip.used;

            posL += Offset(leftHandOffset);
            posR += Offset(rightHandOffset);

            if (useHandRotation)
            {
                rotL = rotL * Quaternion.Euler(leftPalmEuler);
                rotR = rotR * Quaternion.Euler(rightPalmEuler);
            }

            return useL || useR;
        }

        // ---- OTHERWISE, MEASURE IT ----

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

        float half = Mathf.Max(b.extents.x, b.extents.z) * gripWidth;
        half = Mathf.Min(half, maxGripWidth);
        half = Mathf.Max(0.05f, half - gripInset);

        float gripY = Mathf.Lerp(b.min.y, b.max.y, gripHeightOnBox);

        Vector3 centre = new Vector3(b.center.x, gripY, b.center.z);

        // ---- THE ASYMMETRY FIX ----
        //
        // The item is positioned by its TRANSFORM but measured by its
        // RENDERER BOUNDS. On an off-centre pivot those disagree, and the
        // disagreement is entirely sideways as far as the hands care. Cancel
        // just that component against the anchor we asked the item to sit at,
        // and the two hands come out mirrored again - while height and depth
        // still come from the real geometry.
        if (centreOnBody)
        {
            Vector3 anchor = carry.HoldAnchor();
            float drift = Vector3.Dot(centre - anchor, side);
            centre -= side * drift;
        }

        centre += toward * gripToward;

        posL = centre - side * half + Offset(leftHandOffset);
        posR = centre + side * half + Offset(rightHandOffset);

        // Palms face each other across the object - the left hand looks right,
        // the right hand looks left - then each gets its own correction on
        // top, because a left hand is a right hand reflected and one shared
        // angle turns one of them inside out.
        Vector3 up = Vector3.up;
        rotL = Quaternion.LookRotation(side, up) * Quaternion.Euler(leftPalmEuler);
        rotR = Quaternion.LookRotation(-side, up) * Quaternion.Euler(rightPalmEuler);

        useL = useR = true;
        return true;
    }

    /// <summary>A per-hand nudge, given in the player's own frame so "forward"
    /// means forward from the character rather than along world Z.</summary>
    Vector3 Offset(Vector3 local)
    {
        if (local == Vector3.zero) return Vector3.zero;

        Transform f = motor != null ? motor.transform : transform;
        return f.right * local.x + Vector3.up * local.y + f.forward * local.z;
    }

    void OnDrawGizmos()
    {
        if (!drawGrips || !Application.isPlaying || !haveGrips) return;

        Draw(posL, rotL, useL, Color.cyan);
        Draw(posR, rotR, useR, Color.yellow);

        if (useL && useR)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(posL, posR);
        }
    }

    static void Draw(Vector3 p, Quaternion r, bool used, Color c)
    {
        if (!used) return;

        Gizmos.color = c;
        Gizmos.DrawSphere(p, 0.03f);

        // Which way the palm is pointing, so a hand at the right place but the
        // wrong angle is obvious instead of mysterious.
        Gizmos.DrawLine(p, p + r * Vector3.forward * 0.12f);
    }
}
