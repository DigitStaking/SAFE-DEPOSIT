// ProceduralLegsIK.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ProceduralLegsIK.cs
// Goes on: PlayerModel_FBX_VISUAL  (the SAME GameObject as the Animator).
//
// ========================================================================
// PHASE 5 STEP 3 - THE FEET STOP OBEYING THE CLIP.
//
// "looks like the game still runing the animation what to do to stop
//  animation ?"
//
// This is the answer, and the answer is NOT to stop it.
//
// WHY TURNING THE ANIMATION OFF IS THE WRONG MOVE
//
// Unity's humanoid IK does not replace an animation. It BENDS the pose the
// Animator has already produced - SetIKPosition says "wherever the clip put
// this foot, put it here instead", and the solver works the knee and hip out
// from there. So there has to be a pose underneath to bend.
//
// Disable the Animator and there is nothing to bend: the model collapses into
// its bind pose and the legs freeze. Rebuild the controller with the clips
// removed and the same thing happens for the same reason. Both of those look
// like a bigger step than this one and are actually a step backwards.
//
// What stops the walk cycle MATTERING is this file. The clip still plays, and
// the feet stop listening to it - they go where ProceduralLegs decided, which
// is a real place on the real floor. Once the feet are pinned, the walk cycle
// underneath is only supplying knee bend and hip sway, and its sliding is
// gone because sliding is a property of where the feet are.
//
// Step 6 swaps the blend tree for a single neutral stance, and by then that is
// housekeeping rather than a fix: the legs already look right, and removing
// the clip only stops it fighting quietly.
//
// WHY IT LIVES ON THE MODEL AND NOT THE PLAYER ROOT
//
// Unity delivers OnAnimatorIK only to components sharing a GameObject with the
// Animator. FirstPersonHands is already here for exactly that reason and does
// exactly this for the hands - so this is the established pattern in the
// project, not a new one. Two components both implementing OnAnimatorIK on one
// object is fine; they touch different goals and never meet.
//
// The IK pass itself is already on. AnimatorBuilder sets iKPass on every layer
// and has since Phase 1, with a comment saying why - so nothing has to be
// reconfigured before this works.
//
// WHAT THIS STILL DOES NOT DO, AND IT WILL SHOW
//
// The hips do not move yet. A body whose pelvis holds a constant height while
// the feet find real ground will visibly over-stretch a leg reaching down a
// step, and will look stiff even on the flat, because a walking person's hips
// rise, fall and rock. That is step 4, and it is the step that decides whether
// this whole approach was worth doing. Do not judge the system here.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(30)]
[RequireComponent(typeof(Animator))]
public class ProceduralLegsIK : MonoBehaviour
{
    [Header("Blend")]
    [Tooltip("How much the procedural feet win over the clip. 1 = the feet go " +
             "exactly where ProceduralLegs decided. Drop toward 0 to A/B it " +
             "against the old animation without leaving Play mode - which is " +
             "the fastest way to see what this is actually changing.")]
    [Range(0f, 1f)] public float weight = 1f;

    [Tooltip("Turn the foot to match the slope it is standing on, instead of " +
             "keeping it level. This is most of what makes rubble and stairs " +
             "read as rubble and stairs.")]
    public bool tiltToSlope = true;

    [Header("Fit")]
    [Tooltip("Distance from the sole of the foot up to the ankle bone, in " +
             "metres. ProceduralLegs targets the FLOOR, and the IK goal is the " +
             "ANKLE - so without this offset the character stands with its " +
             "ankles on the ground and its feet through it.")]
    public float ankleHeight = 0.09f;

    [Header("When the legs must hand control back")]
    [Tooltip("Fade the procedural feet out while airborne. A jump has no " +
             "ground to step on, and the jump and fall clips already say what " +
             "the legs should do - pinning the feet to the last floor they saw " +
             "would leave them dangling at take-off height.")]
    public bool releaseInAir = true;

    [Tooltip("Seconds to fade in and out. Snapping the weight makes the feet " +
             "jump the instant you leave or touch the floor.")]
    public float blendTime = 0.15f;

    Animator anim;
    PlayerMotor motor;
    PlayerHealth health;

    ProceduralLegs left;
    ProceduralLegs right;

    float live;   // the weight actually applied, after fading

    void Awake()
    {
        anim = GetComponent<Animator>();

        // Found, not wired. The legs sit on the player root and this sits on
        // the model beneath it, so the search goes up - and a prefab that gets
        // rebuilt by PlayerFbxSetupTool cannot lose a reference it never had.
        motor = GetComponentInParent<PlayerMotor>();
        health = GetComponentInParent<PlayerHealth>();

        foreach (var leg in GetComponentsInParent<ProceduralLegs>(true))
        {
            if (leg.side == ProceduralLegs.Side.Left) left = leg;
            else right = leg;
        }

        if (left == null || right == null)
        {
            Debug.LogWarning("[Legs] " + name + " found " +
                             (left == null ? "no LEFT leg" : "a left leg") + " and " +
                             (right == null ? "no RIGHT leg" : "a right leg") +
                             ". Add TWO ProceduralLegs components to the player " +
                             "root, one with side = Left and one with side = " +
                             "Right. A missing side is left to the animation " +
                             "rather than forced to a guess.", this);
        }
    }

    /// <summary>
    /// Called by Unity during the animation update, after the clip has been
    /// evaluated and before the pose is committed - which is the only moment
    /// the foot position can be overruled.
    /// </summary>
    void OnAnimatorIK(int layerIndex)
    {
        // Every layer with an IK pass gets this call, and the feet only need
        // deciding once. Doing it per layer would apply the same goal two or
        // three times and waste the solve.
        if (layerIndex != 0) return;
        if (anim == null) return;

        live = Mathf.MoveTowards(live, Target(),
                                 blendTime <= 0f ? 1f : Time.deltaTime / blendTime);

        Apply(AvatarIKGoal.LeftFoot, left);
        Apply(AvatarIKGoal.RightFoot, right);
    }

    /// <summary>
    /// How much the procedural legs should be worth right now.
    ///
    /// Three cases hand control back to the clips, and all three are cases
    /// where the ground is not the thing deciding where a foot goes.
    /// </summary>
    float Target()
    {
        if (left == null && right == null) return 0f;

        // DOWNED. The kneel is a full-body pose that puts the feet somewhere
        // no walk would - underneath and behind. Pinning them to a walking
        // stance would fight it, and the player is not walking anyway.
        if (health != null && health.IsDowned) return 0f;

        // AIRBORNE. No floor to step on, and the jump and fall clips already
        // say what the legs do. Holding the feet at the last ground they saw
        // would leave them hanging at take-off height while the body rises.
        if (releaseInAir && motor != null && !motor.IsGrounded) return 0f;

        return weight;
    }

    void Apply(AvatarIKGoal goal, ProceduralLegs leg)
    {
        if (leg == null || live <= 0.001f)
        {
            // Zeroed rather than skipped. An IK weight persists across frames,
            // so a goal that is simply not written keeps whatever it had -
            // which would leave a foot welded to wherever it was standing when
            // the player left the ground.
            anim.SetIKPositionWeight(goal, 0f);
            anim.SetIKRotationWeight(goal, 0f);
            return;
        }

        // ProceduralLegs targets the FLOOR. The IK goal is the ANKLE. Without
        // the offset the character stands with its ankles on the ground and
        // its feet buried in it.
        anim.SetIKPositionWeight(goal, live);
        anim.SetIKPosition(goal, leg.FootPosition + Vector3.up * ankleHeight);

        if (!tiltToSlope)
        {
            anim.SetIKRotationWeight(goal, 0f);
            return;
        }

        // ---- THE FOOT LIES ON THE SLOPE, AND STILL POINTS FORWARD ----
        //
        // The normal alone would be enough to lie the foot flat, but it says
        // nothing about which way the toes point - so the body's forward is
        // projected onto the slope to supply that. On a ramp the foot tilts up
        // the ramp; on the flat this is exactly the body's facing, which is
        // the camera's, so nothing changes on level ground.
        Vector3 normal = leg.FootNormal;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal);

        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;

        anim.SetIKRotationWeight(goal, live);
        anim.SetIKRotation(goal, Quaternion.LookRotation(forward.normalized, normal));
    }
}
