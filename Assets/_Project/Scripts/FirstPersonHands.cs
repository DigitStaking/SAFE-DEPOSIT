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
// WHY THE WEIGHT STAYS AT 1
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
    [Tooltip("Leave empty to use Camera.main.")]
    public Transform cameraTransform;

    [Header("Where the hands sit, in CAMERA space")]
    // WE WERE HERE FRAMING: wide, low and CLOSE.
    //
    // Their hands sit in the bottom corners and read big, which is what makes
    // them feel like your hands rather than a distant character's. Three
    // numbers do all the work:
    //
    //   z 0.34  close to the eye. This is the one that controls SIZE - the
    //           nearer the hand, the larger it draws. 0.52 put them at arm's
    //           length, which reads small and detached.
    //   x 0.36  wide, so they frame the view from the corners instead of
    //           meeting in the middle like a zombie.
    //   y 0.22  low enough to stay out of the way of what you are looking at.
    //
    // Do not push z below about 0.30: the arm has roughly 0.5 m of reach from
    // the shoulder, and once the target is closer than the elbow can fold the
    // IK starts folding the arm through the chest.
    [Tooltip("x = right, y = up, z = forward, in metres from the eye. " +
             "z is SIZE: smaller = closer = bigger hands. Keep z above 0.30.")]
    public Vector3 leftHand  = new Vector3(-0.36f, -0.22f, 0.34f);
    public Vector3 rightHand = new Vector3( 0.36f, -0.22f, 0.34f);

    [Header("Visibility")]
    [Tooltip("LEAVE AT 1. Anything lower blends the hands back toward the walk " +
             "clip's arm position and drops them off the bottom of the screen. " +
             "Read the header comment before touching this.")]
    [Range(0f, 1f)] public float handWeight = 1f;

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
    [Tooltip("Lag behind the camera. A little lag reads as weight; none reads " +
             "as the hands being glued to the screen.")]
    public float followSpeed = 16f;

    [Tooltip("Fade speed when the weight target changes.")]
    public float weightSpeed = 6f;

    [Header("Debug")]
    public bool drawTargets = false;

    const int ArmsLayer = 1;   // matches AnimatorBuilder's masked arms layer

    Animator anim;
    Transform cam;
    Camera camComponent;
    Vector3 lPos, rPos;
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

    void Start()
    {
        Bind();
        MeasureReach();
    }

    void Bind()
    {
        cam = cameraTransform;
        if (cam == null && Camera.main != null) { cam = Camera.main.transform; cameraTransform = cam; }
        camComponent = cam != null ? cam.GetComponent<Camera>() : null;
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
        var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
        Vector3 eye = headBone != null
            ? headBone.position
            : transform.position + Vector3.up * 1.6f;

        // In first person, anchor on the camera instead: it is rock steady,
        // whereas the head bone carries the idle clip's breathing bob and would
        // make the hands jitter.
        bool thirdPerson = Vector3.Distance(cam.position, eye) > 0.8f;
        Vector3 anchor = thirdPerson ? eye : cam.position;

        // The frustum clamp is only meaningful when the camera is at the eye.
        // In third person it would be measuring a screen the hands are not
        // supposed to fill.
        bool clamp = keepInFrame && !thirdPerson;
        Vector3 lLocal = clamp ? ClampToFrame(leftHand)  : leftHand;
        Vector3 rLocal = clamp ? ClampToFrame(rightHand) : rightHand;

        Vector3 lWant = ClampToReach(anchor + cam.rotation * lLocal, HumanBodyBones.LeftUpperArm,  reachLeft);
        Vector3 rWant = ClampToReach(anchor + cam.rotation * rLocal, HumanBodyBones.RightUpperArm, reachRight);

        if (!primed) { lPos = lWant; rPos = rWant; primed = true; }

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);   // framerate independent
        lPos = Vector3.Lerp(lPos, lWant, t);
        rPos = Vector3.Lerp(rPos, rWant, t);

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand,  weight);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        anim.SetIKPosition(AvatarIKGoal.LeftHand,  lPos);
        anim.SetIKPosition(AvatarIKGoal.RightHand, rPos);

        // Palms roughly facing each other and forward, so you see the backs of
        // the hands rather than the edges.
        Quaternion look = Quaternion.LookRotation(cam.forward, cam.up);
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
