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
    [Range(0f, 1f)] public float weight = 0.55f;

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

    [Tooltip("Hand the legs back to the clip while an emote is playing. " +
             "Emotes are FULL-BODY - a dance is mostly legs - so holding the " +
             "feet on the floor would keep the upper half dancing while the " +
             "lower half stood still. " +
             "This reads the same FreeArms tag the arm systems already " +
             "uses to release the HAND ik for the same reason, so an emote is " +
             "released by both halves off one signal and they cannot disagree.")]
    public bool releaseDuringEmotes = true;

    [Header("Hips - step 4")]
    [Tooltip("Move the pelvis. Everything else in this component places the " +
             "FEET; this is the half that makes the body above them look like " +
             "it is walking rather than being carried. " +
             "Turn it off to see what the feet alone were doing.")]
    public bool moveHips = true;

    [Tooltip("Metres the hips rise as the swinging leg passes the standing " +
             "one. A walking body is highest at that moment - the stance leg " +
             "is vertical and at full length - and lowest when both feet are " +
             "down and both legs are splayed. Without this the pelvis glides " +
             "along a rail, which is most of what reads as floating.")]
    public float hipBob = 0.035f;

    [Tooltip("How far the hips shift toward whichever foot is carrying the " +
             "weight, in metres. People walk over their standing foot, not " +
             "between their feet - it is a small number and its absence is " +
             "very visible.")]
    public float hipSway = 0.028f;

    [Tooltip("How much of a leg's length may be used before the hips drop to " +
             "help it reach. Below 1 because a straight leg looks locked - " +
             "real knees keep a bend even at full stride.")]
    [Range(0.7f, 1f)] public float maxLegExtension = 0.93f;

    [Tooltip("Metres per second the reach-drop may move. Instant would make " +
             "the whole body jolt the moment a foot lands on something lower.")]
    public float hipDropSpeed = 1.6f;

    [Tooltip("Furthest the hips may ever sink, in metres. " +
             "A deep crouch is about 0.35m and a person cannot do more without " +
             "sitting down, so anything past this is arithmetic rather than " +
             "anatomy. Without the cap a jump asked for 0.79m - the readout " +
             "said 'hip offset -78.8 cm' - and the legs went through the floor " +
             "on landing while that unwound.")]
    public float maxHipDrop = 0.35f;

    [Tooltip("Seconds the body must be continuously off the ground before the " +
             "legs are handed back to the clips. The ground SphereCast misses " +
             "for a frame here and there on uneven floors, and without this the " +
             "IK weight flickered between 1 and 0 several times a second - " +
             "half-procedural feet, which look like sliding because the clip " +
             "and the targets pull opposite ways.")]
    public float airGrace = 0.25f;

    [Tooltip("Metres the hips may be above the floor under the foot before the " +
             "body counts as airborne. Higher than a stride so that walking " +
             "over a step never reads as a jump, lower than a real jump.")]
    public float airborneHeight = 1.45f;

    float lastGrounded = -999f;

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
    /// How long this character's leg actually is, hip to ankle, measured from
    /// the skeleton rather than typed in. A model swap or a rescale cannot
    /// silently invalidate it.
    /// </summary>
    float legLength;

    float hipDrop;      // current reach-drop, eased
    float hipOffsetY;   // what the readout reports

    void Start()
    {
        if (anim == null || !anim.isHuman) return;

        var hip = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        var knee = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);

        if (hip == null || knee == null || foot == null) return;

        legLength = Vector3.Distance(hip.position, knee.position) +
                    Vector3.Distance(knee.position, foot.position);
    }

    /// <summary>
    /// Called by Unity during the animation update, after the clip has been
    /// evaluated and before the pose is committed - which is the only moment
    /// the foot position can be overruled.
    /// </summary>
    void OnAnimatorIK(int layerIndex)
    {
        // ---- LAYER 0 IS CORRECT *HERE*, AND ONLY BECAUSE OF THE MASK ----
        //
        // Every layer with an IK pass gets this call, and the feet only need
        // deciding once, so this takes the first.
        //
        // That is safe for LEGS and was not safe for ARMS. The Arms layer is
        // Override at weight 1, so it replaces the bones IN ITS MASK with its
        // own pose - and the mask contains the arms, not the legs. Leg IK
        // solved into layer 0 therefore survives untouched, while arm IK solved
        // into layer 0 was being wiped every frame by the layer above.
        //
        // So the legs working was never evidence the arms would: the two cases
        // differ by which bones the mask covers. Worth knowing before adding
        // any new IK - check what owns those bones first.
        if (layerIndex != 0) return;
        if (anim == null) return;

        live = Mathf.MoveTowards(live, Target(),
                                 blendTime <= 0f ? 1f : Time.deltaTime / blendTime);

        // BEFORE the feet, and that order is not optional. Moving the hips
        // moves everything hanging off them, so a foot goal written first
        // would be dragged along by the hip adjustment that came after it. The
        // feet are placed in WORLD space last, so wherever the hips end up,
        // the feet still land exactly where the ground says.
        ApplyHips();

        Apply(AvatarIKGoal.LeftFoot, left);
        Apply(AvatarIKGoal.RightFoot, right);
    }

    // --------------------------------------------------------------------
    // THE HIPS
    //
    // Three separate jobs, and only the first is about reach.
    // --------------------------------------------------------------------

    void ApplyHips()
    {
        if (!moveHips || legLength <= 0.001f) return;

        // ---- WHILE THE LEGS ARE RELEASED, GIVE THE DROP BACK ----
        //
        // This used to return outright when live hit zero, which FROZE
        // hipDrop at whatever it held. Jumping releases the legs mid-air, so
        // any drop taken during take-off was still being applied on the way
        // down and for half a second after landing - the legs sinking through
        // the floor.
        //
        // Unwound at the same speed it was taken, so the body rises back to
        // normal instead of snapping.
        if (live <= 0.001f)
        {
            hipDrop = Mathf.MoveTowards(hipDrop, 0f, hipDropSpeed * Time.deltaTime);
            hipOffsetY = hipDrop;
            return;
        }

        Vector3 body = anim.bodyPosition;

        // ---- 1. DROP SO THE LOWEST FOOT CAN REACH ----
        //
        // This is what the readout was reporting as THE LEG IS NOT FOLLOWING.
        // With the pelvis held at a constant height, a foot on a step below
        // simply cannot be reached, and the solver leaves the leg stretched
        // and short of its target.
        //
        // A person solves it by sinking onto the standing leg. So do we: find
        // the foot that needs the most help and lower the hips by exactly that
        // much. The whole-body dip going downstairs comes out of this for
        // free - nobody has to detect a staircase.
        float need = 0f;

        need = Mathf.Min(need, DropNeededFor(left, body));
        need = Mathf.Min(need, DropNeededFor(right, body));

        // ---- AND IT CANNOT ASK FOR MORE THAN A PERSON CAN CROUCH ----
        //
        // DropNeededFor answers "how far down would the hips have to be for
        // this foot to be reachable", and mid-jump that question has a silly
        // answer: the body is rising while the feet are still planted on the
        // floor, so it asked for most of a metre.
        //
        // A leg reaching further than a leg can reach is not a hip problem, it
        // is a foot in the wrong place - and the honest response is to leave
        // it unreached rather than to drive the pelvis into the ground chasing
        // it.
        need = Mathf.Max(need, -Mathf.Abs(maxHipDrop));

        // Eased, because landing on something lower should sink the body, not
        // jolt it.
        hipDrop = Mathf.MoveTowards(hipDrop, need, hipDropSpeed * Time.deltaTime);

        // ---- 2. RISE AS THE SWING PASSES THE STANCE LEG ----
        //
        // Highest at mid-swing, lowest at double support. Taken from whichever
        // foot is actually in the air, so it stays in step with the gait
        // without a clock of its own - which also means it slows down when the
        // player does, and stops dead when they stand still.
        float phase = Mathf.Max(Phase(left), Phase(right));
        float bob = Mathf.Sin(phase * Mathf.PI) * hipBob;

        // ---- 3. LEAN OVER THE FOOT CARRYING THE WEIGHT ----
        //
        // People walk over their standing foot, not between their feet. Small
        // number, very visible by its absence.
        Vector3 sway = Vector3.zero;
        var carrying = Weighted();

        if (carrying != null)
        {
            Vector3 over = carrying.FootPosition - body;
            over.y = 0f;
            sway = Vector3.ClampMagnitude(over, 1f) * hipSway;
        }

        hipOffsetY = hipDrop + bob;

        body += (sway + Vector3.up * hipOffsetY) * live;
        anim.bodyPosition = body;
    }

    static float Phase(ProceduralLegs leg) => leg != null ? leg.StepPhase : 0f;

    /// <summary>Whichever foot is on the floor. When both are, neither is
    /// carrying more than the other, so nothing leans.</summary>
    ProceduralLegs Weighted()
    {
        bool l = left != null && !left.IsStepping;
        bool r = right != null && !right.IsStepping;

        if (l && !r) return left;
        if (r && !l) return right;
        return null;
    }

    /// <summary>
    /// How far the hips must come down for this foot to be reachable, as a
    /// negative number, or zero if it already is.
    /// </summary>
    float DropNeededFor(ProceduralLegs leg, Vector3 body)
    {
        if (leg == null) return 0f;

        var goal = leg == left ? HumanBodyBones.LeftUpperLeg
                               : HumanBodyBones.RightUpperLeg;

        var upper = anim.GetBoneTransform(goal);
        if (upper == null) return 0f;

        Vector3 target = leg.FootPosition + Vector3.up * ankleHeight;
        float reach = legLength * maxLegExtension;
        float distance = Vector3.Distance(upper.position, target);

        return distance <= reach ? 0f : -(distance - reach);
    }

    /// <summary>
    /// How much the procedural legs should be worth right now.
    ///
    /// Three cases hand control back to the clips, and all three are cases
    /// where the ground is not the thing deciding where a foot goes.
    /// </summary>
    /// <summary>
    /// Why the weight is whatever it is. Empty when the legs are in charge.
    ///
    /// Kept as text because every one of these reasons is invisible on screen
    /// and identical to the others: the feet simply carry on playing the clip.
    /// Guessing which of three silent causes it is has already cost more time
    /// than printing it would have.
    /// </summary>
    public string HeldBack { get; private set; } = "";

    float Target()
    {
        if (left == null && right == null)
        {
            HeldBack = "no ProceduralLegs found on any parent";
            return 0f;
        }

        // DOWNED. The kneel is a full-body pose that puts the feet somewhere
        // no walk would - underneath and behind. Pinning them to a walking
        // stance would fight it, and the player is not walking anyway.
        //
        // WORTH KNOWING: IsDowned is Health <= 0, and Health comes from the
        // crew table, which is 0 until a run has actually started. So a body
        // dropped into a test scene with no run reads as DOWNED, and the legs
        // switch themselves off with no way to tell from looking.
        if (health != null && health.IsDowned)
        {
            HeldBack = "player reads as DOWNED (crew health is 0 - has a run started?)";
            return 0f;
        }

        // AIRBORNE. No floor to step on, and the jump and fall clips already
        // say what the legs do. Holding the feet at the last ground they saw
        // would leave them hanging at take-off height while the body rises.
        // ---- AND IT MUST BE AIRBORNE FOR A WHILE, NOT FOR A FRAME ----
        //
        // This is what the readout caught at IK WEIGHT 0.55. The ground check
        // is a SphereCast that misses intermittently while walking over
        // anything uneven, so the legs were handed back to the clips and taken
        // away again several times a second. Half-procedural feet are the
        // worst of both: the clip drags them one way while the targets pull
        // the other, and the result reads exactly like sliding.
        //
        // A jump lasts a good fraction of a second. A missed cast lasts a
        // frame. Requiring the airborne state to PERSIST tells them apart, and
        // costs a real jump nothing anybody can see.
        // ---- MEASURED FROM THE FLOOR, NOT ASKED OF THE MOTOR ----
        //
        // This used PlayerMotor.IsGrounded, and that answer is only true for
        // the body you own. PlayerMotor.FixedUpdate returns early for anybody
        // else, so GroundCheck never runs on a teammate, lastGroundedTime stays
        // at its -999 sentinel, and IsGrounded is FALSE for that body's entire
        // life.
        //
        // Which would have meant every teammate's legs sat at zero weight and
        // played the clip - the exact thing this system was built to replace,
        // failing only for other people, on the first two-player test.
        //
        // The legs already probe the floor themselves, on every machine, so
        // the question is answered from geometry both machines can see rather
        // than from a simulation only one of them is running. It is also
        // simply a better signal: it is the floor under THE FOOT, not under
        // the body's centre.
        bool airborne = false;

        if (releaseInAir)
        {
            var leg = right != null ? right : left;

            if (leg != null)
                airborne = !leg.HasGround || leg.HeightAboveGround > airborneHeight;
        }

        if (airborne)
        {
            if (Time.time - lastGrounded > airGrace)
            {
                HeldBack = "airborne - releaseInAir hands the legs back to the clips";
                return 0f;
            }
        }
        else
        {
            lastGrounded = Time.time;
        }

        // ---- AN EMOTE OWNS THE WHOLE BODY ----
        //
        // The dance, the wave, the salute - every one of them is a full-body
        // clip, and the two dances are mostly legs. Pinning the feet through
        // one leaves the top half performing over a pair of legs standing to
        // attention, which is worse than having no emote at all.
        //
        // Read from the SAME TAG FirstPersonHands uses to release the hand IK.
        // One signal, both halves, so the arms and the legs can never disagree
        // about whether an emote is happening - and any emote added later is
        // covered by both without touching either file.
        //
        // Next as well as current, so the release begins on the transition
        // INTO the emote rather than a fifth of a second after it starts.
        if (releaseDuringEmotes && anim.runtimeAnimatorController != null &&
            (anim.GetCurrentAnimatorStateInfo(0).IsTag("FreeArms") ||
             anim.GetNextAnimatorStateInfo(0).IsTag("FreeArms")))
        {
            HeldBack = "emote playing - the clip owns the whole body";
            return 0f;
        }

        if (weight <= 0.01f)
        {
            HeldBack = "weight on this component is 0";
            return 0f;
        }

        HeldBack = "";
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
        // The foot's OWN facing, not the body's. A planted foot does not
        // swivel when the camera turns - it stays where it was put until it
        // steps - and reading the live body yaw here made every standing foot
        // rotate under the character as if it were on ice.
        Vector3 heading = Quaternion.Euler(0f, leg.FootYaw, 0f) * Vector3.forward;

        Vector3 normal = leg.FootNormal;
        Vector3 forward = Vector3.ProjectOnPlane(heading, normal);

        if (forward.sqrMagnitude < 0.0001f) forward = heading;

        anim.SetIKRotationWeight(goal, live);
        anim.SetIKRotation(goal, Quaternion.LookRotation(forward.normalized, normal));
    }

    // --------------------------------------------------------------------
    // THE READOUT
    //
    // Built because "it still looks like sliding" and "it looks better" are
    // impossible to tell apart by eye in a dark room, and because there is one
    // failure that makes every tuning number in ProceduralLegs meaningless: if
    // this component is not actually driving the feet, the picture cannot
    // change no matter what is typed into the inspector.
    //
    // So the first line is whether it is connected at all, and everything
    // under it is the gait as the code currently understands it.
    // --------------------------------------------------------------------

    // ---- WHAT THE BONE ACTUALLY DID ----
    //
    // Everything above is what the code ASKED for. This is what the skeleton
    // did about it, sampled in LateUpdate once the animation has been applied
    // and the solver has run.
    //
    // The gap between the two is the whole diagnosis, and it separates the two
    // failures that look identical on screen:
    //
    //   target barely rises   -> the arc is wrong, tune ProceduralLegs
    //   target rises, bone does not follow -> the leg cannot reach, which no
    //                            amount of tuning fixes, and means the hips
    //                            have to move (step 4)
    //
    // Three rounds of tuning went into the first when it might have been the
    // second, because from outside they are the same picture.

// ---- THE ON-SCREEN READOUT IS GONE ----
    //
    // It printed IK WEIGHT, step length, asked lift, bone lift, reach error
    // and the planted/stepping state, and it earned its keep: "asked lift 22cm
    // / bone lift 21cm" is what ended several rounds of guessing about whether
    // the goal or the solve was wrong.
    //
    // Removed rather than switched off, because switching it off did not work
    // TWICE and both failures are worth recording:
    //
    //   #if UNITY_EDITOR is TRUE in play mode. It strips a build, not the
    //   Editor, so it did nothing for anybody testing in the Editor - which is
    //   everybody, all the time.
    //
    //   Changing the default to false changed nothing either, because
    //   showReadout was already serialized as 1 on the Player prefab. A script
    //   default only applies to objects created after it changes; existing
    //   assets keep what they were saved with. This project has now been
    //   caught by that twice.
    //
    // Deleting the code is the only version that cannot come back. The
    // orphaned showReadout: 1 left in the prefab is harmless and Unity drops
    // it the next time the prefab is written.
}
