// HandFingerCurl.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/HandFingerCurl.cs
// Goes on: PlayerModel_FBX_VISUAL  (the SAME GameObject as the Animator).
//
// ========================================================================
// FINGERS THAT ACTUALLY CLOSE AROUND THINGS.
//
// "we don't grab boxes like that you grab them with fingers"
// "can you add something for the hand to control the grip like of each hand"
// "like the fingers"
//
// Putting the hand in the right place was only ever half of it. An open, flat
// hand parked against a crate reads as PUSHING it. What makes it read as
// holding is the fingers closing - and until now nothing in this project
// touched a finger bone at all, so every grip was a flat hand no matter where
// it was placed.
//
// This closes them. One number per finger, 0 straight to 1 fully curled,
// applied to the real bones after the animation has run.
//
// ---- WHY NOT A GRIP POSE CLIP ----
//
// Same reason the hand placement is not a clip. A pose is authored for one
// grip; this game's loot goes from a can to a filing cabinet to an
// unconscious teammate. Numbers blend, interpolate between items, and can be
// dragged in the Inspector while the game runs. A pose cannot.
//
// ---- THE PART THAT IS NOT OBVIOUS: WHICH WAY IS "CLOSED" ----
//
// A finger bone's local axes are whatever the person who rigged the model
// felt like. Hard-coding "rotate around local Z" works on exactly one rig and
// silently produces fingers bending sideways or backwards on the next one -
// and backwards fingers are the kind of thing you notice in a screenshot
// three weeks later.
//
// So the axis is MEASURED, not assumed. It is built from the PALM:
//
//   palm normal  =  across the knuckles  x  along the fingers
//   bend axis    =  along this joint     x  palm normal
//
// and rotating a positive amount about that axis always moves the fingertip
// toward the palm, which is the definition of closing a hand.
//
// ---- THE FIRST VERSION OF THIS WAS WRONG, AND WORTH KEEPING WRITTEN DOWN ----
//
// It found the sign by rotating a fingertip 20 degrees each way and keeping
// whichever ended up closer to the WRIST. That reasoning sounds fine and is
// geometrically degenerate: the bend axis is perpendicular to the
// wrist-to-knuckle line, so both directions land almost exactly the same
// distance from the wrist. The comparison was decided by floating-point noise
// - which is exactly why it came out right on one hand and wrong on the
// other, and why it looked like a left/right bug rather than a maths bug.
//
// The palm normal has no such degeneracy. Its only subtlety is that a left
// hand is a right hand reflected, so the cross product lands on the BACK of
// one and the PALM of the other - handled by one sign flip, which is a fact
// about mirrored geometry rather than a fact about this particular rig.
//
// ---- RUNS IN LateUpdate, ON PURPOSE ----
//
// The Animator writes the whole skeleton during its own update, and IK goals
// are solved inside that. Anything written before then is overwritten. This
// runs after all of it, so it layers on top of whatever pose the walk cycle
// and the hand IK have already agreed on, instead of fighting them.
// ========================================================================

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
    [Tooltip("Seconds to close and open. Snapping the fingers shut the frame " +
             "a pickup completes looks like the hand glitched.")]
    public float blendTime = 0.14f;

    [Header("Debug")]
    [Tooltip("Log the measured bend axis and sign for each hand once, at " +
             "startup. Worth a look the first time, and after any rig change.")]
    public bool logCalibration = false;

    Animator anim;

    // The five curls asked for, per hand, and the eased values actually used.
    readonly float[] wantL = new float[5];
    readonly float[] wantR = new float[5];
    readonly float[] liveL = new float[5];
    readonly float[] liveR = new float[5];

    // Bones, cached: [hand][finger][joint]
    Transform[,,] bones = new Transform[2, 5, 3];
    Transform[] hands = new Transform[2];
    bool[] ready = new bool[2];

    // Measured per hand, once: which way the palm faces, in the HAND bone's
    // own space so it stays correct wherever the arm swings.
    Vector3[] palmLocal = new Vector3[2];

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

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null || !anim.isHuman) return;

        Cache();
    }

    /// <summary>
    /// Find every finger bone once. The humanoid enum orders each finger's
    /// three joints consecutively - Proximal, Intermediate, Distal - so the
    /// two children are just the next two values along, which saves naming
    /// thirty bones by hand.
    /// </summary>
    void Cache()
    {
        hands[0] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        hands[1] = anim.GetBoneTransform(HumanBodyBones.RightHand);

        for (int h = 0; h < 2; h++)
        {
            int found = 0;

            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                {
                    var b = anim.GetBoneTransform((HumanBodyBones)((int)Map[h, f] + j));
                    bones[h, f, j] = b;
                    if (b != null) found++;
                }

            // Needs the hand and at least the index and middle knuckles: the
            // axis is measured from across the knuckles, so one finger is not
            // enough to measure anything.
            ready[h] = hands[h] != null && found >= 6 && Calibrate(h);

            if (!ready[h] && logCalibration)
                Debug.LogWarning("[Fingers] " + (h == 0 ? "Left" : "Right") +
                                 " hand has no usable finger bones (" + found +
                                 "/15). The avatar probably has fingers unmapped.");
        }
    }

    /// <summary>
    /// Work out which way this rig's fingers close, from the rig itself.
    ///
    /// The axis is across the knuckles. The sign is whichever direction moves
    /// a fingertip toward the wrist, because that is the definition of closing
    /// a hand and it is true of every hand ever rigged.
    /// </summary>
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
            across = middleP.position - indexP.position;   // rough, but a direction
        else
            return false;

        Transform anyKnuckle = Pick(middleP, indexP, littleP);
        if (anyKnuckle == null) return false;

        Vector3 alongFingers = anyKnuckle.position - hand.position;

        if (across.sqrMagnitude < 1e-10f || alongFingers.sqrMagnitude < 1e-10f)
            return false;

        // Perpendicular to the plane of the hand. Which FACE of the hand it
        // lands on depends only on handedness - a left hand is a right hand
        // reflected - so one flip covers it, on every rig, forever.
        Vector3 palm = Vector3.Cross(across.normalized, alongFingers.normalized);
        if (palm.sqrMagnitude < 1e-10f) return false;

        if (h == 0) palm = -palm;          // left hand: the cross lands on the back

        palmLocal[h] = hand.InverseTransformDirection(palm.normalized);

        if (logCalibration)
            Debug.Log("[Fingers] " + (h == 0 ? "Left" : "Right") +
                      " palm(local)=" + palmLocal[h].ToString("F3"));

        return true;
    }

    /// <summary>First of these that actually exists. Written out rather than
    /// using ?? because that operator bypasses Unity's overloaded null check
    /// and will happily hand back a destroyed object.</summary>
    static Transform Pick(params Transform[] options)
    {
        for (int i = 0; i < options.Length; i++)
            if (options[i] != null) return options[i];
        return null;
    }

    // ------------------------------------------------------------------
    // ASKED FOR BY WHOEVER IS HOLDING SOMETHING
    // ------------------------------------------------------------------

    /// <summary>Close one hand. Five values, 0 straight to 1 fully curled.</summary>
    public void SetCurl(bool leftHand, float thumb, float index, float middle,
                        float ring, float little)
    {
        float[] w = leftHand ? wantL : wantR;
        w[0] = thumb; w[1] = index; w[2] = middle; w[3] = ring; w[4] = little;
    }

    /// <summary>Open one hand again. Called on release - and it must be
    /// CALLED, not just left alone, for the same reason IK weights have to be
    /// written to zero: nothing else resets these.</summary>
    public void ClearCurl(bool leftHand)
    {
        float[] w = leftHand ? wantL : wantR;
        for (int i = 0; i < 5; i++) w[i] = 0f;
    }

    /// <summary>Open both hands.</summary>
    public void ClearAll()
    {
        ClearCurl(true);
        ClearCurl(false);
    }

    // ------------------------------------------------------------------

    void LateUpdate()
    {
        float step = blendTime <= 0f ? 1f : Time.deltaTime / blendTime;

        for (int h = 0; h < 2; h++)
        {
            float[] want = h == 0 ? wantL : wantR;
            float[] live = h == 0 ? liveL : liveR;

            bool any = false;

            for (int f = 0; f < 5; f++)
            {
                live[f] = Mathf.MoveTowards(live[f], Mathf.Clamp01(want[f]), step);
                if (live[f] > 0.001f) any = true;
            }

            if (!any || !ready[h]) continue;

            // Back into world space from the hand's CURRENT orientation, so
            // the fingers close correctly whatever the arm is doing.
            Vector3 palm = hands[h].TransformDirection(palmLocal[h]);
            if (palm.sqrMagnitude < 1e-8f) continue;
            palm.Normalize();

            for (int f = 0; f < 5; f++)
            {
                if (live[f] <= 0.001f) continue;

                float scale = (f == 0 ? thumbScale : 1f) * live[f];

                Bend(bones[h, f, 0], bones[h, f, 1], palm, proximalDegrees * scale);
                Bend(bones[h, f, 1], bones[h, f, 2], palm, intermediateDegrees * scale);
                Bend(bones[h, f, 2], null, palm, distalDegrees * scale);
            }
        }
    }

    /// <summary>
    /// Fold one joint toward the palm.
    ///
    /// The axis is built PER JOINT from that joint's own live direction, so it
    /// stays correct as the finger folds - by the time the fingertip joint is
    /// reached it is pointing somewhere quite different from where it started,
    /// and a single axis measured at the knuckle would splay it sideways.
    ///
    /// Reading the rotation live is what makes the three bends accumulate down
    /// the finger the way a real one folds: bending the knuckle drags the whole
    /// finger with it, so the middle joint's rotation already includes it.
    /// </summary>
    static void Bend(Transform bone, Transform next, Vector3 palm, float degrees)
    {
        if (bone == null || Mathf.Abs(degrees) < 0.01f) return;

        // Which way this joint points. The fingertip joint has no child bone,
        // so it borrows its direction from the joint above it.
        Vector3 dir = next != null
            ? next.position - bone.position
            : (bone.parent != null ? bone.position - bone.parent.position : Vector3.zero);

        if (dir.sqrMagnitude < 1e-10f) return;

        Vector3 axis = Vector3.Cross(dir.normalized, palm);
        if (axis.sqrMagnitude < 1e-8f) return;

        // Positive, always. Rotating this way about this axis moves the tip
        // toward the palm - that is what the cross product was built to
        // guarantee, and it is why there is no sign to get wrong any more.
        bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
    }
}
