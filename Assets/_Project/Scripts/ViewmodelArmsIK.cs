// ViewmodelArmsIK.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ViewmodelArmsIK.cs
// Goes on: the cloned arms rig, added by FirstPersonViewmodel. Never by hand.
//
// ========================================================================
// REST -> FORWARD, FLAT PALMS -> REST. NOTHING ELSE.
//
// "There must be NO teleporting or snapping when the push starts. The
//  animation must start from the exact current first-person resting hand
//  position."
//
// That is guaranteed here by construction rather than by care, and the
// distinction matters because two earlier versions were careful and still
// snapped.
//
// EVERY VALUE IS AN OFFSET FROM THE LIVE POSE:
//
//     world = wherever the animation has this hand THIS FRAME + offset(t)
//
// At t = 0 the offset is exactly zero, so the hand does not move at all on the
// first frame. It cannot snap because there is nothing to snap FROM - it is
// already where it was. And because the baseline is read live rather than
// frozen, a shove thrown mid-walk rides the walk cycle instead of cancelling
// it.
//
// ---- THE TWO WAYS THIS PREVIOUSLY SNAPPED, BOTH FIXED ----
//
// v1 lerped from a FROZEN SNAPSHOT of the rest pose taken the frame before the
// push. Anything that moved the hands during the push - the rig rising, the
// walk swing, idle breathing - fought a stale target and dragged them back.
//
// v2 was additive, but FirstPersonViewmodel counted pushing as "hands busy",
// which raised the whole rig by hiddenOffset - 45cm straight up - the instant
// G was pressed. Fixed there; a shove no longer moves the rig at all.
//
// Neither is reachable now: there is no snapshot to go stale, and no absolute
// position anywhere in this file.
//
// ---- DRIVEN BY A PUSH PROFILE ----
//
// The numbers arrive per-hand and are replaced every frame by
// FirstPersonViewmodel from whichever PushProfile the current shove resolved
// to. Editing that asset mid-push shows up on the next frame - which is the
// entire point of the Push Library, and is why nothing here is cached.
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

    // ---- the shove, from the active PushProfile ----

    /// <summary>Seconds since the shove began. Negative means idle.</summary>
    [HideInInspector] public float pushElapsed = -1f;

    [HideInInspector] public bool pushLeftUsed = true;
    [HideInInspector] public bool pushRightUsed = true;

    /// <summary>Where each hand travels to at full push, in CAMERA space,
    /// as an offset from wherever it already is.</summary>
    [HideInInspector] public Vector3 pushLeftOffset = new Vector3(0f, 0f, 0.2f);
    [HideInInspector] public Vector3 pushRightOffset = new Vector3(0f, 0f, 0.2f);

    /// <summary>Per-hand rotation at full push, additive on the live pose.</summary>
    [HideInInspector] public Vector3 pushLeftRotation;
    [HideInInspector] public Vector3 pushRightRotation;

    /// <summary>The mirrored "palms go flat" gesture, on top of the per-hand
    /// rotations above.</summary>
    [HideInInspector] public Vector3 pushPalmRotation = new Vector3(0f, 0f, 55f);

    [HideInInspector] public float pushSpread = 0.04f;
    [HideInInspector] public float pushDuration = 0.18f;
    [HideInInspector] public float pushHold = 0.08f;
    [HideInInspector] public float pushReturn = 0.3f;

    // ---- fingers, driven through HandFingerCurl if it is present ----

    [HideInInspector] public bool pushCurlFingers = true;
    [HideInInspector] public Vector4 pushLeftFingers;    // thumb, index, middle, ring
    [HideInInspector] public float pushLeftLittle;
    [HideInInspector] public Vector4 pushRightFingers;
    [HideInInspector] public float pushRightLittle;

    Animator anim;
    HandFingerCurl fingersCached;

    /// <summary>
    /// Found on demand rather than in Awake.
    ///
    /// FirstPersonViewmodel builds the clone by adding components one after
    /// another, and AddComponent runs Awake IMMEDIATELY - so this component's
    /// Awake fires before HandFingerCurl has been added, caches null, and the
    /// fingers never move again. Nothing about that failure points at
    /// construction order, which is what makes it worth avoiding rather than
    /// debugging.
    /// </summary>
    HandFingerCurl Fingers
    {
        get
        {
            if (fingersCached == null) fingersCached = GetComponent<HandFingerCurl>();
            return fingersCached;
        }
    }

    void Awake() => anim = GetComponent<Animator>();

    void OnAnimatorIK(int layerIndex)
    {
        // ---- LAYER 0 ONLY, AND THIS IS THE CAMERA HANDS ----
        //
        // The world body's arm writers had to stop filtering by layer, because
        // the third-person rig has an Arms layer that was overriding their IK.
        // That change deliberately does NOT extend here.
        //
        // These are two different rigs with two different jobs: the viewmodel
        // is the local player's own view and the model is what everybody else
        // sees. They are being worked on separately and on purpose, so a fix
        // aimed at one must not quietly alter the other.
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;

        float push = PushAmount();

        // Nothing to do: write NOTHING, not even a zero weight. Nothing else
        // writes these goals on the clone, so leaving them alone is correct -
        // and it saves a pointless solve on a rig the animation already poses.
        if (push <= 0.0001f && !Nudged())
        {
            Curl(0f);
            return;
        }

        Apply(AvatarIKGoal.LeftHand, HumanBodyBones.LeftHand,
              leftOffset, pushLeftOffset, pushLeftRotation, pushLeftUsed, -1f, push);

        Apply(AvatarIKGoal.RightHand, HumanBodyBones.RightHand,
              rightOffset, pushRightOffset, pushRightRotation, pushRightUsed, +1f, push);

        Curl(push);
    }

    bool Nudged() =>
        leftOffset != Vector3.zero || rightOffset != Vector3.zero ||
        !Mathf.Approximately(spread, 0f) || !Mathf.Approximately(reach, 0f);

    void Apply(AvatarIKGoal goal, HumanBodyBones bone, Vector3 rest,
               Vector3 pushOffset, Vector3 pushRotation, bool used,
               float side, float push)
    {
        var t = anim.GetBoneTransform(bone);
        if (t == null) return;

        // A hand this profile does not use still gets its RESTING nudge - it
        // is only excluded from the shove, not from existing.
        if (!used) pushOffset = Vector3.zero;

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
                      + right * (rest.x + spread * side
                                 + (pushOffset.x + pushSpread * side) * push)
                      + up * (rest.y + pushOffset.y * push)
                      + fwd * (rest.z + reach + pushOffset.z * push);

        anim.SetIKPositionWeight(goal, 1f);
        anim.SetIKPosition(goal, world);

        if (push <= 0.0001f || !used)
        {
            anim.SetIKRotationWeight(goal, 0f);
            return;
        }

        // Two rotations, both additive on the pose the animation is holding:
        //
        //   palmRotation   MIRRORED between the hands - the shared "palms go
        //                  flat" gesture. A left hand is a right hand
        //                  reflected, so one shared angle would turn one palm
        //                  inside out; the sign flip is what stops that.
        //
        //   pushRotation   this hand's own correction, NOT mirrored, because
        //                  the whole reason it exists is fixing one hand that
        //                  the mirrored version got wrong.
        Quaternion flat = Quaternion.Euler(pushPalmRotation.x,
                                           pushPalmRotation.y,
                                           pushPalmRotation.z * side);

        Quaternion own = Quaternion.Euler(pushRotation);

        anim.SetIKRotationWeight(goal, push);
        anim.SetIKRotation(goal,
            Quaternion.Slerp(t.rotation, t.rotation * flat * own, push));
    }

    /// <summary>
    /// Open or close the fingers with the shove.
    ///
    /// Scaled by the push amount so they open as the arms return, which means
    /// they too are zero at both ends and cannot pop. Written to zero rather
    /// than skipped when idle, because a finger curl persists exactly like an
    /// IK weight does.
    /// </summary>
    void Curl(float push)
    {
        var fingers = Fingers;
        if (fingers == null) return;

        if (!pushCurlFingers || push <= 0.0001f)
        {
            fingers.ClearAll();
            return;
        }

        fingers.SetCurl(true,
            pushLeftFingers.x * push, pushLeftFingers.y * push,
            pushLeftFingers.z * push, pushLeftFingers.w * push,
            pushLeftLittle * push);

        fingers.SetCurl(false,
            pushRightFingers.x * push, pushRightFingers.y * push,
            pushRightFingers.z * push, pushRightFingers.w * push,
            pushRightLittle * push);
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
