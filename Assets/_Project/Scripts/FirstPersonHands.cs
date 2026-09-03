// FirstPersonHands.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/FirstPersonHands.cs
// Goes on: PlayerModel_FBX_VISUAL  (the SAME GameObject as the Animator).
//
// ========================================================================
// KEEPING THE HANDS IN FRAME, PEAK STYLE
//
// The problem: Mixamo animates a real person. A real person walks with their
// arms at their sides, roughly 60 degrees below eye line. In first person you
// see empty floor. Every clip you download will have this problem, forever.
//
// THREE WAYS TO SOLVE IT, AND WHY THIS ONE
//
//   1. A separate first-person arms mesh (Call of Duty). Two models, two
//      skeletons, two sets of clips - and the other three players cannot see
//      what your hands are doing, because your real body is still walking
//      normally. Wrong for a co-op game about pointing at things.
//
//   2. A permanent "arms forward" clip on a masked layer. Works, but it
//      OVERWRITES the arms completely: no walk swing, no climb reach, no
//      recoil from a trap. The character goes stiff from the chest up, and
//      you need a clip that happens to hold the arms in exactly the right
//      place.
//
//   3. INVERSE KINEMATICS. You give Unity a world position for the hand and
//      it solves the shoulder and elbow to reach it. This runs AFTER the
//      animation, on top of whatever clip is playing, so the underlying
//      motion is still there - it is bent toward the camera rather than
//      replaced. It needs no clips at all, and the arms automatically follow
//      wherever you look, because the target is defined in camera space.
//
// ========================================================================
// DECISION, 18 Aug 2026: THIS SYSTEM IS AN INTERIM. IT IS REPLACED IN BLOCK 8.
//
// Option 1 above was rejected too early. We Were Here Together - the actual
// reference for this game - DOES use a separate first-person arms mesh, and
// the objection recorded above ("the other three players cannot see what
// your hands are doing") is only true of a bad implementation. They drive
// the FP arms and the third-person body from the SAME state, so a wave
// plays on your gloves and on your body at once.
//
// The cost of one skeleton is unavoidable and is visible in play: IK pins
// the character's REAL hands 30cm from its own face, so from the outside a
// teammate sees someone walking around clutching at their own head. There
// is no weight or offset that fixes that - it is what one skeleton doing
// two jobs looks like.
//
// So handWeight is turned DOWN for now. The body animates normally, which
// is what matters while there are graybox rooms to build, and the real
// answer lands in Block 8 with the art pass:
//
//   arms mesh parented to the camera, own render layer, own FOV, own
//   Animator fed by the same parameters as the body.
//
// Do not spend more time tuning the numbers below. They are a holding
// pattern, not a design.
// ========================================================================
//
// WHY THE WEIGHT USED TO STAY AT 1
//
// It is tempting to set the IK weight below 1 so some of the walk clip's arm
// swing shows through and the hands look less rigid. That is wrong here, and
// it is the mistake I made first.
//
// IK weight is a BLEND between where the clip put the hand and where you
// asked for it. Weight 0.65 does not mean "in frame with a bit of wobble" -
// it means the hand lands two thirds of the way from your hip to the target.
// The hip is about 0.65 m below the eye and the target is 0.26 m below. Two
// thirds of the way is 0.39 m below the eye. At 75 degrees FOV and half a
// metre out, the bottom of the screen is 0.40 m below the eye.
//
// So weight 0.65 puts the hands one centimetre from falling off the screen,
// and any swing in the clip pushes them over the edge. Any weight under 1 is
// gambling with visibility.
//
// So: weight 1, position guaranteed, hands never leave the frame - plus a
// hard frustum clamp below as a safety net. If you later want them to feel
// alive, the correct place to add that is the TARGET (a small offset you
// control in metres), never the weight.
//
// WHY THE CAMERA IS NOT PARENTED TO THE HEAD BONE
//
// It is tempting, and it is a trap. Mixamo's idle has a breathing head bob
// of a few centimetres. Parent the camera to that bone and the view drifts
// constantly while standing still, which reads as motion sickness within
// about a minute. Your FirstPersonCamera already uses a fixed eyeOffset from
// the body - that is correct, keep it.
// ========================================================================

using UnityEngine;

[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(30)]
public class FirstPersonHands : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Leave empty to use this body's own eye.")]
    public Transform cameraTransform;

    [Header("Where the hands sit, in CAMERA space")]
    // WE WERE HERE FRAMING.
    //
    // These offsets are measured from the EYE, so what matters is not the
    // number itself but the WORLD height it lands on relative to the
    // shoulder. Get that wrong and the IK reaches straight out at shoulder
    // height, which is the zombie pose.
    //
    //   eye 1.55 + y -0.24  ->  hands at world 1.31
    //   shoulder is at about 1.42
    //
    // So the hands sit ~11cm BELOW the shoulder and the elbow hangs. With
    // the eye at 1.65 the same offset landed at 1.43 - dead level with the
    // shoulder - which is why the arms looked raised.
    //
    //   z 0.32  controls SIZE: nearer = bigger. Also the reach budget.
    //   x 0.26  narrow enough that the elbows stay down instead of flaring.
    //   y 0.24  below the shoulder, which is what makes the arms hang.
    //
    // Reach is only about 0.5m from the shoulder on this stocky rig, and the
    // target above already sits at 0.46m. Push x or z much further and
    // ClampToReach straightens the arm, which looks locked and wrong.
    //
    // NOTE: keepInFrame clamps y to the frustum half-height at this depth
    // (about 0.246 at z 0.32), so asking for a much lower y does nothing on
    // its own - it gets pushed straight back up. Lower the EYE instead.
    [Tooltip("x = right, y = up, z = forward, in metres from the eye. " +
             "z is SIZE: smaller = closer = bigger hands. To sit the hands " +
             "lower, drop the camera's eyeOffset, not this - the frame clamp " +
             "limits how far down y can go.")]
    public Vector3 leftHand  = new Vector3(-0.26f, -0.24f, 0.32f);
    public Vector3 rightHand = new Vector3( 0.26f, -0.24f, 0.32f);

    [Header("Visibility")]
    [Tooltip("INTERIM VALUE - see the decision note at the top of this file.\n\n" +
             "0.4, not 1. At 1 the IK pins the character's real hands to its " +
             "own face, which looks wrong to everyone except you. Lower lets " +
             "the body animate normally at the cost of the hands often sitting " +
             "out of frame. Proper first-person arms replace this in Block 8, " +
             "so do not spend time tuning it.")]
    [Range(0f, 1f)] public float handWeight = 0.4f;

    [Tooltip("Hard clamp that pins the hands inside the screen no matter what " +
             "offsets or FOV are in use. Safety net - leave on.")]
    public bool keepInFrame = true;

    [Tooltip("How far in from the screen edge to stay. 0.06 = 6%.")]
    [Range(0f, 0.4f)] public float frameMargin = 0.06f;

    [Tooltip("Release the hands during gameplay one-shots too (pick up, stow, " +
             "use). OFF keeps them locked in frame, which is the safe default: " +
             "these fire during play, when you need to see what you are doing.")]
    public bool freeArmsDuringActions = false;

    [Tooltip("How much the wrists are rotated to face the camera.")]
    [Range(0f, 1f)] public float rotationWeight = 0.7f;

    [Header("Feel")]
    [Tooltip("How fast the hands ease to a new POSE (a carry offset, an " +
             "injury). It does NOT lag them behind the camera - that is done " +
             "in camera space, so walking can never drag them.")]
    public float followSpeed = 16f;

    [Tooltip("Fade speed when the weight target changes.")]
    public float weightSpeed = 6f;

    [Header("Debug")]
    public bool drawTargets = false;

    const int ArmsLayer = 1;   // matches AnimatorBuilder's masked arms layer

    Animator anim;
    Transform cam;
    Camera camComponent;
    FirstPersonCamera fpCam;
    Vector3 lPos, rPos;        // world, for the gizmos
    Vector3 lSmooth, rSmooth;  // CAMERA space - this is what gets eased
    float weight;
    bool primed;

    // Arm reach, measured once from the actual rig. Asking IK to reach past
    // the arm's length is what produces those horrible stretched or snapping
    // elbows - so we clamp to it instead.
    float reachLeft = 0.6f, reachRight = 0.6f;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    /// <summary>
    /// LET GO OF THE HANDS ON THE WAY OUT.
    ///
    /// An IK weight PERSISTS. SetIKPositionWeight is not a per-frame
    /// instruction to the solver, it is a value the Animator keeps using
    /// until somebody changes it - so a component that simply stops running
    /// leaves the last goal it wrote in force forever.
    ///
    /// That is what "his hand still up" was: FirstPersonViewmodel switches
    /// this component off once its own arms exist, OnAnimatorIK stopped being
    /// called, and the final weight-1 goal at the camera stayed applied. The
    /// character kept both hands pinned in front of its face - the exact
    /// clutching-my-own-face pose this whole system was built to get rid of -
    /// with nothing left running to explain why.
    ///
    /// ProceduralLegsIK already carries a comment saying precisely this about
    /// feet, written before this bug was introduced. Same trap, same file
    /// author, one system later.
    ///
    /// Setting the weights here works even though OnAnimatorIK will not run
    /// again: the Animator reads the stored weights during its own IK pass,
    /// so zero is what it finds.
    /// </summary>
    void OnDisable()
    {
        if (anim == null || !anim.isHuman) return;

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);

        weight = 0f;
        primed = false;
    }

    void Start()
    {
        // The IK targets are placed relative to the EYE, so a remote body
        // wearing this would reach toward a camera that is not its own.
        // Their normal animation is the correct thing for everyone else to
        // see; the camera-locked pose is a first-person illusion and only
        // its owner is standing in the right place to be fooled by it.
        if (!PlayerRegistry.IsLocalFor(this))
        {
            enabled = false;
            return;
        }

        Bind();
        MeasureReach();
    }

    void Bind()
    {
        // cameraTransform is an INSPECTOR override, and a stale one is worse
        // than none: it survives the scene reload as a destroyed reference and
        // would be handed straight back. Unity's null overload catches that,
        // so the registry answers whenever the override is gone or dead.
        cam = cameraTransform != null ? cameraTransform : PlayerRegistry.EyeOf(this);
        camComponent = cam != null ? cam.GetComponent<Camera>() : null;
        fpCam = cam != null ? cam.GetComponent<FirstPersonCamera>() : null;
    }

    void MeasureReach()
    {
        reachLeft  = Reach(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand);
        reachRight = Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
    }

    float Reach(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
    {
        if (anim == null || !anim.isHuman) return 0.6f;
        var a = anim.GetBoneTransform(upper);
        var b = anim.GetBoneTransform(lower);
        var c = anim.GetBoneTransform(hand);
        if (a == null || b == null || c == null) return 0.6f;

        // 0.97 so the arm never fully locks straight - a completely
        // extended limb looks broken even when it is mathematically correct.
        return (Vector3.Distance(a.position, b.position) +
                Vector3.Distance(b.position, c.position)) * 0.97f;
    }

    // Called by Unity once per layer that has IK Pass enabled. AnimatorBuilder
    // turns that on; if you rebuild the controller by hand and forget, this
    // method is simply never called and nothing happens.
    void OnAnimatorIK(int layerIndex)
    {
        // Rebind if the camera went. Same reason the lamp had to: the scene
        // reload between rounds destroys the old one, and a one-shot Bind in
        // Start leaves the hands aiming at nothing from round 2 onward.
        if (cam == null) Bind();

        if (layerIndex != 0) return;          // apply once, not per layer
        if (anim == null || !anim.isHuman) return;
        if (cam == null) { Bind(); if (cam == null) return; MeasureReach(); }

        // ---- how strongly, right now ----
        //
        // The rule: DURING GAMEPLAY THE HANDS ARE LOCKED TO THE CAMERA. This
        // value is 1 while walking, running, strafing, jumping, carrying and
        // standing still - it never drifts with speed or carry state, so the
        // hands do not move when you walk.
        //
        // It only releases for things that are meant to be WATCHED rather
        // than used: emotes, and being downed. Those cases are found by
        // reading the tag on the arms layer's current state, not by
        // hard-coding state names here - so adding a fifth emote later needs
        // no change to this script.
        bool free = SafeBool("Downed");

        // Base layer carries the full-body emotes; arms layer carries the
        // gameplay one-shots. Check both. GetNextAnimatorStateInfo is checked
        // too so the hands start blending out as the emote fades IN, rather
        // than snapping once it is already halfway through.
        if (!free)
        {
            free = IsFreeState(anim.GetCurrentAnimatorStateInfo(0)) ||
                   IsFreeState(anim.GetNextAnimatorStateInfo(0));
        }

        if (!free && anim.layerCount > ArmsLayer)
        {
            free = IsFreeState(anim.GetCurrentAnimatorStateInfo(ArmsLayer)) ||
                   IsFreeState(anim.GetNextAnimatorStateInfo(ArmsLayer));
        }

        float target = free ? 0f : handWeight;
        weight = Mathf.MoveTowards(weight, target, weightSpeed * Time.deltaTime);

        if (weight <= 0.001f)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            primed = false;
            return;
        }

        // ---- where ----
        // Targets are built in CAMERA space and are CONSTANT. Nothing in this
        // block reads velocity, so walking, running and strafing all leave the
        // hands exactly where they are. Looking up lifts them and looking down
        // drops them, because the camera itself moved - no extra code.
        // ---- WHERE THE OFFSETS ARE MEASURED FROM ----
        //
        // Not from the camera's POSITION - from the character's EYE, using the
        // camera's ROTATION.
        //
        // In first person those are the same point, so it makes no difference.
        // In third person the camera is three metres behind the character, and
        // measuring from it puts the hand targets behind the character's back -
        // which is exactly the arms-wrenched-backwards pose in the screenshot.
        //
        // The hands belong in front of the FACE. The camera only ever supplies
        // the direction that face is looking.
        // ---- WHERE THE EYE IS, THIS FRAME, NOT LAST ----
        //
        // Read from FirstPersonCamera rather than the camera's transform.
        // Unity calls OnAnimatorIK during the ANIMATION update, which runs
        // BEFORE LateUpdate - and LateUpdate is where the camera moves. So
        // cam.position still holds LAST frame's value while the body has
        // already moved this frame. At 4.5 m/s that is a ~7cm backward error
        // for the whole time you are walking, which is why the hands would
        // not stay centred while moving.
        //
        // EyePosition is computed from the target's current position, so
        // there is no stale frame to inherit.
        Vector3 eyePos = fpCam != null ? fpCam.EyePosition : cam.position;
        Quaternion eyeRot = fpCam != null ? fpCam.EyeRotation : cam.rotation;

        var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
        Vector3 head = headBone != null
            ? headBone.position
            : transform.position + Vector3.up * 1.6f;

        // In first person, anchor on the eye instead: it is rock steady,
        // whereas the head bone carries the idle clip's breathing bob and would
        // make the hands jitter.
        bool thirdPerson = Vector3.Distance(eyePos, head) > 0.8f;
        Vector3 anchor = thirdPerson ? head : eyePos;

        // The frustum clamp is only meaningful when the camera is at the eye.
        // In third person it would be measuring a screen the hands are not
        // supposed to fill.
        bool clamp = keepInFrame && !thirdPerson;
        Vector3 lLocal = clamp ? ClampToFrame(leftHand)  : leftHand;
        Vector3 rLocal = clamp ? ClampToFrame(rightHand) : rightHand;

        // ---- SMOOTH IN CAMERA SPACE, NEVER IN WORLD SPACE ----
        //
        // This used to lerp the WORLD position, which meant simply walking
        // dragged the hands: the body translated, the target translated with
        // it, and the smoothed position trailed about 60ms behind. That reads
        // as the hands sliding around whenever you move.
        //
        // Easing the camera-space OFFSET instead exempts translation by
        // construction. The hands are welded to the view no matter how fast
        // you move, and the easing is left to do the job it is actually good
        // at: blending to a new POSE, such as a carry offset or an injury.
        if (!primed) { lSmooth = lLocal; rSmooth = rLocal; primed = true; }

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);   // framerate independent
        lSmooth = Vector3.Lerp(lSmooth, lLocal, t);
        rSmooth = Vector3.Lerp(rSmooth, rLocal, t);

        lPos = ClampToReach(anchor + eyeRot * lSmooth, HumanBodyBones.LeftUpperArm,  reachLeft);
        rPos = ClampToReach(anchor + eyeRot * rSmooth, HumanBodyBones.RightUpperArm, reachRight);

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand,  weight);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        anim.SetIKPosition(AvatarIKGoal.LeftHand,  lPos);
        anim.SetIKPosition(AvatarIKGoal.RightHand, rPos);

        // Palms roughly facing each other and forward, so you see the backs of
        // the hands rather than the edges. Uses the same fresh eye rotation as
        // the positions above - reading cam.forward here would reintroduce the
        // one-frame lag on the wrists only, which shows up as the hands
        // twisting slightly whenever you turn.
        Quaternion look = Quaternion.LookRotation(eyeRot * Vector3.forward, eyeRot * Vector3.up);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand,  weight * rotationWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, weight * rotationWeight);
        anim.SetIKRotation(AvatarIKGoal.LeftHand,  look * Quaternion.Euler(0f,  0f,  75f));
        anim.SetIKRotation(AvatarIKGoal.RightHand, look * Quaternion.Euler(0f,  0f, -75f));
    }

    /// <summary>
    /// Pin a camera-space point inside the visible frustum. This is the hard
    /// guarantee: whatever offsets you type in, whatever the FOV is doing, the
    /// hands cannot leave the screen.
    ///
    /// It reads the camera's CURRENT field of view rather than a stored value,
    /// so it keeps working while FirstPersonCamera widens the FOV at speed -
    /// which is exactly the moment a fixed calculation would be wrong.
    /// </summary>
    Vector3 ClampToFrame(Vector3 local)
    {
        if (camComponent == null || local.z <= 0.01f) return local;

        // Half-height of the frustum at the depth the hands sit at.
        float halfH = Mathf.Tan(camComponent.fieldOfView * 0.5f * Mathf.Deg2Rad) * local.z;
        float halfW = halfH * camComponent.aspect;

        local.x = Mathf.Clamp(local.x, -halfW * (1f - frameMargin), halfW * (1f - frameMargin));
        local.y = Mathf.Clamp(local.y, -halfH * (1f - frameMargin), halfH * (1f - frameMargin));
        return local;
    }

    Vector3 ClampToReach(Vector3 want, HumanBodyBones shoulderBone, float reach)
    {
        var shoulder = anim.GetBoneTransform(shoulderBone);
        if (shoulder == null || reach <= 0f) return want;

        Vector3 delta = want - shoulder.position;
        float d = delta.magnitude;
        if (d <= reach) return want;
        return shoulder.position + delta / d * reach;
    }

    /// <summary>
    /// True if this arms-layer state wants the IK out of its way.
    /// AnimatorBuilder tags emotes "FreeArms" and gameplay one-shots
    /// "ArmAction".
    /// </summary>
    bool IsFreeState(AnimatorStateInfo info)
    {
        if (info.IsTag("FreeArms")) return true;
        if (freeArmsDuringActions && info.IsTag("ArmAction")) return true;
        return false;
    }

    bool SafeBool(string p) { foreach (var x in anim.parameters) if (x.name == p) return anim.GetBool(p);    return false; }

    void OnDrawGizmos()
    {
        if (!drawTargets || !Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lPos, 0.04f);
        Gizmos.DrawWireSphere(rPos, 0.04f);
    }
}
