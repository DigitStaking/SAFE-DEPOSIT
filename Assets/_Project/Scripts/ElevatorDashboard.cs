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
// ====================================================================
// STEP 6 - DASHBOARD, PART TWO.
//
// Type a floor on the keypad, press GO, travel there FAST (~8 m/s, see
// Elevator.fastSpeed) instead of the slow UP/DOWN crawl. Nothing from Step 5
// needed rewriting for it, exactly as planned - this file owns getting to the
// panel and back, Step 6 only added controls to draw while you are at it.
//
// SCOPE CUT, DELIBERATE: the spec mockup in ELEVATOR_SPEC.md Part 2 shows a
// full grid of every floor's state (■ reachable / □ beyond cable / ✕
// demolished) permanently on screen. That is NOT built here. With 5 floors
// that exist today and 20 coming in Step 11, a persistent 20-cell grid is a
// layout I cannot verify without a screenshot and a system with nothing real
// to show yet - Step 11 is what actually populates floor content. Instead,
// GO validates against the same data (Campaign.DeepestReachableFloor,
// Campaign.DestroyedRooms) and REJECTS with a reason - "12 SEALED", "12
// BEYOND CABLE" - which is the same information, delivered reactively rather
// than as a permanent display. The full grid is easy to add later against
// this same validation logic once there is real content to show it against.
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

    [Tooltip("Step 7. UP / DOWN / GO route through this instead of calling " +
             "Elevator directly, so a departure can be held for the bridge's " +
             "retract warning. Found automatically if left empty.")]
    public ElevatorBridge bridge;

    [Tooltip("Step 8. Checked before any departure - overloaded rejects " +
             "immediately rather than spending a 5-second warning on a trip " +
             "that was never going to leave. Found automatically if left empty.")]
    public ElevatorDeck deck;

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

    // What you have typed so far, shown on the readout in place of the
    // current floor while it is non-empty. Capped at two digits - the demo
    // tops out at floor 20; three digits for the full game's 100 is a
    // problem for whenever a cable can actually reach that deep.
    string entryBuffer = "";

    // A rejection reason, shown in place of the normal readout for a couple
    // of seconds after a bad GO - "12 SEALED", "12 BEYOND CABLE" - then it
    // clears itself. This is the floor-state feedback from the spec's grid
    // mockup, just delivered reactively instead of as a permanent display.
    string statusMsg;
    float statusMsgUntil;

    // Where the camera was when we took it over, so we can put it back.
    Vector3 fromPos;
    Quaternion fromRot;
    float t;

    void Start()
    {
        if (elevator == null) elevator = GetComponentInParent<Elevator>();
        if (bridge == null) bridge = GetComponentInParent<ElevatorBridge>();
        if (deck == null) deck = GetComponentInParent<ElevatorDeck>();
        if (viewAnchor == null) viewAnchor = transform.Find("DashboardAnchor");

        if (elevator == null)
            Debug.LogError("[Dashboard] No Elevator found in parents.");
        if (bridge == null)
            Debug.LogError("[Dashboard] No ElevatorBridge found in parents.");
        if (deck == null)
            Debug.LogError("[Dashboard] No ElevatorDeck found in parents.");
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

        run = Object.FindFirstObjectByType<RunManager>();

        // Remember where we started so an arrival at floor 0 can be told
        // apart from simply BEGINNING at floor 0.
        wasMoving = elevator != null && elevator.IsMoving;
    }

    RunManager run;
    bool wasMoving;

    /// <summary>
    /// The car arriving at the surface is what ends a run. Detected as a
    /// transition - moving, then stopped, at floor 0 - rather than polled as
    /// "is at floor 0", so a car that STARTS the scene parked at the surface
    /// does not instantly end the run before anyone has moved.
    /// </summary>
    void CheckSurfaceArrival()
    {
        if (elevator == null || run == null) return;

        bool moving = elevator.IsMoving;
        bool justArrived = wasMoving && !moving;
        wasMoving = moving;

        if (justArrived && elevator.CurrentFloor == 0)
        {
            // If the player is standing at the panel when the run ends, hand
            // the camera back first - the results screen needs the mouse,
            // and Release() is what restores it along with movement.
            if (state != State.Idle) Release();
            run.Extract();
        }
    }

    void Update()
    {
        // FIRST, and outside every early return below. The run ends when the
        // car reaches the surface whether or not anybody happens to be
        // standing at the panel at that moment - a crew that pressed RETURN
        // and then walked away to argue about the haul still arrives.
        CheckSurfaceArrival();

        // ALSO outside every early return, and for the same reason.
        //
        // This used to live at the bottom of this method, past the
        // `state == Idle` return - so the panel only refreshed while someone
        // was standing at it in first person. From anywhere else in the car
        // it showed whatever floor it had last been looked at from, which is
        // worse than showing nothing: a crewmate glancing over from the far
        // corner read 01 while the lift was passing 03.
        //
        // A floor indicator is a thing OTHER PEOPLE read. It is mounted in a
        // shared space, at head height, in a game whose whole loop is four
        // players shouting at each other - the one player driving it is the
        // one who needs it least. ElevatorDeck's load gauge has always
        // updated in its own unconditional Update, which is why nobody ever
        // filed this bug against the load number.
        UpdateReadout();

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
        if (buttons == null || elevator == null || bridge == null) return;

        // Dead while the elevator is moving OR while the bridge is mid-cycle
        // (Warning, Retracting, or still swinging out from the last
        // arrival) - the same "make the refusal visible" rule as everywhere
        // else, now covering the window RequestGoToFloor also refuses.
        bool busy = elevator.IsMoving || bridge.IsBusy;

        foreach (var b in buttons)
        {
            if (b == null) continue;
            b.Interactable = b.kind switch
            {
                // UP stops at floor 1, NOT floor 0.
                //
                // Floor 0 is the surface - reaching it ENDS THE RUN. Letting
                // UP walk you there from floor 1 meant one keypress skipped
                // every check RETURN exists to enforce: nobody counted the
                // crew, nobody checked the load, and the run just ended.
                // Floor 0 is now reachable only through TryReturn(), which
                // is where those checks live.
                ElevatorButton.Kind.Up   => !busy && elevator.CurrentFloor > 1,
                ElevatorButton.Kind.Down => !busy && elevator.CurrentFloor < elevator.lowestFloor,
                _ => !busy
            };
        }
    }

    void Activate(ElevatorButton b)
    {
        if (elevator == null || bridge == null) return;

        switch (b.kind)
        {
            // Step 7: routed through the bridge, not called on Elevator
            // directly - see ElevatorBridge.cs for why. The button still
            // reads elevator.IsMoving/CurrentFloor for its own Interactable
            // state above; only the ACTION of pressing it changed.
            //
            // Step 8: checked here too, not just in TryGo() - "It will not
            // move while overloaded" applies to the slow crawl exactly as
            // much as it does to GO.
            case ElevatorButton.Kind.Up:
                entryBuffer = "";
                if (!RejectIfOverloaded()) bridge.RequestGoUp();
                break;

            case ElevatorButton.Kind.Down:
                entryBuffer = "";
                if (!RejectIfOverloaded()) bridge.RequestGoDown();
                break;

            case ElevatorButton.Kind.Digit:
                if (entryBuffer.Length < 2) entryBuffer += b.digit;
                break;

            case ElevatorButton.Kind.Clear:
                entryBuffer = "";
                break;

            case ElevatorButton.Kind.Go:
                TryGo();
                break;

            case ElevatorButton.Kind.Return:
                TryReturn();
                break;
        }
    }

    /// <summary>
    /// RETURN TO SURFACE. The departure vote from the spec, minus the vote -
    /// with one player there is nobody to vote against, so what remains is
    /// the half that actually matters: the check that everyone is aboard,
    /// and NAMING whoever is not.
    ///
    /// "CANNOT DEPART - KARIM IS NOT ABOARD" is the spec's own example, and
    /// the naming is the whole design. ECONOMY_AND_CAMPAIGN.md is explicit
    /// about why: "Name them, don't just say 'someone'. With proximity
    /// voice, three people shouting one name is the moment." A count would
    /// tell you a problem exists; a name tells you whose problem it is.
    /// </summary>
    void TryReturn()
    {
        entryBuffer = "";

        if (elevator.CurrentFloor == 0) { Reject("ALREADY AT SURFACE"); return; }
        if (RejectIfOverloaded()) return;

        string missing = FirstCrewMemberNotAboard();
        if (missing != null) { Reject($"{missing.ToUpper()} IS NOT ABOARD"); return; }

        // Floor 0 through the bridge like any other departure, so the
        // retract warning still runs - leaving is not exempt from the
        // countdown that makes leaving tense.
        bridge.RequestGoToFloor(0, fast: true);
    }

    /// <summary>
    /// The name of one crew member who is not currently inside the car, or
    /// null if everyone is aboard.
    ///
    /// Reads Elevator.Riders - the same overlap query that decides who gets
    /// carried when the car moves and whose 70kg counts toward the load. A
    /// separate "is aboard" test could disagree with those two, and a
    /// departure check that disagrees with the thing that physically
    /// carries you is the worst possible kind of bug.
    /// </summary>
    string FirstCrewMemberNotAboard()
    {
        foreach (var motor in Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
        {
            if (motor == null) continue;

            bool aboard = false;
            foreach (var rb in elevator.Riders)
                if (rb != null && rb.gameObject == motor.gameObject) { aboard = true; break; }

            if (!aboard) return motor.gameObject.name;
        }
        return null;
    }

    /// <summary>
    /// GO. Validates against the same reachability and demolition data the
    /// spec's floor grid would have shown, and rejects with a reason instead
    /// of silently doing nothing - "nothing happened" is the worst possible
    /// response to a button press.
    /// </summary>
    void TryGo()
    {
        if (entryBuffer.Length == 0) return;

        int floor = int.Parse(entryBuffer);
        entryBuffer = "";

        // Typing 0 does NOT depart. Ending the run is RETURN's job and only
        // RETURN's. Briefly this routed to TryReturn() so the checks applied
        // however you asked - correct on safety, wrong on feel: a keypad
        // entry that silently ends the run is a surprise, and one button
        // meaning exactly one thing beats a second hidden way to do it.
        // The 0 KEY still works, for typing 10 and 20.
        if (floor == 0) { Reject("USE RETURN"); return; }

        if (floor > elevator.lowestFloor) { Reject($"NO FLOOR {floor:00}"); return; }

        // ALREADY HERE. Rejected LOUDLY rather than quietly ignored, because
        // quietly ignored is what it used to be and it was the worst bug on
        // the panel: Elevator.GoToFloor drops a request for the floor it is
        // already parked at, but by then the bridge had already run its full
        // five-second warning and retracted. The lift then never moved - so
        // the crew was left standing in a car with the bridge gone, staring
        // into an open shaft, with no way to call it back except pressing a
        // DIFFERENT floor. Refusing here means the bridge is never asked.
        if (floor == elevator.CurrentFloor) { Reject($"ALREADY ON {floor:00}"); return; }

        if (floor > 0 && Campaign.DestroyedRooms.Contains(floor)) { Reject($"{floor:00} SEALED"); return; }
        if (floor > 0 && floor > Campaign.DeepestReachableFloor) { Reject($"{floor:00} BEYOND CABLE"); return; }
        if (RejectIfOverloaded()) return;

        // fast: true is the whole point of Step 6 - this is what makes GO
        // different from just pressing DOWN repeatedly. Routed through the
        // bridge (Step 7) the same as Up/Down, so a numeric-entry departure
        // gets the same retract warning a call-button departure does.
        bridge.RequestGoToFloor(floor, fast: true);
    }

    /// <summary>
    /// Step 8. Checked before EVERY departure - Up, Down and GO alike -
    /// rather than left to Elevator or Bridge, so it can reject immediately
    /// with a reason instead of spending a 5-second warning on a trip the
    /// car was never going to make. "It will not move while overloaded" per
    /// the spec; this is where that rule actually lives.
    /// </summary>
    bool RejectIfOverloaded()
    {
        if (deck == null || !deck.IsOverloaded) return false;
        Reject("OVERLOADED");
        return true;
    }

    void Reject(string message)
    {
        statusMsg = message;
        statusMsgUntil = Time.time + 2f;
    }

    /// <summary>
    /// The number on the panel. Updated whether or not anyone is using it -
    /// the readout is for the whole crew, not just the driver. Priority:
    /// what you are typing, then a recent rejection reason, then the normal
    /// floor / moving display.
    /// </summary>
    void UpdateReadout()
    {
        if (floorText == null || elevator == null) return;

        if (entryBuffer.Length > 0)
        {
            floorText.text = entryBuffer + "_";
            floorText.color = Color.white;
            return;
        }

        if (statusMsg != null)
        {
            if (Time.time < statusMsgUntil)
            {
                floorText.text = statusMsg;
                floorText.color = new Color(1f, 0.35f, 0.3f);
                return;
            }
            statusMsg = null;
        }

        floorText.text = elevator.IsMoving
            ? $"{elevator.DisplayFloor:00}"
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

        // Start clean - a "12 SEALED" from three visits ago has no business
        // greeting the next person who walks up to the panel.
        entryBuffer = "";
        statusMsg = null;

        // Take the camera off FirstPersonCamera. Disabling rather than
        // fighting it: it writes position and rotation every LateUpdate, and
        // two scripts assigning the same transform in the same frame is a
        // race whose winner depends on script execution order.
        if (fpCam != null) fpCam.enabled = false;

        // Movement lock, in two parts because one is not enough.
        //
        // Disabling PlayerInput stops new input arriving - but PlayerMotor
        // CACHES the last move vector, so if you press F mid-stride it keeps
        // walking into the wall forever. Zeroing the external lock is what
        // actually stops the body.
        //
        // externalSpeedLock, NOT the old shared speedMultiplier: releasing
        // used to assign 1, which would have healed a limp on the way out of
        // the panel. This field is only ever ours.
        if (playerInput != null) playerInput.enabled = false;
        if (motor != null) motor.externalSpeedLock = 0f;

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

        // Clear on the way OUT as well as on the way in. It only mattered on
        // entry while the readout was drawn for whoever was standing at the
        // panel; now that it is live for the whole car, a half-typed "12_"
        // abandoned by someone who wandered off would sit there being read as
        // the floor by three other people.
        entryBuffer = "";
        statusMsg = null;

        if (playerInput != null) playerInput.enabled = true;
        if (motor != null) motor.externalSpeedLock = 1f;   // releases OUR lock only
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
