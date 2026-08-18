// ElevatorDashboard.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ElevatorDashboard.cs
// Goes on: the Dashboard object inside the car, built by ElevatorBuilder.
//
// ====================================================================
// ELEVATOR_SPEC STEP 5 - DASHBOARD, PART ONE.
//
// Press F near the panel. The camera moves in, the crosshair becomes a
// cursor, movement locks. UP and DOWN drive the car. F or Esc steps back.
//
// Step 6 adds numeric entry, GO, fast travel and the floor list. Nothing
// here should need rewriting for that - this file owns GETTING TO the panel
// and back, and Step 6 only adds controls to draw while you are at it.
//
// ====================================================================
// WHY THIS IS A CAMERA MOVE AND NOT A MENU.
//
// A menu would be less work and it would be wrong. The dashboard is a
// physical object in a room with three other people in it, and the moment it
// becomes a fullscreen overlay it stops being somewhere you STAND. The whole
// argument the elevator exists to host - who is driving, who is still in the
// room, why are we going down before Karim is back - only happens if using
// the panel means walking to it and turning your back on the door.
//
// So: the camera flies to a point in the world, your body stays where it is,
// and everyone else can see you standing at the controls.
// ====================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class ElevatorDashboard : MonoBehaviour
{
    [Header("Links - found automatically if left empty")]
    public Elevator elevator;

    [Tooltip("Where the camera sits while you are using the panel. " +
             "ElevatorBuilder makes this as DashboardAnchor.")]
    public Transform viewAnchor;

    [Header("Use")]
    [Tooltip("How close the player has to be for the F prompt to appear.")]
    public float useRange = 2.2f;

    [Tooltip("Seconds for the camera to fly in or out.")]
    public float moveTime = 0.28f;

    public bool InUse => state == State.Entering || state == State.Active;

    enum State { Idle, Entering, Active, Exiting }
    State state = State.Idle;

    PlayerMotor motor;
    PlayerInput playerInput;
    FirstPersonCamera fpCam;
    Transform cam;

    // Where the camera was when we took it over, so we can put it back.
    Vector3 fromPos;
    Quaternion fromRot;
    float t;

    void Start()
    {
        if (elevator == null) elevator = GetComponentInParent<Elevator>();
        if (viewAnchor == null) viewAnchor = transform.Find("DashboardAnchor");

        if (elevator == null)
            Debug.LogError("[Dashboard] No Elevator found in parents.");
        if (viewAnchor == null)
            Debug.LogError("[Dashboard] No DashboardAnchor - run Build Elevator Car.");

        // Single-player lookup. Phase C replaces this with a player registry;
        // until then there is exactly one of each and finding it is honest.
        motor = Object.FindFirstObjectByType<PlayerMotor>();
        if (motor != null) playerInput = motor.GetComponent<PlayerInput>();

        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            fpCam = Camera.main.GetComponent<FirstPersonCamera>();
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (state == State.Idle)
        {
            if (kb.fKey.wasPressedThisFrame && PlayerIsNear()) Enter();
            return;
        }

        // F or Esc steps back. Esc as well as F because every player alive
        // tries Esc first, and a panel you cannot leave is a bug report.
        if (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
        {
            Exit();
            return;
        }
    }

    // The camera has to be placed AFTER FirstPersonCamera would have run, or
    // it fights us for a frame on the way in and out. FirstPersonCamera is
    // disabled while we are active, but during Entering and Exiting it is
    // still off and we are interpolating, so LateUpdate is the right home for
    // both.
    void LateUpdate()
    {
        if (state == State.Idle || cam == null) return;

        t += Time.deltaTime / Mathf.Max(0.01f, moveTime);
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        if (state == State.Entering)
        {
            cam.SetPositionAndRotation(
                Vector3.Lerp(fromPos, viewAnchor.position, k),
                Quaternion.Slerp(fromRot, viewAnchor.rotation, k));

            if (t >= 1f) state = State.Active;
        }
        else if (state == State.Active)
        {
            // Hard-follow rather than a one-off snap: the car MOVES while you
            // are standing at the panel, and a camera parked at a world
            // position would be left behind in the shaft the moment you
            // pressed DOWN. This is the whole reason the anchor is a child of
            // the car.
            cam.SetPositionAndRotation(viewAnchor.position, viewAnchor.rotation);
        }
        else if (state == State.Exiting)
        {
            // Target is recomputed every frame, because the eye moves with
            // the car too.
            Vector3 toPos = fpCam != null ? fpCam.EyePosition : fromPos;
            Quaternion toRot = fpCam != null ? fpCam.EyeRotation : fromRot;

            cam.SetPositionAndRotation(
                Vector3.Lerp(viewAnchor.position, toPos, k),
                Quaternion.Slerp(viewAnchor.rotation, toRot, k));

            if (t >= 1f) Release();
        }
    }

    bool PlayerIsNear()
    {
        if (motor == null) return false;
        return Vector3.Distance(motor.transform.position, transform.position) <= useRange;
    }

    void Enter()
    {
        if (viewAnchor == null || cam == null) return;

        state = State.Entering;
        t = 0f;
        fromPos = cam.position;
        fromRot = cam.rotation;

        // Take the camera off FirstPersonCamera. Disabling rather than
        // fighting it: it writes position and rotation every LateUpdate, and
        // two scripts assigning the same transform in the same frame is a
        // race whose winner depends on script execution order.
        if (fpCam != null) fpCam.enabled = false;

        // Movement lock, in two parts because one is not enough.
        //
        // Disabling PlayerInput stops new input arriving - but PlayerMotor
        // CACHES the last move vector, so if you press F mid-stride it keeps
        // walking into the wall forever. Zeroing speedMultiplier is what
        // actually stops the body.
        if (playerInput != null) playerInput.enabled = false;
        if (motor != null) motor.speedMultiplier = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Exit()
    {
        if (state == State.Idle || state == State.Exiting) return;
        state = State.Exiting;
        t = 0f;
    }

    void Release()
    {
        state = State.Idle;

        if (playerInput != null) playerInput.enabled = true;
        if (motor != null) motor.speedMultiplier = 1f;
        if (fpCam != null) fpCam.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        // Never leave the player frozen with no camera because this object
        // was switched off mid-use.
        if (state != State.Idle) Release();
    }

    // ------------------------------------------------------------------
    // THE PANEL ITSELF
    //
    // Throwaway OnGUI, same as every other HUD in the project so far. Step 6
    // replaces it with the real layout - floor grid, numeric entry, GO, and
    // the load gauge. Drawn in screen space over the panel the camera is
    // looking at, which is a graybox shortcut and looks fine at this stage.
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        if (state == State.Idle)
        {
            if (!PlayerIsNear()) return;

            var prompt = new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            prompt.normal.textColor = new Color(0.4f, 0.85f, 1f);

            GUI.Label(new Rect((Screen.width - 700f) * 0.5f, Screen.height * 0.5f + 60f, 700f, 24f),
                      "F   use the dashboard", prompt);
            return;
        }

        if (state != State.Active || elevator == null) return;

        float w = 320f, h = 250f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);
        GUI.color = Color.white;

        var head = new GUIStyle(GUI.skin.label)
        { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        head.normal.textColor = elevator.IsMoving
            ? new Color(1f, 0.75f, 0.3f)
            : new Color(0.55f, 0.9f, 1f);

        GUI.Label(new Rect(x, y + 14f, w, 30f),
                  elevator.IsMoving
                      ? $"{elevator.CurrentFloor}  →  {elevator.TargetFloor}"
                      : $"FLOOR  {elevator.CurrentFloor:00}", head);

        var sub = new GUIStyle(GUI.skin.label)
        { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        sub.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        GUI.Label(new Rect(x, y + 46f, w, 20f),
                  elevator.CurrentFloor == 0 ? "surface" : "", sub);

        // Buttons go dead while moving. The car refuses anyway - this makes
        // the refusal visible instead of silent.
        GUI.enabled = !elevator.IsMoving && elevator.CurrentFloor > 0;
        if (GUI.Button(new Rect(x + 40f, y + 78f, w - 80f, 46f), "▲   UP"))
            elevator.GoUp();

        GUI.enabled = !elevator.IsMoving && elevator.CurrentFloor < elevator.lowestFloor;
        if (GUI.Button(new Rect(x + 40f, y + 132f, w - 80f, 46f), "▼   DOWN"))
            elevator.GoDown();

        GUI.enabled = true;

        var foot = new GUIStyle(GUI.skin.label)
        { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        foot.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(new Rect(x, y + h - 34f, w, 20f), "F  or  Esc   step back", foot);
    }
}
