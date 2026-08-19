// FirstPersonCamera.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/FirstPersonCamera.cs
// Goes on: the Main Camera.
//
// ========================================================================
// THE CAMERA IS NOT A CHILD OF THE PLAYER.
//
// The obvious setup is to parent it to the head and inherit the body's
// rotation. Don't.
//
// The body is a Rigidbody, so it can only rotate in FixedUpdate - 60 times
// a second. Your monitor may draw 144 frames. A camera inheriting that
// rotation updates 60 times and repeats itself the rest, and mouse look
// feels like it is stuttering at a perfect frame rate.
//
// So the camera is a free object that owns yaw and pitch and updates every
// rendered frame. It snaps to the eye position in LateUpdate, after all
// movement is done. The body then turns to match the camera's yaw in
// FixedUpdate, which only matters for what OTHER players see - your own
// view never waits on physics.
// ========================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Tooltip("Offset from the player's pivot (at the feet) to eye height. " +
             "1.60 on a 1.8m character is about right.\n\n" +
             "This was briefly dropped to chase the hand IK, which placed its " +
             "targets relative to the eye. That is no longer a reason to move " +
             "it - the hands are parked until Block 8 - so set this by what " +
             "the WORLD should look like from standing height, nothing else.")]
    public Vector3 eyeOffset = new Vector3(0f, 1.60f, 0.12f);

    // ---- DOWNED: JUST THE EYE HEIGHT ----
    //
    // Tried riding the Head bone so the view would land exactly on the
    // animated face. It put the camera somewhere outside and below the body
    // and it is not worth chasing: the head bone is shrunk to 0.0001 by the
    // body cull, it is re-posed by IK after this script runs, and the kneel
    // clip moves it in ways nothing here can predict.
    //
    // A plain Y is the right tool. One number, visible in the Inspector,
    // tuned by looking at it. Everything else about the eye - the yaw
    // rotation, the forward 0.12 - stays exactly as it is standing, so there
    // is only ever one value to argue with.

    [Tooltip("Eye height while downed. Standing is eyeOffset.y (1.65). " +
             "Tune this against the kneel pose until the view sits on the " +
             "character's face.")]
    public float downedEyeHeight = 1.0f;

    [Tooltip("Seconds to sink into and rise out of the kneeling view.")]
    public float downedBlendTime = 0.45f;

    [Header("Look")]
    [Tooltip("Mouse: degrees per pixel. NOT scaled by delta time - mouse input " +
             "is already a per-frame delta.")]
    public float mouseSensitivity = 0.12f;

    [Tooltip("Gamepad: degrees per second at full deflection. IS scaled by " +
             "delta time, because a held stick is a rate, not a delta.")]
    public float stickSensitivity = 220f;

    [Tooltip("How far up and down you can look. Negative is up.\n\n" +
             "This game NEEDS the full range - players must be able to look " +
             "straight up at the winch and straight down the drop. Do not " +
             "narrow it to the usual 70 or 80.\n\n" +
             "Stops just short of 90 on purpose: at exactly 90 the yaw and roll " +
             "axes align (gimbal lock) and the view snaps sideways.")]
    public float minPitch = -89.9f;
    public float maxPitch = 89.9f;

    [Tooltip("Flip vertical look. Some players cannot play without this - ship " +
             "it as an option, never as a fixed choice.")]
    public bool invertY = false;

    [Header("Comfort")]
    [Tooltip("Camera roll when moving sideways or swinging. Looks good, makes " +
             "some people motion sick. SHIP THIS OFF and expose it as an " +
             "accessibility option. First person plus pendulum motion is the " +
             "most nausea-inducing combination in games.")]
    public bool enableTilt = false;

    [Tooltip("Max roll in degrees. Above about 5 it starts costing you players.")]
    public float maxTilt = 3f;

    [Tooltip("Head bob on solid ground. Never bobs airborne - bobbing during a " +
             "swing is what actually makes people ill.")]
    public bool enableHeadBob = true;
    public float bobFrequency = 9f;
    public float bobAmplitude = 0.06f;

    [Header("Field of view")]
    public float baseFov = 75f;

    [Tooltip("Extra FOV at speed. Sells falling and swinging without moving the " +
             "camera at all - by far the gentlest way to convey motion in first " +
             "person. Roll and shake both cost you motion-sick players.")]
    public float speedFovBoost = 12f;
    public float speedForMaxFov = 14f;

    public float Yaw => yaw;
    public float Pitch => pitch;

    // ------------------------------------------------------------------
    // THE EYE POSE, COMPUTED ON DEMAND.
    //
    // These exist because FirstPersonHands needs to know where the eye is
    // from inside OnAnimatorIK, which Unity calls during the ANIMATION
    // update - before LateUpdate, where this script moves the camera. Read
    // transform.position there and you get LAST frame's value while the
    // body has already moved this frame, so the hands sit permanently
    // behind the view while you walk.
    //
    // Computed from the target's CURRENT position instead, so there is no
    // stale frame. ApplyPosition uses the same two properties, which keeps
    // the camera and the hands agreeing by construction rather than by two
    // copies of the same arithmetic.
    // ------------------------------------------------------------------

    public Vector3 EyePosition
    {
        get
        {
            if (target == null) return transform.position;

            // Y is the only thing that changes. Lerped from the standing
            // height rather than swapped, so going down is a fall.
            Vector3 offset = eyeOffset;
            offset.y = Mathf.Lerp(eyeOffset.y, downedEyeHeight, downedBlend);

            return target.position
                 + Quaternion.Euler(0f, yaw, 0f) * offset
                 + Vector3.up * bobOffset;
        }
    }

    public Quaternion EyeRotation => Quaternion.Euler(pitch, yaw, currentTilt);

    PlayerMotor motor;
    Rigidbody targetBody;
    Camera cam;
    PlayerHealth health;
    float downedBlend;

    float yaw, pitch, bobTimer, currentTilt, bobOffset;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("[FirstPersonCamera] No target assigned.");
            enabled = false;
            return;
        }

        motor = target.GetComponent<PlayerMotor>();
        targetBody = target.GetComponent<Rigidbody>();
        health = target.GetComponent<PlayerHealth>();

        // Already downed on the frame the scene loads - Campaign.Health
        // survives a reload, so start folded rather than blending down from
        // standing in front of the player.
        if (health != null && health.IsDowned) downedBlend = 1f;
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = baseFov;

        yaw = target.eulerAngles.y;
        pitch = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Escape frees the mouse so you can reach the editor without stopping
        // play mode; click to recapture.
        //
        // Reads the NEW input system (Keyboard.current, Mouse.current). The
        // old UnityEngine.Input API throws InvalidOperationException because
        // Active Input Handling is set to Input System Package. The two
        // systems cannot be mixed.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Cursor.lockState == CursorLockMode.None &&
                 Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // LateUpdate runs after every Update, so the player has finished moving
    // this frame before we position the camera. In Update the camera would
    // always be one frame behind - subtle, but it reads as "floaty" and
    // nobody can ever say why.
    void LateUpdate()
    {
        if (target == null || motor == null) return;
        ApplyLook();
        ApplyPosition();
        ApplyFov();
    }

    void ApplyLook()
    {
        Vector2 look = motor.LookInput;

        // A mouse reports pixels moved since last frame - already a delta, so
        // scaling by deltaTime again would tie sensitivity to frame rate. A
        // stick reports how far it is currently pushed - a rate, which must be
        // scaled by deltaTime. Getting this backwards is why some games have
        // gamepad aim that speeds up on better hardware.
        float scale = motor.UsingGamepad
            ? stickSensitivity * Time.deltaTime
            : mouseSensitivity;

        yaw += look.x * scale;
        pitch -= look.y * scale * (invertY ? -1f : 1f);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void ApplyPosition()
    {
        // NOTE: eyeOffset is rotated by YAW inside EyePosition.
        //
        // It used to be added as `target.position + eyeOffset`, in WORLD
        // space - so the 0.12 "forward" component always pointed along world
        // +Z no matter which way you were facing. The camera therefore sat
        // 12cm in FRONT of your face looking one way and 12cm BEHIND it
        // looking the other: a 24cm swing that the hand IK inherited.
        //
        // Yaw only, never pitch - the eye must not slide forward when you
        // look down, or the view pushes into your own chest.
        // Drive the kneel blend first, so EyePosition below reads this
        // frame's value rather than last frame's.
        bool down = health != null && health.IsDowned;
        float step = downedBlendTime > 0.001f ? Time.deltaTime / downedBlendTime : 1f;
        downedBlend = Mathf.MoveTowards(downedBlend, down ? 1f : 0f, step);

        bobOffset = 0f;

        // No walk bob while folding up. Velocity is near zero when downed so
        // this rarely fires anyway, but the bob is a SIN wave that eases to
        // neutral rather than snapping, and a residual centimetre of it
        // riding on top of the head bone reads as a twitch.
        if (enableHeadBob && downedBlend < 0.999f && motor.IsGrounded && targetBody != null)
        {
            Vector3 flat = targetBody.linearVelocity;
            flat.y = 0f;
            float speed = flat.magnitude;

            if (speed > 0.4f)
            {
                // Advanced by distance travelled rather than by time, so the
                // rhythm matches your pace instead of running at a fixed rate.
                bobTimer += Time.deltaTime * bobFrequency * Mathf.Clamp01(speed / 4.5f);
            }
            else
            {
                // Ease to neutral, or the camera jumps every time you stop.
                bobTimer = Mathf.MoveTowards(bobTimer, 0f, Time.deltaTime * 8f);
            }

            bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;
        }

        float targetTilt = 0f;
        if (enableTilt && downedBlend < 0.999f && targetBody != null)
        {
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float lateral = Vector3.Dot(targetBody.linearVelocity, right);
            targetTilt = Mathf.Clamp(-lateral / 6f, -1f, 1f) * maxTilt;
        }
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, 6f * Time.deltaTime);

        transform.position = EyePosition;
        transform.rotation = EyeRotation;
    }

    void ApplyFov()
    {
        if (cam == null || targetBody == null) return;

        float t = Mathf.Clamp01(targetBody.linearVelocity.magnitude / speedForMaxFov);
        float desired = baseFov + speedFovBoost * t;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, desired, 4f * Time.deltaTime);
    }
}