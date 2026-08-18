// Elevator.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Elevator.cs
// Goes on: the ELEVATOR root built by Editor/ElevatorBuilder.cs.
//
// ====================================================================
// ELEVATOR_SPEC STEP 4 - MOVEMENT.
//
// Moves between fixed floor heights, one floor at a time, on a hard-coded
// key. Doors lock while moving. That is the whole step.
//
// The dashboard (Steps 5-6) does not exist yet, so PageUp / PageDown stand
// in for the UP and DOWN buttons. Everything the dashboard will need is
// already public below - GoToFloor, CurrentFloor, IsMoving, DoorsLocked - so
// Step 5 is a UI on top of this, not a rewrite of it.
//
// ====================================================================
// THE ONLY HARD PART IS CARRYING THE PLAYER.
//
// A kinematic body sweeping upward pushes a dynamic body resting on it, so
// going UP almost works by accident. Going DOWN does not: gravity
// accelerates the player at 9.8 m/s^2 while the floor drops at a constant
// 8 m/s, so the floor leaves and the player falls after it, landing again
// every frame. That reads as violent shaking, and at speed the player falls
// straight through.
//
// Friction cannot fix it either - there is no sideways contact to grip.
//
// So riders are carried EXPLICITLY: work out how far the car moved this
// physics step and move everything standing in it by the same amount. The
// player keeps their own velocity and input on top, so you can walk around a
// moving lift, which is the whole point of it being a room.
// ====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Elevator : MonoBehaviour
{
    [Header("Shaft")]
    [Tooltip("World Y of floor 0, the surface. Floor N sits floorHeight below it.")]
    public float surfaceY = 0f;

    [Tooltip("Must match GrayboxBuilder.FloorHeight, or the car stops between floors.")]
    public float floorHeight = 4f;

    [Tooltip("Deepest floor that exists. Step 11 raises this to 20 for the demo.")]
    public int lowestFloor = 5;

    [Header("Speed")]
    [Tooltip("UP / DOWN, one floor, deliberate.")]
    public float slowSpeed = 2f;

    [Tooltip("Type a floor and press GO. Used from Step 6.")]
    public float fastSpeed = 8f;

    [Header("Doors")]
    [Tooltip("Which of the four sides faces the room. Step 11 sets this per " +
             "floor; until then every floor uses the same side.")]
    public string activeSide = "Side_East";

    [Tooltip("How far a shutter has rolled, in metres, when fully open.")]
    public float shutterRoll = 0.2f;

    public float shutterSpeed = 2.5f;

    // ---- what the dashboard will read ----
    public int CurrentFloor { get; private set; }
    public int TargetFloor  { get; private set; }
    public bool IsMoving    { get; private set; }

    /// <summary>Doors are locked whenever the car is not stopped at a floor.</summary>
    public bool DoorsLocked => IsMoving;

    Rigidbody rb;

    // Ride volume, derived from the car's own geometry rather than restated
    // here. ElevatorBuilder owns the dimensions; this only has to find them.
    float rideHalfXZ = 2f;
    float rideHeight = 2.8f;

    readonly List<Rigidbody> riders = new List<Rigidbody>();
    static readonly Collider[] Overlap = new Collider[64];

    class Shutter
    {
        public Transform t;
        public Vector3 closedPos, closedScale, openPos, openScale;
        public float open;          // 0 shut, 1 rolled up
    }
    readonly List<Shutter> shutters = new List<Shutter>();
    Shutter active;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Kinematic: this is a platform, not a falling object. A dynamic
        // elevator would sag under the weight of the crew standing in it,
        // which is a lovely idea and a completely different system.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        MeasureRideVolume();
        CollectShutters();

        CurrentFloor = TargetFloor = FloorAt(rb.position.y);
        SnapToFloor(CurrentFloor);
    }

    /// <summary>
    /// Read the interior from the geometry instead of repeating ElevatorBuilder's
    /// constants. Change the car's size there and this follows on its own.
    /// </summary>
    void MeasureRideVolume()
    {
        var floor = transform.Find("Car/Floor");
        var ceiling = transform.Find("Car/Ceiling");

        if (floor != null)
            // A hair inside the walls, so a player pressed against one is not
            // excluded by a rounding error.
            rideHalfXZ = floor.localScale.x * 0.5f - 0.1f;

        if (ceiling != null)
            rideHeight = ceiling.localPosition.y;
    }

    void CollectShutters()
    {
        var car = transform.Find("Car");
        if (car == null) { Debug.LogError("[Elevator] No Car child - run Build Elevator Car."); return; }

        foreach (Transform side in car)
        {
            if (!side.name.StartsWith("Side_")) continue;
            var t = side.Find("Shutter");
            if (t == null) continue;

            // The CLOSED pose is whatever the builder left. The open pose is
            // derived from it, so the two files never have to agree on a
            // number - only on the fact that a shutter starts shut.
            var s = new Shutter
            {
                t = t,
                closedPos = t.localPosition,
                closedScale = t.localScale
            };

            float top = s.closedPos.y + s.closedScale.y * 0.5f;
            s.openScale = new Vector3(s.closedScale.x, shutterRoll, s.closedScale.z);
            s.openPos = new Vector3(s.closedPos.x, top - shutterRoll * 0.5f, s.closedPos.z);

            shutters.Add(s);
            if (side.name == activeSide) active = s;
        }

        if (active == null)
            Debug.LogWarning($"[Elevator] No side named '{activeSide}'. No door will open.");
    }

    // ------------------------------------------------------------------
    // INPUT - a stand-in for the dashboard's UP and DOWN buttons.
    // ------------------------------------------------------------------

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.pageUpKey.wasPressedThisFrame) GoUp();
        if (kb.pageDownKey.wasPressedThisFrame) GoDown();

        DriveShutters();
    }

    public void GoUp() => GoToFloor(TargetFloor - 1);
    public void GoDown() => GoToFloor(TargetFloor + 1);

    /// <summary>
    /// Send the car to a floor. Refused while already moving - the dashboard
    /// gets to change its mind in Step 7, once the bridge exists to make that
    /// decision cost something.
    /// </summary>
    public void GoToFloor(int floor, bool fast = false)
    {
        if (IsMoving) return;

        floor = Mathf.Clamp(floor, 0, lowestFloor);
        if (floor == CurrentFloor) return;

        TargetFloor = floor;
        IsMoving = true;
        useFast = fast;
    }

    bool useFast;

    public float FloorY(int floor) => surfaceY - floorHeight * floor;

    int FloorAt(float y) => Mathf.Clamp(
        Mathf.RoundToInt((surfaceY - y) / floorHeight), 0, lowestFloor);

    void SnapToFloor(int floor)
    {
        var p = rb.position;
        rb.position = new Vector3(p.x, FloorY(floor), p.z);
    }

    // ------------------------------------------------------------------
    // MOVEMENT
    // ------------------------------------------------------------------

    void FixedUpdate()
    {
        if (!IsMoving) return;

        float target = FloorY(TargetFloor);
        float speed = useFast ? fastSpeed : slowSpeed;
        float newY = Mathf.MoveTowards(rb.position.y, target, speed * Time.fixedDeltaTime);

        Vector3 from = rb.position;
        Vector3 to = new Vector3(from.x, newY, from.z);
        Vector3 delta = to - from;

        // ORDER MATTERS. Gather riders while they are still standing on the
        // floor's OLD position - do it after the move and anyone the car is
        // dropping away from has already been left behind by a frame.
        GatherRiders();

        rb.MovePosition(to);

        foreach (var r in riders)
        {
            if (r == null) continue;

            // Assigning .position rather than MovePosition, deliberately.
            // MovePosition on a dynamic body derives a velocity from the
            // move, which would fight PlayerMotor's own acceleration budget
            // and fling the player. Setting position is a teleport: the
            // rider keeps its own velocity and simply arrives where the floor
            // put it. The step is at most 0.16m at full speed, far too small
            // to tunnel through anything.
            r.position += delta;
        }

        if (Mathf.Approximately(newY, target))
        {
            IsMoving = false;
            useFast = false;
            CurrentFloor = TargetFloor;
        }
    }

    /// <summary>
    /// Everything with a Rigidbody standing inside the car - players, loot,
    /// and later survivors.
    ///
    /// An overlap query rather than a trigger volume on purpose: a trigger
    /// would be one more collider inside a car that already has thirty, and
    /// PlayerCarry's pickup SphereCast reaches through this space every
    /// frame. Querying costs one box test per physics step and adds nothing
    /// for anything else to trip over.
    /// </summary>
    void GatherRiders()
    {
        riders.Clear();

        Vector3 centre = transform.TransformPoint(new Vector3(0f, rideHeight * 0.5f, 0f));
        Vector3 half = new Vector3(rideHalfXZ, rideHeight * 0.5f, rideHalfXZ);

        int n = Physics.OverlapBoxNonAlloc(centre, half, Overlap, transform.rotation,
                                           ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            var other = Overlap[i].attachedRigidbody;
            if (other == null) continue;
            if (other == rb) continue;          // the car's own thirty colliders
            if (other.isKinematic) continue;    // held loot is kinematic; its carrier moves it
            if (riders.Contains(other)) continue;
            riders.Add(other);
        }
    }

    // ------------------------------------------------------------------
    // DOORS
    //
    // Only the active side ever opens, and only while stopped. Everything
    // else stays shut, which is what makes a four-sided box readable: the
    // one gap in the wall is the way out, and it moves per floor.
    // ------------------------------------------------------------------

    void DriveShutters()
    {
        float dt = Time.deltaTime * shutterSpeed;

        foreach (var s in shutters)
        {
            float want = (s == active && !DoorsLocked) ? 1f : 0f;
            s.open = Mathf.MoveTowards(s.open, want, dt);

            s.t.localPosition = Vector3.Lerp(s.closedPos, s.openPos, s.open);
            s.t.localScale = Vector3.Lerp(s.closedScale, s.openScale, s.open);
        }
    }

    // ------------------------------------------------------------------
    // THROWAWAY HUD.
    //
    // Step 6 replaces every line of this with the real dashboard. It exists
    // now for one reason: without it there is no way to tell a lift that is
    // refusing to move from a lift that never received the keypress, and
    // "nothing happened" is the least debuggable sentence in games.
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        style.normal.textColor = IsMoving
            ? new Color(1f, 0.75f, 0.3f)
            : new Color(0.55f, 0.9f, 1f);

        string state = IsMoving
            ? $"MOVING  {CurrentFloor} -> {TargetFloor}"
            : $"FLOOR {CurrentFloor}" + (CurrentFloor == 0 ? "  (surface)" : "");

        GUI.Label(new Rect(24f, Screen.height - 74f, 520f, 22f),
                  $"ELEVATOR   {state}", style);

        var hint = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        hint.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
        GUI.Label(new Rect(24f, Screen.height - 52f, 520f, 22f),
                  $"PageUp  go up          PageDown  go down          " +
                  $"doors {(DoorsLocked ? "LOCKED" : "open")}", hint);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, rideHeight * 0.5f, 0f),
                            new Vector3(rideHalfXZ * 2f, rideHeight, rideHalfXZ * 2f));
    }
}
