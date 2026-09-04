// HandFingerCurl.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/HandFingerCurl.cs
// Goes on: PlayerModel_FBX_VISUAL, and the first-person arms clone.
//
// ========================================================================
// FINGERS THAT ACTUALLY CLOSE AROUND THINGS - AND ACTUALLY OPEN.
//
// "look at the fingers of left hand they are at 0 and his hand looks like he
//  open his hand but when i put 0 in right hand fingers his fingers stayed
//  closed not open"
//
// Exactly right, and the cause was not the right hand. It was what ZERO meant.
//
// ---- WHY 0 USED TO MEAN NOTHING ----
//
// The first version was purely ADDITIVE: it bent each joint on top of whatever
// the animation clip had. So 0 did not mean "open", it meant "write nothing and
// let the clip decide" - and this rig's clip poses one hand open and the other
// closed. Two hands, same number, different result, and nothing in the finger
// code was asymmetric at all.
//
// That is a bad contract regardless of the clip. A grip is a definite pose:
// asking for an open hand should produce an open hand on any character, in any
// clip, on any frame.
//
// ---- SO THE CURL IS ABSOLUTE NOW ----
//
//     0  straight, from the rig's own BIND POSE
//     1  fully curled
//
// The bind pose comes from Avatar.humanDescription.skeleton - the rotations the
// model was authored with, before any animation. That is a real "straight" for
// THIS rig, readable at any moment, needing no reference captured at a lucky
// frame.
//
// While a hand is being driven these bones are OURS: the clip's finger pose is
// replaced rather than added to. When the hand is released the override eases
// back to zero over blendTime and the clip has them again - so idle hands still
// breathe and swing normally.
//
// ---- WHICH WAY IS "CLOSED" ----
//
// Measured, never assumed, because finger bone axes are whatever the rigger
// felt like and hard-coding one produces sideways fingers on the next model:
//
//     palm normal = across the knuckles  x  along the fingers
//     bend axis   = along this joint     x  palm normal
//
// Rotating positive about that always moves the tip toward the palm. One sign
// flip for the left hand, because a left hand is a right hand reflected - a
// fact about mirrored geometry, not about this rig.
//
// An earlier version picked the sign by rotating a fingertip both ways and
// keeping whichever ended closer to the WRIST. That is geometrically
// degenerate: the axis is perpendicular to the wrist-to-knuckle line, so both
// answers are near-identical and the winner was decided by rounding error. It
// came out right on one hand and wrong on the other.
//
// ---- RUNS IN LateUpdate ----
//
// The Animator writes the whole skeleton during its own update and solves IK
// inside that. This runs after all of it, so it layers on top of the pose the
// walk cycle and the hand IK have already agreed on.
// ========================================================================

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(60)]          // after PlayerCarryArms (35), PlayerPushArms (40)
[RequireComponent(typeof(Animator))]
public class HandFingerCurl : MonoBehaviour
{
    [Header("How far each joint bends at full curl, in degrees")]
    [Tooltip("The knuckle joint - the one at the base of the finger.")]
    public float proximalDegrees = 55f;

    [Tooltip("The middle joint. Bends the most on a real hand.")]
    public float intermediateDegrees = 65f;

    [Tooltip("The fingertip joint.")]
    public float distalDegrees = 35f;

    [Tooltip("Thumb joints bend less than fingers and it looks broken if they " +
             "do not. Scales all three thumb angles.")]
    [Range(0f, 1f)] public float thumbScale = 0.55f;

    [Header("Blend")]
    [Tooltip("Seconds to take control of the fingers and to hand them back. " +
             "Handing back matters as much as taking over: dropping a crate " +
             "should relax the fingers into the walk cycle, not snap them.")]
    public float blendTime = 0.14f;

    [Header("Debug")]
    [Tooltip("Log what was measured for each hand at startup: how many finger " +
             "bones were found, how many had a readable bind pose, and whether " +
             "the hand came out usable. Worth one look on a new rig.")]
    public bool logCalibration = false;

    Animator anim;

    // The five curls asked for, per hand, and the eased values actually used.
    readonly float[] wantL = new float[5];
    readonly float[] wantR = new float[5];
    readonly float[] liveL = new float[5];
    readonly float[] liveR = new float[5];

    // Whether anybody is currently driving each hand, and how strongly we have
    // taken it over. SEPARATE FROM THE CURL VALUES, which is the whole fix:
    // "drive this hand, at curl 0" and "do not drive this hand" used to be the
    // same state, so an open hand was indistinguishable from no opinion.
    readonly bool[] driven = new bool[2];
    readonly float[] authority = new float[2];

    // Bones, cached: [hand][finger][joint]
    readonly Transform[,,] bones = new Transform[2, 5, 3];

    // The rig's own straight pose for each of those.
    readonly Quaternion[,,] bind = new Quaternion[2, 5, 3];
    readonly bool[,,] haveBind = new bool[2, 5, 3];

    readonly Transform[] hands = new Transform[2];
    readonly bool[] ready = new bool[2];

    // Measured per hand, once: which way the palm faces, in the HAND bone's own
    // space so it stays correct wherever the arm swings.
    readonly Vector3[] palmLocal = new Vector3[2];

    static readonly HumanBodyBones[,] Map = new HumanBodyBones[2, 5]
    {
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftLittleProximal
        },
        {
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightIndexProximal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal,
            HumanBodyBones.RightLittleProximal
        }
    };

    bool cached;

    void Awake() => TryCache();

    /// <summary>
    /// Cache the bones, and be willing to try again.
    ///
    /// On the real body Awake is plenty. On the VIEWMODEL clone this component
    /// is added while the rig is still being assembled, so the Animator may not
    /// have its avatar yet and isHuman comes back false - cache once in Awake
    /// and those fingers never move again, with nothing in the failure pointing
    /// at construction order. So it retries until it succeeds, then stops
    /// asking.
    /// </summary>
    bool TryCache()
    {
        if (cached) return true;

        if (anim == null) anim = GetComponent<Animator>();
        if (anim == null || !anim.isHuman) return false;

        Cache();
        cached = true;
        return true;
    }

    void Cache()
    {
        hands[0] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        hands[1] = anim.GetBoneTransform(HumanBodyBones.RightHand);

        // ---- THE RIG'S OWN STRAIGHT POSE ----
        //
        // humanDescription.skeleton holds the rotations the model was authored
        // with, before any clip touched it. That is a real "open hand" for THIS
        // rig, readable at any moment - no need to catch the skeleton at a lucky
        // frame before the Animator has run, which is the kind of timing
        // assumption that works on the body and fails on the clone.
        var byName = new Dictionary<string, Quaternion>();

        if (anim.avatar != null && anim.avatar.isValid)
            foreach (var sb in anim.avatar.humanDescription.skeleton)
                byName[sb.name] = sb.rotation;

        for (int h = 0; h < 2; h++)
        {
            int found = 0;
            int binds = 0;

            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                {
                    var b = anim.GetBoneTransform((HumanBodyBones)((int)Map[h, f] + j));
                    bones[h, f, j] = b;

                    if (b == null) continue;
                    found++;

                    if (byName.TryGetValue(b.name, out Quaternion q))
                    {
                        bind[h, f, j] = q;
                        binds++;
                    }
                    else
                    {
                        // Fallback: whatever it is right now. Worse, because the
                        // clip may already have moved it - but a hand that curls
                        // from a slightly wrong straight beats a hand that does
                        // not curl at all.
                        bind[h, f, j] = b.localRotation;
                    }

                    haveBind[h, f, j] = true;
                }

            // Needs the hand and at least the index and middle knuckles: the
            // palm normal is measured across the knuckles, so one finger is not
            // enough to measure anything.
            ready[h] = hands[h] != null && found >= 6 && Calibrate(h);

            if (logCalibration)
                Debug.Log("[Fingers] " + (h == 0 ? "Left" : "Right") +
                          "  bones " + found + "/15" +
                          "  bindPose " + binds + "/15" +
                          "  ready " + ready[h]);
            else if (!ready[h])
                Debug.LogWarning("[Fingers] " + (h == 0 ? "Left" : "Right") +
                                 " hand unusable (" + found + "/15 bones). The " +
                                 "avatar probably has fingers unmapped.");
        }
    }

    /// <summary>Work out which way this rig's fingers close, from the rig
    /// itself.</summary>
    bool Calibrate(int h)
    {
        Transform hand = hands[h];
        Transform indexP = bones[h, 1, 0];
        Transform littleP = bones[h, 4, 0];
        Transform middleP = bones[h, 2, 0];

        Vector3 across;

        if (indexP != null && littleP != null)
            across = littleP.position - indexP.position;
        else if (indexP != null && middleP != null)
            across = middleP.position - indexP.position;
        else
            return false;

        Transform anyKnuckle = Pick(middleP, indexP, littleP);
        if (anyKnuckle == null) return false;

        Vector3 alongFingers = anyKnuckle.position - hand.position;

        if (across.sqrMagnitude < 1e-10f || alongFingers.sqrMagnitude < 1e-10f)
            return false;

        // Perpendicular to the plane of the hand. Which FACE it lands on depends
        // only on handedness - a left hand is a right hand reflected - so one
        // flip covers it, on every rig, forever.
        Vector3 palm = Vector3.Cross(across.normalized, alongFingers.normalized);
        if (palm.sqrMagnitude < 1e-10f) return false;

        if (h == 0) palm = -palm;          // left hand: the cross lands on the back

        palmLocal[h] = hand.InverseTransformDirection(palm.normalized);
        return true;
    }

    /// <summary>First of these that actually exists. Written out rather than
    /// using ?? because that operator bypasses Unity's overloaded null check and
    /// will happily hand back a destroyed object.</summary>
    static Transform Pick(params Transform[] options)
    {
        for (int i = 0; i < options.Length; i++)
            if (options[i] != null) return options[i];
        return null;
    }

    // ------------------------------------------------------------------
    // ASKED FOR BY WHOEVER IS HOLDING SOMETHING
    // ------------------------------------------------------------------

    /// <summary>
    /// Drive one hand. Five values, 0 STRAIGHT to 1 fully curled.
    ///
    /// Zero is a real instruction now, not the absence of one: it opens the
    /// hand. Stop driving with ClearCurl.
    /// </summary>
    public void SetCurl(bool leftHand, float thumb, float index, float middle,
                        float ring, float little)
    {
        float[] w = leftHand ? wantL : wantR;

        w[0] = thumb; w[1] = index; w[2] = middle; w[3] = ring; w[4] = little;
        driven[leftHand ? 0 : 1] = true;
    }

    /// <summary>Hand this one back to the animation. Must be CALLED, not just
    /// left alone - these bones keep whatever they were last given, exactly like
    /// an IK weight does.</summary>
    public void ClearCurl(bool leftHand) => driven[leftHand ? 0 : 1] = false;

    /// <summary>Hand both back to the animation.</summary>
    public void ClearAll()
    {
        driven[0] = false;
        driven[1] = false;
    }

    // ------------------------------------------------------------------

    void LateUpdate()
    {
        if (!TryCache()) return;

        float step = blendTime <= 0f ? 1f : Time.deltaTime / blendTime;

        for (int h = 0; h < 2; h++)
        {
            float[] want = h == 0 ? wantL : wantR;
            float[] live = h == 0 ? liveL : liveR;

            for (int f = 0; f < 5; f++)
                live[f] = Mathf.MoveTowards(live[f], Mathf.Clamp01(want[f]), step);

            // How much of this hand is ours. Eased, so letting go relaxes the
            // fingers into the walk cycle instead of snapping them back.
            authority[h] = Mathf.MoveTowards(authority[h], driven[h] ? 1f : 0f, step);

            if (!ready[h] || authority[h] <= 0.001f) continue;

            // Back into world space from the hand's CURRENT orientation, so the
            // fingers close correctly whatever the arm is doing.
            Vector3 palm = hands[h].TransformDirection(palmLocal[h]);
            if (palm.sqrMagnitude < 1e-8f) continue;
            palm.Normalize();

            for (int f = 0; f < 5; f++)
            {
                float scale = (f == 0 ? thumbScale : 1f) * live[f];

                // NOT skipped at zero. Zero is "hold this finger straight",
                // which is an instruction - and skipping it is exactly what made
                // an open hand mean "whatever the clip wants", so one hand came
                // out open and the other closed for the same number.
                Pose(h, f, 0, bones[h, f, 1], palm, proximalDegrees * scale);
                Pose(h, f, 1, bones[h, f, 2], palm, intermediateDegrees * scale);
                Pose(h, f, 2, null, palm, distalDegrees * scale);
            }
        }
    }

    /// <summary>
    /// Put one joint where it should be: straight, plus however much curl.
    ///
    /// ---- WHY IT ROUTES THROUGH THE BIND POSE ----
    ///
    /// The result has to be the SAME whatever the clip was doing, or 0 does not
    /// mean open. So the joint is set to its authored straight rotation first,
    /// bent from there, and only then blended against the clip by how much
    /// authority we have. At full authority the clip's finger pose is gone.
    ///
    /// ---- WHY THE AXIS IS PER JOINT ----
    ///
    /// A single axis measured at the knuckle splays the fingertip sideways once
    /// the finger has folded, because by then it points somewhere quite
    /// different. Each joint's axis comes from its own live direction, taken
    /// after the joint above it has been placed - which is also what makes the
    /// three bends accumulate down the finger the way a real one folds.
    /// </summary>
    void Pose(int h, int f, int j, Transform next, Vector3 palm, float degrees)
    {
        Transform bone = bones[h, f, j];
        if (bone == null || !haveBind[h, f, j]) return;

        Quaternion fromClip = bone.localRotation;

        // Straight, as the model was authored.
        bone.localRotation = bind[h, f, j];

        if (Mathf.Abs(degrees) > 0.01f)
        {
            Vector3 dir = next != null
                ? next.position - bone.position
                : (bone.parent != null ? bone.position - bone.parent.position : Vector3.zero);

            if (dir.sqrMagnitude > 1e-10f)
            {
                Vector3 axis = Vector3.Cross(dir.normalized, palm);

                // Positive, always. Rotating this way about this axis moves the
                // tip toward the palm - which is what the cross product was
                // built to guarantee, and why there is no sign left to get
                // wrong.
                if (axis.sqrMagnitude > 1e-8f)
                    bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) *
                                    bone.rotation;
            }
        }

        if (authority[h] < 0.999f)
            bone.localRotation = Quaternion.Slerp(fromClip, bone.localRotation,
                                                  authority[h]);
    }
}
