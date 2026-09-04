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

[DefaultExecutionOrder(35)]          // before PlayerPushArms (40) and HandFingerCurl (60)
[RequireComponent(typeof(Animator))]
public class PlayerCarryArms : MonoBehaviour
{
    // ====================================================================
    // THE CHARACTER'S DEFAULT MEASUREMENTS.
    //
    // Used for any item that has not been given its own. An item that HAS -
    // Carryable.overrideMeasure - wins, because "each box has her own
    // dimension" cannot be answered from here.
    //
    // The five numbers live on Carryable.GripMeasure so that there is exactly
    // one definition of what they mean, one function that measures with them,
    // and one set of known-good defaults. They used to be five loose fields
    // here plus a second copy of the arithmetic on Carryable plus the same
    // four constants hardcoded in two editor tools.
    // ====================================================================

    [Header("Default measurements - an item may override these")]
    public Carryable.GripMeasure measure = Carryable.GripMeasure.Default;

    [Tooltip("Centre the grip on the BODY rather than on the item's renderer " +
             "bounds. " +
             "This is the fix for one hand looking right and the other not. A " +
             "prop whose mesh pivot is off-centre has bounds that sit to one " +
             "side of your chest, which pushes both hands the same way and " +
             "makes one arm cross the body. Leave this on unless you have a " +
             "reason.")]
    public bool centreOnBody = true;

    // ====================================================================
    // CHARACTER-LEVEL NUDGES - AUTO MODE ONLY.
    //
    // These do NOT apply to an item with a Custom grip, and that restriction
    // is the point rather than an oversight.
    //
    // They used to be added on top of every grip including Custom ones, which
    // broke the promise that Item A's grip belongs to Item A: changing the
    // character's palm angle silently re-posed every hand-placed item in the
    // game.
    //
    // It was also a corruption bug. The Grip Library saved the COMPUTED world
    // points, which already included these - so a save baked leftPalmEuler
    // into the item, and the next pickup multiplied it in again. Every press
    // of the button rotated that item's saved palms another 90 degrees. Two
    // presses and they were inside out.
    //
    // Fixed at both ends: these are Auto-only, and the save writes fields
    // verbatim.
    // ====================================================================

    [Header("Auto-mode nudges - NOT applied to a Custom grip")]
    [Tooltip("Extra offset for the LEFT hand only, in the PLAYER'S space: " +
             "X sideways, Y up, Z forward, in metres. " +
             "AUTO MODE ONLY - an item with a Custom grip is placed exactly " +
             "where its own points say, so that its grip stays its own.")]
    public Vector3 leftHandOffset = Vector3.zero;

    [Tooltip("Extra offset for the RIGHT hand only, in the PLAYER'S space. " +
             "Auto mode only.")]
    public Vector3 rightHandOffset = Vector3.zero;

    [Header("Elbow")]
    [Tooltip("How far off the arm the elbow hint is placed, in metres. " +
             "The hint defines a PLANE, so it has to sit clearly off the " +
             "shoulder-to-hand line. The first working version put it 12cm " +
             "out - roughly where the elbow already was - so the solver was " +
             "asked to move the elbow to where it already was. Correct, and " +
             "completely invisible.\n\n" +
             "0.5 reads clearly on this rig. Smaller is subtler, not broken.")]
    public float elbowHintRadius = 0.5f;

    [Header("Palms")]
    [Tooltip("Rotate the palms to face the item instead of leaving them at " +
             "whatever the walk cycle had. " +
             "Off is how this shipped first, and it is why one hand looked " +
             "inside out.")]
    public bool useHandRotation = true;

    [Tooltip("Palm angle correction for the LEFT hand, in degrees. " +
             "AUTO MODE ONLY. A Custom grip carries its own palm angle per " +
             "hand, and mixing this in on top is what used to rotate a saved " +
             "grip by another 90 degrees every time it was saved.\n\n" +
             "Hand bone axes differ per rig, so this is a tune-by-eye number. " +
             "Nudge one axis at a time.")]
    public Vector3 leftPalmEuler = new Vector3(-90f, 0f, 0f);

    [Tooltip("Palm angle correction for the RIGHT hand, in degrees. Auto " +
             "mode only.")]
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

    // ====================================================================
    // BACK TO KNOWN-GOOD.
    //
    // "parametre are different i don't know how i can test and fill this"
    //
    // Fair. There are eighteen numbers here and no way to tell from looking
    // whether the one in front of you is a considered value or something a
    // slider got dragged past on the way somewhere else - and several of them
    // had drifted a long way from where they started.
    //
    // So there is a floor to come back to. Nothing here needs to be filled in
    // by hand to get a working grip: these values ARE the working grip, and
    // Auto mode uses them on every item with no per-item setup at all.
    // ====================================================================

    [ContextMenu("Restore recommended settings")]
    public void RestoreRecommended()
    {
        measure = Carryable.GripMeasure.Default;
        centreOnBody = true;

        leftHandOffset = Vector3.zero;
        rightHandOffset = Vector3.zero;

        useHandRotation = true;
        leftPalmEuler = new Vector3(-90f, 0f, 0f);
        rightPalmEuler = new Vector3(-90f, 0f, 0f);
        rotationWeight = 1f;

        curlFingers = true;
        thumbCurl = 0.35f;
        indexCurl = 0.8f;
        middleCurl = 0.85f;
        ringCurl = 0.85f;
        littleCurl = 0.8f;

        elbowHintRadius = 0.5f;
        blendTime = 0.18f;
        weight = 1f;
    }

    /// <summary>Unity calls this when the component is first added, and from
    /// the gear menu's Reset. Same numbers, so both routes land in the same
    /// place.</summary>
    void Reset() => RestoreRecommended();

    Animator anim;
    PlayerCarry carry;
    PlayerMotor motor;
    HandFingerCurl fingers;

    float live;                              // eased weight actually applied
    Vector3 posL, posR;                      // world, this frame
    Quaternion rotL = Quaternion.identity, rotR = Quaternion.identity;
    bool useL, useR;
    bool haveGrips;

    // ====================================================================
    // WHAT IS IN THE HANDS, READABLE FROM OUTSIDE.
    //
    // Deliberately ONE accessor. There used to be six more - the computed
    // world positions and rotations of both hands - so the Grip Library could
    // save what it saw back onto the prefab.
    //
    // That was the corruption bug. The computed points already had this
    // character's offsets and palm angles folded in, so saving them baked
    // those into the item and the next pickup folded them in again. The Grip
    // Library saves FIELDS now, verbatim, and has no reason to ask what the
    // hands worked out - so the accessors that let it ask are gone rather than
    // left lying around to be used again by mistake.
    // ====================================================================

    /// <summary>What is in the hands this frame, or null.</summary>
    public Carryable LiveItem => carry != null ? carry.Held : null;

    /// <summary>
    /// Copy every tunable value off another instance.
    ///
    /// Exists so a play-mode instance can be written back onto the prefab -
    /// field by field rather than by serialising the whole component, because
    /// the component also holds runtime state (the eased weight, the cached
    /// Animator) that has no business being saved into an asset.
    /// </summary>
    public void CopySettingsFrom(PlayerCarryArms from)
    {
        if (from == null) return;

        measure = from.measure;
        centreOnBody = from.centreOnBody;

        leftHandOffset = from.leftHandOffset;
        rightHandOffset = from.rightHandOffset;

        useHandRotation = from.useHandRotation;
        leftPalmEuler = from.leftPalmEuler;
        rightPalmEuler = from.rightPalmEuler;
        rotationWeight = from.rotationWeight;

        curlFingers = from.curlFingers;
        thumbCurl = from.thumbCurl;
        indexCurl = from.indexCurl;
        middleCurl = from.middleCurl;
        ringCurl = from.ringCurl;
        littleCurl = from.littleCurl;

        elbowHintRadius = from.elbowHintRadius;
        blendTime = from.blendTime;
        weight = from.weight;
        drawGrips = from.drawGrips;
    }

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

        // Elbows come from the ITEM only. There is deliberately no character
        // default: an elbow that is wrong is far more noticeable than an elbow
        // the solver chose for itself, so nothing steers one unless an item
        // explicitly asks.
        var held = carry != null ? carry.Held : null;

        Elbow(AvatarIKHint.LeftElbow, true, held, useL);
        Elbow(AvatarIKHint.RightElbow, false, held, useR);

        Curl();
    }

    void Release(AvatarIKGoal goal)
    {
        anim.SetIKPositionWeight(goal, 0f);
        anim.SetIKRotationWeight(goal, 0f);

        // An IK HINT persists exactly like an IK goal does. Letting go of a
        // crate without releasing the elbow leaves the arm bent around
        // something that is no longer there - the same bug as the hands, one
        // channel over, and it would have looked like a broken walk cycle
        // rather than a carry bug.
        anim.SetIKHintPositionWeight(
            goal == AvatarIKGoal.LeftHand ? AvatarIKHint.LeftElbow
                                          : AvatarIKHint.RightElbow, 0f);
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

    /// <summary>True while this arm is stretched so straight that no elbow
    /// hint can move it. Read by the Grip Library, which is an editor tool -
    /// nothing in a build looks at these.</summary>
    public bool LeftArmStraight { get; private set; }
    public bool RightArmStraight { get; private set; }

    /// <summary>
    /// Swing one elbow around the arm, if the item asked for it.
    ///
    /// ---- WHY THE FIRST TWO VERSIONS DID NOTHING ----
    ///
    /// v1 rotated the elbow's CURRENT offset from the shoulder. That is the
    /// identity whenever the arm is straight, because rotating a vector about
    /// an axis it is parallel to changes nothing. Same hint at 20 degrees and
    /// at 160 - exactly zero effect, which is what was reported.
    ///
    /// v2 built the hint from a perpendicular reference, which fixed the
    /// degenerate case, but placed it only reach*0.25 - about 12cm - off the
    /// arm line. That is roughly where the elbow already is, so the solver was
    /// being asked to move it to where it already was. Correct, and invisible.
    ///
    /// The hint defines a PLANE. It wants to be clearly off the arm line, far
    /// enough that the direction is unambiguous. elbowHintRadius is that
    /// distance and it is tunable, because "far enough to read" depends on the
    /// character's size.
    ///
    /// ---- STILL ONE SOLVER ----
    ///
    /// AvatarIKHint is the elbow channel of the same humanoid IK that places
    /// the hand, in the same OnAnimatorIK pass. The hand does not move; only
    /// the bend between shoulder and hand changes.
    /// </summary>
    void Elbow(AvatarIKHint hint, bool leftHand, Carryable item, bool used)
    {
        if (leftHand) LeftArmStraight = false; else RightArmStraight = false;

        if (!used || item == null ||
            !item.ElbowSwing(leftHand, out float degrees, out float w))
        {
            anim.SetIKHintPositionWeight(hint, 0f);
            return;
        }

        Transform shoulder = anim.GetBoneTransform(
            leftHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        Transform elbow = anim.GetBoneTransform(
            leftHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Transform hand = anim.GetBoneTransform(
            leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);

        if (shoulder == null || elbow == null || hand == null)
        {
            anim.SetIKHintPositionWeight(hint, 0f);
            return;
        }

        Vector3 axis = hand.position - shoulder.position;
        float span = axis.magnitude;

        if (span < 1e-4f)
        {
            anim.SetIKHintPositionWeight(hint, 0f);
            return;
        }

        axis /= span;

        float reach = Vector3.Distance(shoulder.position, elbow.position) +
                      Vector3.Distance(elbow.position, hand.position);

        // ---- THE CONDITION THAT SILENTLY DISABLES ALL OF THIS ----
        //
        // An arm stretched to within 3% of its full length has no bend left,
        // and an elbow with no bend sits ON the shoulder-to-hand line where
        // nothing can steer it. Recorded rather than compensated for, because
        // the cause is a hand target too far away - a GRIP problem - and
        // quietly bending the arm anyway would hide it.
        bool straight = span >= reach * 0.97f;
        if (leftHand) LeftArmStraight = straight; else RightArmStraight = straight;

        // A reference that does not depend on the current bend: straight down
        // the body, flattened against the arm axis. That is where a relaxed
        // elbow hangs, so 0 reads as normal and the sweep goes out from there.
        Transform body = motor != null ? motor.transform : transform;

        Vector3 reference = Vector3.ProjectOnPlane(-body.up, axis);

        if (reference.sqrMagnitude < 1e-6f)
            reference = Vector3.ProjectOnPlane(-body.forward, axis);

        if (reference.sqrMagnitude < 1e-6f)
        {
            anim.SetIKHintPositionWeight(hint, 0f);
            return;
        }

        Vector3 dir = (Quaternion.AngleAxis(degrees, axis) * reference.normalized);

        anim.SetIKHintPositionWeight(hint, live * w);
        anim.SetIKHintPosition(hint,
            shoulder.position + axis * (span * 0.5f) + dir * elbowHintRadius);
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

        return item.HasCustomGrip ? CustomGrip(item) : MeasuredGrip(item);
    }

    /// <summary>
    /// The item's own hand-placed grip, used EXACTLY as saved.
    ///
    /// Nothing from this character is mixed in - no offsets, no palm angles.
    /// That is the whole contract of a Custom grip: Item A's grip belongs to
    /// Item A, so tuning the character cannot silently re-pose it, and saving
    /// it back cannot accumulate anything that was not in the fields.
    /// </summary>
    bool CustomGrip(Carryable item)
    {
        item.WorldGrips(out posL, out rotL, out posR, out rotR);

        useL = item.leftGrip.used;
        useR = item.rightGrip.used;

        return useL || useR;
    }

    /// <summary>
    /// Measured from the item's bounds, for anything not hand-placed.
    ///
    /// The numbers come from the ITEM if it was given its own, and from this
    /// character otherwise - so a single crate can be nudged without moving
    /// every other crate in the building.
    /// </summary>
    bool MeasuredGrip(Carryable item)
    {
        // Sideways is the PLAYER'S right, flattened - the hands go to the
        // sides of the box as the player sees it, not to whichever way the
        // world's X axis happens to point.
        Vector3 side = motor != null ? motor.transform.right : transform.right;
        side.y = 0f;
        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        side.Normalize();

        Vector3 toward = motor != null ? -motor.transform.forward : -transform.forward;
        toward.y = 0f;

        // ONE implementation of the measurement, shared with the editor's
        // seed button - so what you preview is what you play.
        item.MeasuredGrips(side, toward, item.MeasureOr(measure),
                           out posL, out posR);

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
            Vector3 mid = (posL + posR) * 0.5f;
            float drift = Vector3.Dot(mid - anchor, side);

            posL -= side * drift;
            posR -= side * drift;
        }

        posL += Offset(leftHandOffset);
        posR += Offset(rightHandOffset);

        // Palms face each other across the object - the left hand looks right,
        // the right hand looks left - then each gets its own correction on
        // top, because a left hand is a right hand reflected and one shared
        // angle turns one of them inside out.
        rotL = Quaternion.LookRotation(side, Vector3.up) * Quaternion.Euler(leftPalmEuler);
        rotR = Quaternion.LookRotation(-side, Vector3.up) * Quaternion.Euler(rightPalmEuler);

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
