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
    Camera camComponent;

    ElevatorButton[] buttons;
    ElevatorButton hovered;
    TextMesh floorText;

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
            camComponent = Camera.main;
            cam = Camera.main.transform;
            fpCam = Camera.main.GetComponent<FirstPersonCamera>();
        }

        buttons = GetComponentsInChildren<ElevatorButton>(true);

        var ft = transform.Find("Face/FloorText");
        if (ft != null) floorText = ft.GetComponent<TextMesh>();
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

        if (state == State.Active) UpdatePointer();
        UpdateReadout();
    }

    // ------------------------------------------------------------------
    // POINTING AT A PHYSICAL BUTTON
    //
    // A raycast from the camera through the cursor. This is what a UI Canvas
    // would have done for us, and it is about fifteen lines to do by hand -
    // a fair price for buttons that live in the room, catch the cage light,
    // and can be seen being pressed by everyone else in the car.
    // ------------------------------------------------------------------

    void UpdatePointer()
    {
        var mouse = Mouse.current;
        if (mouse == null || camComponent == null) return;

        RefreshInteractable();

        ElevatorButton hit = null;
        Ray ray = camComponent.ScreenPointToRay(mouse.position.ReadValue());

        // Short range: the panel is half a metre away. A long ray would let
        // you press buttons through the far wall of the car.
        if (Physics.Raycast(ray, out RaycastHit info, 4f, ~0, QueryTriggerInteraction.Ignore))
            hit = info.collider.GetComponentInParent<ElevatorButton>();

        if (hit != null && !hit.Interactable) hit = null;

        if (hit != hovered)
        {
            if (hovered != null) hovered.SetHover(false);
            hovered = hit;
            if (hovered != null) hovered.SetHover(true);
        }

        if (hovered != null && mouse.leftButton.wasPressedThisFrame)
        {
            hovered.Poke();
            Activate(hovered);
        }
    }

    void RefreshInteractable()
    {
        if (buttons == null || elevator == null) return;

        foreach (var b in buttons)
        {
            if (b == null) continue;
            b.Interactable = b.kind switch
            {
                ElevatorButton.Kind.Up   => !elevator.IsMoving && elevator.CurrentFloor > 0,
                ElevatorButton.Kind.Down => !elevator.IsMoving && elevator.CurrentFloor < elevator.lowestFloor,
                _ => !elevator.IsMoving
            };
        }
    }

    void Activate(ElevatorButton b)
    {
        if (elevator == null) return;

        switch (b.kind)
        {
            case ElevatorButton.Kind.Up: elevator.GoUp(); break;
            case ElevatorButton.Kind.Down: elevator.GoDown(); break;
            // Digit / Go / Clear / Return arrive in Steps 6 and 10.
        }
    }

    /// <summary>
    /// The number on the panel. Updated whether or not anyone is using it -
    /// the readout is for the whole crew, not just the driver.
    /// </summary>
    void UpdateReadout()
    {
        if (floorText == null || elevator == null) return;

        floorText.text = elevator.IsMoving
            ? $"{elevator.CurrentFloor} > {elevator.TargetFloor}"
            : $"{elevator.CurrentFloor:00}";

        floorText.color = elevator.IsMoving
            ? new Color(1f, 0.72f, 0.25f)
            : new Color(0.15f, 0.85f, 1f);
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
    // THE ONLY SCREEN-SPACE TEXT LEFT.
    //
    // The panel itself is real geometry - buttons you raycast, a readout that
    // is lit by the cage light and can be blocked by somebody standing in
    // front of it. What stays on the screen is the bit that is not a physical
    // object: a prompt telling you the key, and a reminder of how to leave.
    //
    // Those two are genuinely information ABOUT the world rather than IN it,
    // which is the line worth holding when deciding what gets a mesh.
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 14, alignment = TextAnchor.MiddleCenter };

        if (state == State.Idle)
        {
            if (!PlayerIsNear()) return;

            style.normal.textColor = new Color(0.4f, 0.85f, 1f);
            GUI.Label(new Rect((Screen.width - 700f) * 0.5f, Screen.height * 0.5f + 60f, 700f, 24f),
                      "F   use the dashboard", style);
            return;
        }

        if (state != State.Active) return;

        style.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
        GUI.Label(new Rect((Screen.width - 700f) * 0.5f, Screen.height - 56f, 700f, 24f),
                  "click a button      F  or  Esc   step back", style);
    }
}
