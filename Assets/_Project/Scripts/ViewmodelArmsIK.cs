// ViewmodelArmsIK.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ViewmodelArmsIK.cs
// Goes on: the cloned arms rig, added by FirstPersonViewmodel. Never by hand.
//
// ========================================================================
// REST -> FORWARD, FLAT PALMS -> REST. NOTHING ELSE.
//
// "when I trigger PUSH, the hands suddenly teleport/jump upward before the
//  push animation starts... the push animation must start exactly from the
//  current/default first-person hand position"
//
// TWO separate causes were producing that, and only one of them was in this
// file.
//
// The first was in FirstPersonViewmodel: pushing counted as "hands busy",
// which raised the whole rig by hiddenOffset - 45cm straight up - the instant
// G was pressed. That is fixed there; a shove no longer moves the rig at all.
//
// The second was here, and it would have survived that fix. The gesture used
// to lerp from a FROZEN SNAPSHOT of the rest pose, taken the frame before the
// push began. Anything that moved the hands during the push - the rig
// rising, the walk cycle swinging, the idle breathing - was then fighting a
// stale target, and the hands were dragged back to where they used to be.
//
// So the gesture is ADDITIVE now. It is an offset from wherever the animation
// has the hand THIS frame, and that offset is zero at the start:
//
//     world = live bone position + offset(elapsed)
//
// At elapsed 0 the offset is exactly zero, so the hand does not move at all on
// the first frame. It cannot snap, because there is nothing to snap FROM - it
// is already where it was. And because the baseline is read live rather than
// frozen, a shove thrown mid-walk rides the walk cycle instead of cancelling
// it.
//
// The shape is the plain one that was asked for, in real seconds rather than
// fractions of a mystery duration:
//
//     pushDuration   reach out and turn the palms flat
//     pushHold       stay there
//     pushReturn     ease back, longer than the reach, because arms are
//                    thrown out and then relax
//
// No wind-up, no draw-back. Those were my additions and they are gone.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(40)]
[RequireComponent(typeof(Animator))]
public class ViewmodelArmsIK : MonoBehaviour
{
    // ---- resting nudges, set by FirstPersonViewmodel every frame ----
    [HideInInspector] public Vector3 leftOffset;
    [HideInInspector] public Vector3 rightOffset;
    [HideInInspector] public float spread;
    [HideInInspector] public float reach;

    /// <summary>The viewmodel camera - the space every offset is given in,
    /// because that is the space the person tuning them looks through.</summary>
    [HideInInspector] public Transform space;

    // ---- the shove ----

    /// <summary>Seconds since the shove began. Negative means idle.</summary>
    [HideInInspector] public float pushElapsed = -1f;

    [HideInInspector] public float pushForward = 0.2f;
    [HideInInspector] public float pushDuration = 0.18f;
    [HideInInspector] public float pushHold = 0.08f;
    [HideInInspector] public float pushReturn = 0.3f;
    [HideInInspector] public float pushSpread = 0.04f;
    [HideInInspector] public Vector3 pushHandRotation = new Vector3(0f, 0f, 55f);

    Animator anim;

    void Awake() => anim = GetComponent<Animator>();

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;

        float push = PushAmount();

        // Nothing to do: write NOTHING, not even a zero weight. Nothing else
        // writes these goals, so leaving them alone is correct - and it saves
        // a pointless solve on a rig the animation is already posing properly.
        if (push <= 0.0001f && !Nudged()) return;

        Apply(AvatarIKGoal.LeftHand, HumanBodyBones.LeftHand, leftOffset, -1f, push);
        Apply(AvatarIKGoal.RightHand, HumanBodyBones.RightHand, rightOffset, +1f, push);
    }

    bool Nudged() =>
        leftOffset != Vector3.zero || rightOffset != Vector3.zero ||
        !Mathf.Approximately(spread, 0f) || !Mathf.Approximately(reach, 0f);

    void Apply(AvatarIKGoal goal, HumanBodyBones bone, Vector3 offset,
               float side, float push)
    {
        var t = anim.GetBoneTransform(bone);
        if (t == null) return;

        Vector3 right = space != null ? space.right : Vector3.right;
        Vector3 up = space != null ? space.up : Vector3.up;
        Vector3 fwd = space != null ? space.forward : Vector3.forward;

        // ---- EVERYTHING IS AN OFFSET FROM THE LIVE POSE ----
        //
        // t.position is where the ANIMATION has this hand this frame. Adding to
        // it means the hand starts exactly where it already was and the clip
        // keeps driving underneath - walk swing, idle breathing - rather than
        // being replaced by a pose of our own.
        Vector3 world = t.position
                      + right * (offset.x + spread * side + pushSpread * side * push)
                      + up * offset.y
                      + fwd * (offset.z + reach + pushForward * push);

        anim.SetIKPositionWeight(goal, 1f);
        anim.SetIKPosition(goal, world);

        if (push <= 0.0001f) return;

        // Palms go flat INTO the push and unwind out of it, rotated from the
        // pose the animation is holding. Mirrored between the hands, because a
        // left hand is a right hand reflected and one shared angle would put a
        // palm inside out.
        Quaternion flat = Quaternion.Euler(pushHandRotation.x,
                                           pushHandRotation.y,
                                           pushHandRotation.z * side);

        anim.SetIKRotationWeight(goal, push);
        anim.SetIKRotation(goal, Quaternion.Slerp(t.rotation, t.rotation * flat, push));
    }

    /// <summary>
    /// How far into the push, 0 to 1, in REAL SECONDS.
    ///
    /// Zero at the start and zero at the end, which is the whole contract: the
    /// hands begin and finish exactly where the animation already had them, so
    /// there is nothing to snap to at either edge.
    /// </summary>
    float PushAmount()
    {
        if (pushElapsed < 0f) return 0f;

        float outT = Mathf.Max(0.01f, pushDuration);
        float hold = Mathf.Max(0f, pushHold);
        float back = Mathf.Max(0.01f, pushReturn);

        if (pushElapsed < outT)
            return Smooth(pushElapsed / outT);

        if (pushElapsed < outT + hold)
            return 1f;

        float r = (pushElapsed - outT - hold) / back;
        return r >= 1f ? 0f : 1f - Smooth(r);
    }

    /// <summary>Total length of the gesture, so the caller knows when it ends.</summary>
    public float TotalLength =>
        Mathf.Max(0.01f, pushDuration) + Mathf.Max(0f, pushHold) + Mathf.Max(0.01f, pushReturn);

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
