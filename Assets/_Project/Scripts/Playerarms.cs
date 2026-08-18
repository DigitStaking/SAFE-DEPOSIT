// PlayerArms.cs  -  SAFE DEPOSIT
// Shared first-person arms: idle / walk swing / carry by weight / climb.
// Prototype lerped targets until a real Animator layer exists.

using UnityEngine;

public class PlayerArms : MonoBehaviour
{
    public enum ArmPose
    {
        Idle,
        Walk,
        CarrySmall,   // one-hand / crate on hip-front
        CarryHeavy,   // two-hand box (filing cabinet)
        CarryMassive, // hug / under arms (vending)
        Climb,
        Point,
        Wave
    }

    [Header("References")]
    public Transform chestPivot;
    public Transform armLeft;
    public Transform armRight;

    [Header("Aim")]
    [Range(0f, 1f)] public float pitchFollow = 0.6f;
    public float poseBlendSpeed = 10f;

    [Header("Idle")]
    public Vector3 idleLeft = new Vector3(-0.30f, -0.18f, 0.48f);
    public Vector3 idleRight = new Vector3(0.30f, -0.18f, 0.48f);

    [Header("Walk swing")]
    public float walkSwing = 0.08f;
    public float walkSwingSpeed = 8.5f;

    [Header("Carry Small (crate)")]
    public Vector3 smallLeft = new Vector3(-0.18f, -0.05f, 0.55f);
    public Vector3 smallRight = new Vector3(0.28f, -0.08f, 0.62f);

    [Header("Carry Heavy (cabinet)")]
    public Vector3 heavyLeft = new Vector3(-0.34f, -0.02f, 0.58f);
    public Vector3 heavyRight = new Vector3(0.34f, -0.02f, 0.58f);

    [Header("Carry Massive (vending)")]
    public Vector3 massiveLeft = new Vector3(-0.42f, 0.05f, 0.48f);
    public Vector3 massiveRight = new Vector3(0.42f, 0.05f, 0.48f);

    [Header("Climb")]
    public Vector3 climbLeft = new Vector3(-0.16f, 0.10f, 0.40f);
    public Vector3 climbRight = new Vector3(0.16f, 0.15f, 0.40f);
    public Vector3 pointArm = new Vector3(0.18f, 0.08f, 0.85f);

    ArmPose pose = ArmPose.Idle;
    Transform cam;
    PlayerMotor motor;
    Rigidbody rb;
    float walkPhase;

    public void SetPose(ArmPose newPose) => pose = newPose;
    public ArmPose CurrentPose => pose;

    /// <summary>Pick carry pose from loot weight.</summary>
    public void SetCarryFor(Carryable item)
    {
        if (item == null) { pose = ArmPose.Idle; return; }
        pose = item.Weight switch
        {
            Carryable.WeightClass.Small => ArmPose.CarrySmall,
            Carryable.WeightClass.Heavy => ArmPose.CarryHeavy,
            _ => ArmPose.CarryMassive
        };
    }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;
        motor = GetComponent<PlayerMotor>();
        rb = GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (chestPivot == null || cam == null) return;
        if (armLeft == null || armRight == null) return;

        // Auto idle/walk when not committed to a special pose.
        if (pose == ArmPose.Idle || pose == ArmPose.Walk)
            pose = ShouldWalk() ? ArmPose.Walk : ArmPose.Idle;

        AimChest();
        BlendPose();
    }

    bool ShouldWalk()
    {
        if (motor == null || !motor.IsGrounded) return false;
        if (motor.MoveIntent < 0.05f) return false;
        if (rb == null) return false;
        var v = rb.linearVelocity;
        return new Vector3(v.x, 0f, v.z).magnitude > 0.35f;
    }

    void AimChest()
    {
        float pitch = cam.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
        chestPivot.localRotation = Quaternion.Euler(pitch * pitchFollow, 0f, 0f);
    }

    void BlendPose()
    {
        Vector3 targetLeft, targetRight;

        switch (pose)
        {
            case ArmPose.CarrySmall:
                targetLeft = smallLeft; targetRight = smallRight; break;
            case ArmPose.CarryHeavy:
                targetLeft = heavyLeft; targetRight = heavyRight; break;
            case ArmPose.CarryMassive:
                targetLeft = massiveLeft; targetRight = massiveRight; break;
            case ArmPose.Climb:
                targetLeft = climbLeft; targetRight = climbRight; break;
            case ArmPose.Point:
                targetLeft = idleLeft; targetRight = pointArm; break;
            case ArmPose.Wave:
                targetLeft = idleLeft;
                targetRight = pointArm + new Vector3(
                    0f, 0.35f + Mathf.Sin(Time.time * 9f) * 0.12f, -0.15f);
                break;
            case ArmPose.Walk:
                walkPhase += Time.deltaTime * walkSwingSpeed;
                float s = Mathf.Sin(walkPhase) * walkSwing;
                targetLeft = idleLeft + new Vector3(0f, 0f, s);
                targetRight = idleRight + new Vector3(0f, 0f, -s);
                break;
            default:
                targetLeft = idleLeft; targetRight = idleRight; break;
        }

        float t = poseBlendSpeed * Time.deltaTime;
        armLeft.localPosition = Vector3.Lerp(armLeft.localPosition, targetLeft, t);
        armRight.localPosition = Vector3.Lerp(armRight.localPosition, targetRight, t);
    }
}
