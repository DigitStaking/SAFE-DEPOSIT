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
// So the axis is MEASURED, not assumed:
//
//   the flexion axis is across the knuckles, index -> little
//   the correct SIGN is the one that moves the fingertip TOWARD the wrist,
//   because that is what closing a hand does
//
// Both are worked out once, from the rig's own geometry, by trying a test
// rotation and keeping whichever sign shortened the tip-to-wrist distance.
// Nothing to tune, nothing to get wrong on a new model, and it survives the
// rig being replaced.
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

    // Measured per hand, once.
    Vector3[] axisLocal = new Vector3[2];   // in the HAND bone's space, so it
                                            // stays correct as the arm moves
    float[] sign = new float[2];

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
            across = (middleP.position - indexP.position) * 3f;   // rough, but a direction
        else
            return false;

        if (across.sqrMagnitude < 1e-8f) return false;
        across.Normalize();

        // Pick a finger with a real tip to test against - middle if we have
        // it, index otherwise.
        Transform tip = Pick(bones[h, 2, 2], bones[h, 2, 1],
                             bones[h, 1, 2], bones[h, 1, 1]);
        Transform root = Pick(bones[h, 2, 0], bones[h, 1, 0]);
        if (tip == null || root == null) return false;

        // Rotate the tip 20 degrees each way about the axis, pivoting at the
        // knuckle, and keep the sign that ends up closer to the wrist.
        Vector3 rel = tip.position - root.position;
        float plus = (root.position + Quaternion.AngleAxis(20f, across) * rel
                      - hand.position).sqrMagnitude;
        float minus = (root.position + Quaternion.AngleAxis(-20f, across) * rel
                       - hand.position).sqrMagnitude;

        sign[h] = plus < minus ? 1f : -1f;

        // Stored in the HAND'S space. The arm swings constantly, so a world
        // axis measured at startup would be wrong by the second frame; the
        // hand-local one stays true wherever the arm goes.
        axisLocal[h] = hand.InverseTransformDirection(across);

        if (logCalibration)
            Debug.Log("[Fingers] " + (h == 0 ? "Left" : "Right") +
                      " axis(local)=" + axisLocal[h].ToString("F3") +
                      " sign=" + sign[h]);

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
            Vector3 axis = hands[h].TransformDirection(axisLocal[h]).normalized;
            if (axis.sqrMagnitude < 1e-6f) continue;

            for (int f = 0; f < 5; f++)
            {
                if (live[f] <= 0.001f) continue;

                float scale = (f == 0 ? thumbScale : 1f) * live[f] * sign[h];

                Bend(bones[h, f, 0], axis, proximalDegrees * scale);
                Bend(bones[h, f, 1], axis, intermediateDegrees * scale);
                Bend(bones[h, f, 2], axis, distalDegrees * scale);
            }
        }
    }

    /// <summary>
    /// Rotate one joint about the world flexion axis.
    ///
    /// Order matters and it is why this reads the bone's rotation live rather
    /// than from a cache: bending the knuckle drags the whole finger with it,
    /// so by the time the middle joint is reached its rotation already
    /// includes the knuckle's bend. Reading it fresh makes the three bends
    /// accumulate down the finger the way a real one folds.
    /// </summary>
    static void Bend(Transform bone, Vector3 axis, float degrees)
    {
        if (bone == null || Mathf.Abs(degrees) < 0.01f) return;
        bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}
