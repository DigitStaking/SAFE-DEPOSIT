// ViewmodelArmsIK.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ViewmodelArmsIK.cs
// Goes on: the cloned arms rig, added by FirstPersonViewmodel. Never by hand.
//
// ========================================================================
// THE SHOVE, DONE THE WAY THE ONE THAT WORKS IS DONE.
//
// "you made a push animation for me before for normal body why now you can't
//  now"
//
// Correct, and it was a fair hit. PlayerPushArms exists, it works, and I did
// not reuse it here - I invented a simpler thing instead, reasoning that the
// viewmodel has no target to reach so it did not need the complicated
// version. That was wrong about WHY the original works.
//
// What makes PlayerPushArms read as a push is not the targeting. It is that
// the HANDS TRAVEL, from where they rest to an extended pose, through IK - so
// the shoulder and elbow follow and the whole arm does the gesture. My
// viewmodel version slid the entire rig forward and rotated the wrists, which
// is a camera move with a wrist flick on top, not an arm pushing.
//
// So this is the same gesture, in camera space:
//
//   sample where the hands REST while nothing is happening
//   lerp them out to an extended pose on the same curve
//   turn the palms into it as they go
//   IK the whole way, so the arms follow rather than the wrists tearing off
//
// The curve is the proven one: negative through the wind-up so the hands draw
// back first, zero at BOTH ends so the gesture starts and finishes exactly
// where the hands already are, and a longer return than throw because real
// arms are flung out and then relax.
//
// WHY IT LIVES ON THE CLONE
//
// Unity only delivers OnAnimatorIK to components sharing a GameObject with
// the Animator - the same rule FirstPersonHands, ProceduralLegsIK and
// PlayerPushArms all live by. It is added in code because the clone it
// belongs to does not exist until FirstPersonViewmodel builds it.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(40)]
[RequireComponent(typeof(Animator))]
public class ViewmodelArmsIK : MonoBehaviour
{
    // ---- set by FirstPersonViewmodel every frame ----

    /// <summary>Camera-space nudge for the left hand, metres.</summary>
    [HideInInspector] public Vector3 leftOffset;

    /// <summary>Camera-space nudge for the right hand, metres.</summary>
    [HideInInspector] public Vector3 rightOffset;

    /// <summary>Pushes both hands apart along the camera's right axis.</summary>
    [HideInInspector] public float spread;

    /// <summary>Pushes both hands forward along the camera's forward axis.</summary>
    [HideInInspector] public float reach;

    /// <summary>The viewmodel camera - the space every offset is given in,
    /// because that is the space the person tuning them looks through.</summary>
    [HideInInspector] public Transform space;

    /// <summary>How far through the shove, 0 to 1. Below zero means idle.</summary>
    [HideInInspector] public float pushProgress = -1f;

    /// <summary>Where the hands end up at full extension, in camera space.</summary>
    [HideInInspector] public Vector3 pushMove = new Vector3(0f, -0.02f, 0.25f);

    /// <summary>How far the palms rotate into the push, degrees, mirrored.</summary>
    [HideInInspector] public Vector3 pushTurn = new Vector3(0f, 0f, 55f);

    /// <summary>How far the hands draw back before the throw, metres.</summary>
    [HideInInspector] public float pushWindBack = 0.05f;

    /// <summary>How far apart the hands travel during the shove, metres.</summary>
    [HideInInspector] public float pushSpread = 0.04f;

    /// <summary>Share of the gesture spent winding back, and thrusting.</summary>
    [HideInInspector] public float windPart = 0.3f;
    [HideInInspector] public float thrustPart = 0.52f;

    Animator anim;

    // ---- WHERE THE HANDS REST ----
    //
    // Sampled off the real bones every frame nothing is happening, in CAMERA
    // space so it stays valid while the player walks and looks around.
    //
    // This is the same thing PlayerPushArms samples and for the same reason:
    // the gesture has to start from where the hands genuinely are, or there is
    // a step at the beginning of every push. Reading it live also means the
    // walk swing and idle breathing are the baseline, so a shove during a walk
    // starts from the walking pose rather than from a fixed idle one.
    Vector3 restL, restR;
    bool sampled;

    void Awake() => anim = GetComponent<Animator>();

    void LateUpdate()
    {
        if (anim == null || !anim.isHuman || space == null) return;
        if (pushProgress >= 0f) return;              // mid-shove, hold the sample

        var l = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        var r = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (l == null || r == null) return;

        restL = space.InverseTransformPoint(l.position);
        restR = space.InverseTransformPoint(r.position);
        sampled = true;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;

        bool shoving = pushProgress >= 0f && sampled && space != null;

        // Nothing asked for: write NOTHING, not even a zero weight. Nothing
        // else writes these goals, so leaving them alone is correct - and it
        // saves a pointless solve on a rig the animation is already posing
        // properly.
        if (!shoving && !Nudged()) return;

        Apply(AvatarIKGoal.LeftHand, HumanBodyBones.LeftHand, leftOffset, restL, -1f, shoving);
        Apply(AvatarIKGoal.RightHand, HumanBodyBones.RightHand, rightOffset, restR, +1f, shoving);
    }

    bool Nudged() =>
        leftOffset != Vector3.zero || rightOffset != Vector3.zero ||
        !Mathf.Approximately(spread, 0f) || !Mathf.Approximately(reach, 0f);

    void Apply(AvatarIKGoal goal, HumanBodyBones bone, Vector3 offset,
               Vector3 rest, float side, bool shoving)
    {
        var t = anim.GetBoneTransform(bone);
        if (t == null) return;

        Vector3 world;

        if (shoving)
        {
            // ---- THE HANDS TRAVEL. THIS IS THE WHOLE GESTURE. ----
            //
            // From the rest pose out to full extension and back, on the proven
            // curve, in camera space. IK carries the arm along, which is the
            // part the previous attempt was missing - it moved the rig instead
            // and the arms never actually reached.
            float k = Curve(pushProgress);

            Vector3 outTo = rest + pushMove + Vector3.right * (pushSpread * side);
            Vector3 local = Vector3.LerpUnclamped(rest, outTo, k);

            world = space.TransformPoint(local);
        }
        else
        {
            // Not shoving: just the resting nudges, added to wherever the
            // animation has the hand right now.
            world = t.position
                  + space.right * (offset.x + spread * side)
                  + space.up * offset.y
                  + space.forward * (offset.z + reach);
        }

        anim.SetIKPositionWeight(goal, 1f);
        anim.SetIKPosition(goal, world);

        if (!shoving) return;

        // Palms turn into the push and unwind out of it - rotated FROM the
        // pose the animation is holding, mirrored between the hands because a
        // left hand is a right hand reflected.
        float turn = Mathf.Clamp01(Curve(pushProgress));

        Quaternion into = Quaternion.Euler(pushTurn.x, pushTurn.y, pushTurn.z * side);

        anim.SetIKRotationWeight(goal, turn);
        anim.SetIKRotation(goal, Quaternion.Slerp(t.rotation, t.rotation * into, turn));
    }

    /// <summary>
    /// How far out the shove is: 0 at rest, negative through the wind-up, 1
    /// at full extension, back to 0 at the end.
    ///
    /// The same curve PlayerPushArms uses, because it is the one that reads
    /// correctly - out firmly, back slowly. A symmetrical curve retracts as
    /// hard as it extends and looks like a puppet being pulled.
    /// </summary>
    float Curve(float t)
    {
        float span = Mathf.Max(0.01f, pushMove.magnitude);
        float back = -Mathf.Abs(pushWindBack) / span;

        if (t < windPart)
            return Mathf.Lerp(0f, back, Smooth(t / Mathf.Max(0.001f, windPart)));

        float rest = (t - windPart) / Mathf.Max(0.001f, 1f - windPart);

        if (rest < thrustPart)
            return Mathf.Lerp(back, 1f, Smooth(rest / thrustPart));

        return Mathf.Lerp(1f, 0f, Smooth((rest - thrustPart) / (1f - thrustPart)));
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);
}
