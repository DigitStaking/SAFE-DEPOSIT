// PlayerPushArms.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerPushArms.cs
// Goes on: PlayerModel_FBX_VISUAL  (the SAME GameObject as the Animator).
//
// ========================================================================
// PHASE 5 - THE SHOVE YOU CAN SEE.
//
// "i need a push animation with hands"
//
// Built the same way the legs were, and for the same reason: there is no push
// clip in Assets/_Project/Models, and a downloaded one would still be wrong.
// A Mixamo shove is authored for one distance, at one height, with the arms
// arriving wherever the animator decided - while a real shove has to reach the
// person actually in front of you, who might be a step away or at arm's
// length, on a ramp, or shorter than you.
//
// Two hands, forward, at whatever the target actually is. That is IK's job.
//
// THE SWING HAS THREE PARTS AND THEY ARE NOT THE SAME LENGTH
//
//   wind up   the hands draw back and in. Short - a shove is not a haymaker
//   thrust    fast, and this is where the impulse lands
//   recover   slowest. Arms fall back rather than snapping, which is what
//             stops it reading as a twitch
//
// The contact moment is PlayerPush.contactAt, so the hit and the hands cannot
// drift apart: one number moves both.
//
// WHY IT DOES NOT FIGHT FirstPersonHands
//
// That component owns the hands during normal play and holds them locked to
// the camera - but it returns early for anybody who is not the local player,
// so on a TEAMMATE'S body nothing is holding the arms at all.
//
// On your own body both want the hands, so this runs at execution order 40,
// after its 30. Unity calls OnAnimatorIK in execution order and the last
// writer wins, so during a shove this quietly overrules it and hands them back
// the moment the swing ends. Nothing had to be modified in that file.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(40)]          // after PlayerCarryArms (35), so a shove wins
[RequireComponent(typeof(Animator))]
public class PlayerPushArms : MonoBehaviour
{
    [Header("Reach")]
    [Tooltip("How far in front of the chest the hands end up at full thrust, " +
             "in metres. Should be close to PlayerPush.range so the hands " +
             "arrive where the shove actually lands.")]
    public float reach = 0.72f;

    [Tooltip("How far apart the two hands are, in metres. Shoulder width - " +
             "both palms on a chest.")]
    public float spread = 0.24f;

    [Tooltip("Height of the shove above the body's origin, in metres. Chest " +
             "height on the person being pushed, not on you.")]
    public float height = 1.28f;

    [Header("The wind up")]
    [Tooltip("How far the hands draw BACK before the thrust, in metres. Small " +
             "- a shove is not a haymaker, and a big wind-up telegraphs it so " +
             "clearly that nobody would ever be caught by one.")]
    public float windBack = 0.16f;

    [Tooltip("How much of the swing is wind-up, 0 to 1. The rest is thrust " +
             "and recovery.")]
    [Range(0.05f, 0.5f)] public float windPart = 0.3f;

    [Header("Blend")]
    [Tooltip("How strongly the hands are pulled to the shove. 1 takes them " +
             "over completely for the length of the swing.")]
    [Range(0f, 1f)] public float weight = 1f;

    [Tooltip("How much of the swing after the wind-up is the thrust itself, " +
             "0 to 1. The rest is the recovery. Larger means a longer, more " +
             "deliberate push; smaller means a snap.")]
    [Range(0.15f, 0.8f)] public float thrustPart = 0.52f;

    [Header("Aim")]
    [Tooltip("Follow where the player is LOOKING, up and down, instead of " +
             "shoving flat out of the chest. The body only carries yaw - it is " +
             "welded to the camera horizontally and knows nothing about pitch - " +
             "so without this the hands ignore the camera entirely whenever you " +
             "look up or down.")]
    public bool followCamera = true;

    [Tooltip("Degrees of look pitch the hands will follow, up or down. Clamped " +
             "because the IMPULSE stays roughly level on purpose - a shove is " +
             "for moving people sideways, not stapling them to the floor - and " +
             "hands that dived at your boots while the push went flat would " +
             "just look wrong.")]
    [Range(0f, 80f)] public float pitchLimit = 38f;

    [Header("Palm")]
    [Tooltip("Rotation of the RIGHT hand relative to the direction of the " +
             "shove. The left hand mirrors it on Z. " +
             "This exists because a hand bone has no standard orientation - " +
             "which axis runs along the fingers is a decision the rig made, " +
             "not something that can be derived. The old hand system solved the " +
             "same problem the same way with its own Euler(0,0,+-75), and this " +
             "is the number to change if the palms arrive edge-on like a " +
             "karate chop instead of flat like a shove.")]
    public Vector3 palmEuler = new Vector3(0f, 0f, 90f);

    [Header("Camera clearance")]
    [Tooltip("Closest the hands may come to the EYE, in metres. " +
             "The wind-up pulls the hands BACKWARD past their rest pose on " +
             "purpose - LerpUnclamped with a negative k, so a real draw-back " +
             "actually happens rather than just slowing down as it approaches " +
             "rest. In first person 'backward' means 'toward your own face', " +
             "and a fist a few centimetres from the lens fills the screen and " +
             "clips through the near plane. This is the floor under that: " +
             "never closer to the eye than this, whatever the curve asks for. " +
             "In third person the eye is metres away and the clamp never " +
             "engages, so it costs nothing there.")]
    public float minEyeDistance = 0.38f;

    Animator anim;
    PlayerPush push;
    Transform body;
    PlayerMotor motor;

    // ---- WHERE THE HANDS ACTUALLY ARE WHEN NOBODY IS PUSHING ----
    //
    // Sampled from the real bones every frame the arms are idle, in the body's
    // frame so it stays valid as the player walks and turns.
    //
    // This is what fixes the teleport. FirstPersonHands holds the hands at 40%
    // toward its own camera-locked targets, and the old code faded MY weight
    // up from zero - which does not blend between the two poses at all, it
    // blends toward the raw CLIP pose on the way in and back to it on the way
    // out. So the hands jumped to wherever the animation had them, travelled,
    // and jumped again coming back.
    //
    // Blending the POSITION instead, from wherever the hands genuinely are,
    // means there is nothing to jump to: at both ends of the swing the target
    // IS the rest pose, so the arms leave from and return to exactly where
    // they were.
    Vector3 restLocalL, restLocalR;
    Quaternion restRotL, restRotR;
    bool sampled;

    /// <summary>True while this component is the one holding the hand IK
    /// goals, so they are released exactly once when a shove ends rather
    /// than re-zeroed every idle frame.</summary>
    bool heldGoals;

    // fpHands is gone. This component used to hold a reference to
    // FirstPersonHands so it could decide whether to release its own IK goals,
    // and that coupling is exactly what went stale. It releases what it wrote,
    // unconditionally, and needs to know about nobody.

    void LateUpdate()
    {
        if (anim == null || !anim.isHuman || body == null) return;
        if (push != null && push.PushProgress >= 0f) return;   // mid-swing, hold

        var l = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        var r = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (l == null || r == null) return;

        restLocalL = body.InverseTransformPoint(l.position);
        restLocalR = body.InverseTransformPoint(r.position);
        restRotL = Quaternion.Inverse(body.rotation) * l.rotation;
        restRotR = Quaternion.Inverse(body.rotation) * r.rotation;
        sampled = true;
    }

    void Awake()
    {
        anim = GetComponent<Animator>();

        // Found upward, like everything else on this model: the push lives on
        // the player root and this lives on the model beneath it.
        push = GetComponentInParent<PlayerPush>();
        body = push != null ? push.transform : transform;
        motor = GetComponentInParent<PlayerMotor>();
    }

    /// <summary>
    /// Which way this shove is aimed, pitch included.
    ///
    /// The body is welded to the camera in YAW and knows nothing about pitch,
    /// so shoving out of the body frame ignores the camera the moment anybody
    /// looks up or down.
    ///
    /// Locally the eye is the truth. On a TEAMMATE'S screen there is no eye to
    /// read, so it comes from the LookYaw and LookPitch already on the wire -
    /// the pair PlayerHeadlamp uses to point a crewmate's beam. That is the
    /// second customer for those two values, which is why they were worth
    /// replicating rather than deriving from the body.
    /// </summary>
    Quaternion Aim()
    {
        if (!followCamera) return Quaternion.LookRotation(body.forward, Vector3.up);

        float yaw = body.eulerAngles.y;
        float pitch = 0f;

        bool mine = motor != null && motor.IsLocal;

        if (mine && motor.Eye != null)
        {
            Vector3 e = motor.Eye.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }
        else if (motor != null)
        {
            var row = CrewMemberNet.ForSlot(motor.Slot);

            if (row != null)
            {
                yaw = row.LookYaw.Value;
                pitch = row.LookPitch.Value;
            }
        }

        // Euler angles arrive as 0..360, so a 20 degree look DOWN reads as 340
        // and would clamp to the limit instead of to a fifth of it.
        if (pitch > 180f) pitch -= 360f;

        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

        return Quaternion.Euler(pitch, yaw, 0f);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || anim == null || !anim.isHuman) return;
        if (push == null) return;

        float t = push.PushProgress;

        // ---- NOT SWINGING: ALWAYS RELEASE WHAT THIS COMPONENT WROTE ----
        //
        // This has been wrong twice, in opposite directions, and both versions
        // were wrong for the same underlying reason - they tried to reason
        // about who ELSE was driving the hands.
        //
        // v1 wrote nothing, assuming FirstPersonHands had already set them.
        // v2 asked whether FirstPersonHands was still enabled, because the
        // viewmodel switches it off at runtime.
        //
        // The v2 question stopped meaning what it said the moment
        // PlayerCarryArms arrived at execution order 35. FirstPersonHands runs
        // at 30, so its output is overwritten before it reaches the screen -
        // "enabled" became true-but-irrelevant, which is worse than false,
        // because it is a confident answer to the wrong question.
        //
        // The rule that does not rot: RELEASE WHAT YOU WROTE, ALWAYS. An IK
        // weight persists, so a component that stops having an opinion must
        // say so. Whoever runs after is free to write their own goals, and
        // whoever runs before was going to be overwritten regardless. No
        // component needs to know what any other one is doing.
        if (t < 0f)
        {
            if (heldGoals)
            {
                anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                heldGoals = false;
            }

            return;
        }

        heldGoals = true;

        if (!sampled) return;

        // ---- CONSTANT WEIGHT, MOVING TARGET ----
        //
        // The weight does NOT ramp. Ramping it was the teleport: a low weight
        // does not mean "near the rest pose", it means "near the CLIP pose",
        // and the clip pose is somewhere else entirely.
        //
        // Held at full for the whole swing, with the TARGET travelling from
        // the rest pose out and back. Continuity is then guaranteed by the
        // curve rather than hoped for: Reach() returns 0 at t=0 and at t=1, so
        // both ends of the swing ask for exactly where the hands already are.
        float w = weight;

        float k = Reach(t);

        Vector3 left = HandTarget(k, -1f);
        Vector3 right = HandTarget(k, +1f);

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, left);
        anim.SetIKPosition(AvatarIKGoal.RightHand, right);

        // ---- PALMS FLAT INTO THE SHOVE ----
        //
        // A hand bone has no standard orientation - which axis runs along the
        // fingers is a decision the rig made - so this cannot be derived and
        // has to be an offset from the direction of travel. Mirrored on Z
        // between the two hands, exactly as FirstPersonHands does it, because
        // a left hand is a right hand reflected.
        Quaternion look = Aim();

        Quaternion rightPalm = look * Quaternion.Euler(palmEuler);
        Quaternion leftPalm = look * Quaternion.Euler(palmEuler.x, palmEuler.y,
                                                      -palmEuler.z);

        // Turned by the same curve that moves them, and from the rotation the
        // hands actually had. A palm that snapped to its shove angle on frame
        // one would be the same teleport in a different axis.
        float turn = Mathf.Clamp01(k);

        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, w);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, w);
        anim.SetIKRotation(AvatarIKGoal.LeftHand,
                           Quaternion.Slerp(body.rotation * restRotL, leftPalm, turn));
        anim.SetIKRotation(AvatarIKGoal.RightHand,
                           Quaternion.Slerp(body.rotation * restRotR, rightPalm, turn));
    }

    /// <summary>
    /// Where one hand should be, given how far out the shove is.
    ///
    /// LERPED FROM WHERE THE HAND ACTUALLY IS, not built from scratch. At k=0
    /// this returns the sampled rest pose exactly, which is what makes the
    /// start and end of the swing invisible instead of a jump.
    ///
    /// Unclamped, so the small negative k during the wind-up pulls the hands
    /// BEHIND their rest position rather than clamping them to it.
    /// </summary>
    Vector3 HandTarget(float k, float side)
    {
        Vector3 rest = body.TransformPoint(side < 0f ? restLocalL : restLocalR);

        // Sideways and forward come from the AIM; the height comes from the
        // body, because a shove leaves your shoulders wherever you happen to
        // be looking - it is the direction that pitches, not the chest.
        Vector3 full = body.position + Vector3.up * height +
                       Aim() * new Vector3(side * spread * 0.5f, 0f, reach);

        Vector3 result = Vector3.LerpUnclamped(rest, full, k);

        // ---- NEVER CLOSER TO THE EYE THAN THIS ----
        //
        // k goes negative during the wind-up by design - that IS the draw
        // back - and LerpUnclamped keeps going past rest rather than stopping
        // there. Past rest is toward the camera, and in first person that put
        // a fist a few centimetres from the lens: the screenshot.
        //
        // Only checked locally. A remote body's hands filling YOUR screen is
        // not a thing - you are watching them from metres away - and Eye is
        // null on a teammate's machine's copy of them anyway.
        if (motor != null && motor.IsLocal && motor.Eye != null)
        {
            Vector3 eye = motor.Eye.position;
            Vector3 fromEye = result - eye;
            float d = fromEye.magnitude;

            if (d < minEyeDistance && d > 0.0001f)
                result = eye + fromEye * (minEyeDistance / d);
        }

        return result;
    }

    /// <summary>
    /// How far out the shove is, 0 to 1, dipping slightly negative during the
    /// wind-up. Zero at both ends of the swing, which is the whole contract:
    /// the hands begin and finish exactly where they were resting.
    /// </summary>
    float Reach(float t)
    {
        // Wind up: draw back a little. Expressed as a FRACTION of the reach so
        // that changing the reach cannot leave the wind-up out of proportion.
        float back = -Mathf.Abs(windBack) / Mathf.Max(0.01f, reach);

        if (t < windPart)
            return Mathf.Lerp(0f, back, Smooth(t / Mathf.Max(0.001f, windPart)));

        float rest = (t - windPart) / Mathf.Max(0.001f, 1f - windPart);

        // ---- OUT FIRMLY, BACK SLOWLY ----
        //
        // A symmetrical curve retracts as hard as it extends, which reads as a
        // puppet being pulled. Real arms are thrown out and then relax, so the
        // return is the longer half and both halves are eased - the earlier
        // squared thrust crossed the whole distance in a few frames and had no
        // travel to see.
        if (rest < thrustPart)
            return Mathf.Lerp(back, 1f, Smooth(rest / thrustPart));

        return Mathf.Lerp(1f, 0f, Smooth((rest - thrustPart) / (1f - thrustPart)));
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);
}
