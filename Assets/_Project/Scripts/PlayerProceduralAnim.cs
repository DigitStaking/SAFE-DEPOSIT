// PlayerProceduralAnim.cs  -  SAFE DEPOSIT
// Lightweight motion for the FBX visual until real Animator clips exist.
// Ground walk bob + stride sway. Rope/air clips later.

using UnityEngine;

[DefaultExecutionOrder(40)]
public class PlayerProceduralAnim : MonoBehaviour
{
    [Header("Visual root")]
    [Tooltip("Usually PlayerModel_FBX_VISUAL. Auto-found if empty.")]
    public Transform visualRoot;

    [Header("Facing")]
    [Tooltip("Extra yaw on the visual only. If the model faces the wrong way, try 0 or 180.")]
    public float visualYawOffset = 0f;

    [Header("Walk (FALLBACK only — prefer real Mixamo Animator clips)")]
    [Tooltip("Leave OFF when using real skeletal animations from Mixamo.")]
    public bool useProceduralWalk = false;
    public float walkBobAmplitude = 0.05f;
    public float walkBobFrequency = 8.5f;
    public float walkSwayDegrees = 3.5f;
    public float moveThreshold = 0.35f;

    PlayerMotor motor;
    Rigidbody rb;
    Vector3 baseLocalPos;
    Quaternion baseLocalRot;
    float phase;
    bool hasBase;

    void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        rb = GetComponent<Rigidbody>();
        if (visualRoot == null)
        {
            var t = transform.Find("PlayerModel_FBX_VISUAL");
            if (t != null) visualRoot = t;
        }
        CacheBase();

        // Ensure FP head cull exists even if setup menu wasn't re-run.
        if (GetComponent<LocalFirstPersonBodyCull>() == null)
            gameObject.AddComponent<LocalFirstPersonBodyCull>();
        if (GetComponent<PlayerAnimatorDriver>() == null)
            gameObject.AddComponent<PlayerAnimatorDriver>();
    }

    void LateUpdate()
    {
        if (visualRoot == null) return;
        if (!hasBase) CacheBase();

        bool grounded = motor == null || motor.IsGrounded;
        float speed = 0f;
        if (rb != null)
        {
            var v = rb.linearVelocity;
            speed = new Vector3(v.x, 0f, v.z).magnitude;
        }

        bool walking = useProceduralWalk && grounded && speed > moveThreshold &&
                       (motor == null || motor.MoveIntent > 0.05f);

        if (walking) phase += Time.deltaTime * walkBobFrequency * Mathf.Clamp01(speed / 4.5f);
        else phase = Mathf.MoveTowards(phase, 0f, Time.deltaTime * 6f);

        float bob = walking ? Mathf.Sin(phase) * walkBobAmplitude : 0f;
        float sway = walking ? Mathf.Sin(phase * 0.5f) * walkSwayDegrees : 0f;

        visualRoot.localPosition = baseLocalPos + new Vector3(0f, Mathf.Abs(bob), 0f);
        visualRoot.localRotation = baseLocalRot * Quaternion.Euler(0f, visualYawOffset, sway);
    }

    void CacheBase()
    {
        if (visualRoot == null) return;
        baseLocalPos = visualRoot.localPosition;
        // Strip any previous yaw offset from cache by using current as base once.
        baseLocalRot = Quaternion.Euler(0f, 0f, 0f);
        // Keep whatever X/Z the setup tool put, preserve Y=0 for facing control.
        var e = visualRoot.localEulerAngles;
        baseLocalRot = Quaternion.Euler(e.x, 0f, e.z);
        hasBase = true;
    }

    [ContextMenu("Flip Visual Yaw 180")]
    public void FlipYaw()
    {
        visualYawOffset = Mathf.Approximately(visualYawOffset, 180f) ? 0f : 180f;
    }
}
