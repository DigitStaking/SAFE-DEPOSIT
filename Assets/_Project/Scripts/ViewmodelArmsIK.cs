// ViewmodelArmsIK.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ViewmodelArmsIK.cs
// Goes on: the cloned arms rig, added by FirstPersonViewmodel. Never by hand.
//
// ========================================================================
// PER-HAND PLACEMENT THAT DOES NOT STRETCH THE MESH.
//
// The obvious way to nudge one hand is to set its bone position. That is
// wrong, and it produced the "weird animation like stretching": a hand bone
// is a CHILD of the forearm, so moving it alone does not move the arm. It
// drags the hand away from the wrist and the skinned mesh stretches to span
// the gap - the whole limb smears rather than reaching.
//
// Moving a hand properly means IK: give the solver a world position and let
// it work out the shoulder and elbow so the ARM follows the hand there. That
// is what this does, and it is why it has to live on the clone's own
// GameObject - Unity only delivers OnAnimatorIK to components sharing an
// object with the Animator, the same rule FirstPersonHands and
// ProceduralLegsIK already live by.
//
// It is added in code by FirstPersonViewmodel rather than sitting on the
// prefab, because the thing it belongs to does not exist until that clone is
// built.
//
// WHY THE PUSH IS NOT DONE HERE
//
// The shove moves the WHOLE RIG forward instead, in FirstPersonViewmodel.
// Seen from your own eyes a push has no target to reach and no contact point
// to solve for - it is the arms going forward - and moving the rig cannot
// stretch anything, because every bone keeps its relationship to every other
// bone. IK is the right tool for "put this hand exactly there"; it is the
// wrong tool for "lean everything forward".
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(40)]
[RequireComponent(typeof(Animator))]
public class ViewmodelArmsIK : MonoBehaviour
{
    /// <summary>Camera-space nudge for the left hand, metres.</summary>
    [HideInInspector] public Vector3 leftOffset;

    /// <summary>Camera-space nudge for the right hand, metres.</summary>
    [HideInInspector] public Vector3 rightOffset;

    /// <summary>Pushes both hands apart along the camera's right axis.</summary>
    [HideInInspector] public float spread;

    /// <summary>Pushes both hands forward along the camera's forward axis.</summary>
    [HideInInspector] public float reach;

    /// <summary>The viewmodel camera, so offsets can be given in the space the
    /// person dragging them is actually looking through.</summary>
    [HideInInspector] public Transform space;

    Animator anim;

    void Awake() => anim = GetComponent<Animator>();

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;

        // Nothing asked for: write NOTHING, not even a zero weight.
        //
        // The opposite mistake to the one FirstPersonHands made. There the
        // component stopped running while its goals stayed pinned; here
        // nothing else writes these goals at all, so leaving them alone is
        // genuinely correct - and zeroing every frame would cost a pointless
        // solve on a rig where the animation is already doing the right thing.
        if (!Wanted()) return;

        Apply(AvatarIKGoal.LeftHand, HumanBodyBones.LeftHand, leftOffset, -1f);
        Apply(AvatarIKGoal.RightHand, HumanBodyBones.RightHand, rightOffset, +1f);
    }

    bool Wanted() =>
        leftOffset != Vector3.zero || rightOffset != Vector3.zero ||
        !Mathf.Approximately(spread, 0f) || !Mathf.Approximately(reach, 0f);

    void Apply(AvatarIKGoal goal, HumanBodyBones bone, Vector3 offset, float side)
    {
        var t = anim.GetBoneTransform(bone);
        if (t == null) return;

        Vector3 right = space != null ? space.right : Vector3.right;
        Vector3 up = space != null ? space.up : Vector3.up;
        Vector3 fwd = space != null ? space.forward : Vector3.forward;

        // Offsets are given in CAMERA space, because that is the space the
        // person dragging them is looking through - "further right" should
        // mean further right on screen, not along whichever way this bone's
        // own axis happens to point.
        Vector3 world = right * (offset.x + spread * side)
                      + up * offset.y
                      + fwd * (offset.z + reach);

        // FROM WHERE THE ANIMATION PUT IT, not from an absolute position. The
        // clip keeps posing the arm - walk swing, idle breathing - and this
        // only adds to the result, so the hand still animates while sitting
        // where it was asked to sit.
        anim.SetIKPositionWeight(goal, 1f);
        anim.SetIKPosition(goal, t.position + world);
    }
}
