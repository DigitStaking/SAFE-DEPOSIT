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
// THE FLOOR MOVES BEFORE THE PEOPLE ON IT.
//
// Reported as "hard to walk in elevator" while it travels. Both scripts ran
// their FixedUpdate in undefined order, so on some steps PlayerMotor moved
// and checked its footing FIRST, against a floor that had not descended yet,
// and on others the floor went first. The ground check flickered, and walking
// on a flickering floor is exactly as awkward as it sounds.
//
// A negative order puts the car first, every step, on every machine. The
// floor is always already where it is going to be by the time anybody tries
// to stand on it.
[DefaultExecutionOrder(-50)]
public class Elevator : MonoBehaviour
{
    [Header("Shaft")]
    [Tooltip("World Y of floor 0, the surface. Floor N sits floorHeight below it.")]
    public float surfaceY = 0f;

    [Tooltip("Must match GrayboxBuilder.FloorHeight and Campaign.FloorHeight, " +
             "or the car stops between floors.")]
    public float floorHeight = 5f;

    [Tooltip("Deepest floor that exists. Must match GrayboxBuilder.LevelCount.")]
    public int lowestFloor = 20;

    [Header("Speed")]
    [Tooltip("UP / DOWN, one floor, deliberate.")]
    public float slowSpeed = 2f;

    [Tooltip("Type a floor and press GO. Used from Step 6.")]
    public float fastSpeed = 8f;

    [Header("Doors")]
    [Tooltip("Which side currently faces the room. DERIVED on arrival from " +
             "the level's own rotation in the shaft - shown here so you can " +
             "see it, not so you can set it. Only used as a fallback at the " +
             "surface, where there is no room to face.")]
    public string activeSide = "Side_East";

    [Tooltip("How far a shutter has rolled, in metres, when fully open.")]
    public float shutterRoll = 0.2f;

    public float shutterSpeed = 2.5f;

    // ---- what the dashboard will read ----
    // ================================================================
    // PHASE 4 STEP 5 - THREE ANSWERS THAT NOW COME FROM THE HOST.
    //
    // These were plain auto-properties and every machine computed its own.
    // Which is how one window rode to the surface while the other stood in a
    // room on floor 1: there was never one lift with two people in it, there
    // were two lifts.
    //
    // Same trick as Campaign in Step 3 - the names do not change, so the
    // dashboard, the bridge, the deck, the cable gauge and RunManager all go
    // on reading exactly what they always read. Offline the private fields
    // answer and nothing about the solo game is different.
    // ================================================================

    static ElevatorNet Net => ElevatorNet.Instance;

    int localCurrent, localTarget;
    bool localMoving;

    // ---- WHAT THIS MACHINE THINKS, IGNORING THE NETWORK ----
    //
    // ElevatorNet seeds itself from these the moment it spawns. Without them
    // it would have to read the properties above, which by that point already
    // answer from the network - and the network answers 0, because nothing has
    // written it yet. The lift would seed itself from its own blank slate.
    //
    // Same job as Campaign.PushLocalStateToNetwork in Step 3. I remembered to
    // carry the money in and forgot to carry the lift.
    internal int RawCurrentFloor => localCurrent;
    internal int RawTargetFloor => localTarget;
    internal bool RawMoving => localMoving;
    internal bool RawFast => localFast;
    internal float CarWorldY => rb != null ? rb.position.y : transform.position.y;

    public int CurrentFloor
    {
        get => Net != null ? Net.Current.Value : localCurrent;
        private set { if (Net != null) { if (Net.IsServer) Net.Current.Value = value; } else localCurrent = value; }
    }

    public int TargetFloor
    {
        get => Net != null ? Net.Target.Value : localTarget;
        private set { if (Net != null) { if (Net.IsServer) Net.Target.Value = value; } else localTarget = value; }
    }

    public bool IsMoving
    {
        get => Net != null ? Net.Moving.Value : localMoving;
        private set { if (Net != null) { if (Net.IsServer) Net.Moving.Value = value; } else localMoving = value; }
    }

    /// <summary>Doors are locked whenever the car is not stopped at a floor.</summary>
    public bool DoorsLocked => IsMoving;

    /// <summary>
    /// The floor the car is PHYSICALLY passing right now, for the panel.
    ///
    /// CurrentFloor only updates on arrival - it is where the car IS in the
    /// state-machine sense, and every gate in the game correctly asks it
    /// "which floor are you parked at". But a real lift counts the floors off
    /// as it passes them, and a panel that reads 01 the whole way from 1 to
    /// 12 gives a crew watching the collapse clock nothing at all.
    /// </summary>
    public int DisplayFloor => IsMoving ? FloorAt(rb.position.y) : CurrentFloor;

    /// <summary>
    /// Everyone and everything GatherRiders found inside the car THIS
    /// physics step. Step 8's ElevatorDeck reads this to count crew mass
    /// rather than running a second overlap query for the same answer.
    /// </summary>
    public IReadOnlyList<Rigidbody> Riders => riders;

    Rigidbody rb;

    // Ride volume, derived from the car's own geometry rather than restated
    // here. ElevatorBuilder owns the dimensions; this only has to find them.
    float rideHalfXZ = 2f;
    float rideHeight = 2.8f;

    readonly List<Rigidbody> riders = new List<Rigidbody>();

    /// <summary>Where the car was at the end of the last physics step. On a
    /// client this is the only way to know how far the host moved it.</summary>
    Vector3 lastCarPosition;

    /// <summary>The floor the active door was last chosen for. See
    /// FixedUpdate - the trigger is learning a new floor, not arriving
    /// at one.</summary>
    int sideFloor = int.MinValue;

    /// <summary>How far a client's own simulation may drift from the host's
    /// car before it stops trusting itself. Two floors' worth of travel is
    /// far more than interpolation noise and far less than a real desync.</summary>
    const float DriftSnap = 1.5f;
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

        // BASELINE IT NOW, NOT AT (0,0,0).
        //
        // A Vector3 field starts at the origin, and on a client the first
        // FixedUpdate computed "how far did the car move" as
        // rb.position - (0,0,0) - which is not a delta at all, it is the car's
        // ENTIRE WORLD POSITION. At floor 3 that is fifteen metres, applied to
        // everybody aboard, in one physics step. They were fired out of the
        // lift on the frame they joined.
        //
        // The host never saw it because its own path re-baselines every step
        // from the very first one, which is exactly why the report was "the
        // host is okay" - and that sentence is what identified this.
        lastCarPosition = rb.position;

        // Kinematic: this is a platform, not a falling object. A dynamic
        // elevator would sag under the weight of the crew standing in it,
        // which is a lovely idea and a completely different system.
        rb.isKinematic = true;
        rb.useGravity = false;

        // Interpolation OFF, deliberately - see the ordering note in
        // FixedUpdate. The car is teleported rather than swept, and
        // interpolating a teleport just smears it.
        rb.interpolation = RigidbodyInterpolation.None;

        MeasureRideVolume();

        // Work out which floor we were placed on BEFORE collecting shutters -
        // CollectShutters picks the door to open, and it can only do that
        // once it knows where the car is.
        CurrentFloor = TargetFloor = FloorAt(rb.position.y);
        SnapToFloor(CurrentFloor);

        CollectShutters();
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

        // ---- WHERE THE STANDING SURFACE ACTUALLY IS ----
        //
        // ElevatorBuilder authors the floor's TOP FACE at local y = 0 and says
        // so: "y = 0 is therefore the surface a player stands on". Everything
        // written since has trusted that - the spawn corners are placed 0.2
        // above the root, and the ride volume is measured from the root.
        //
        // The scene disagrees. Standing still in SINGLE PLAYER, with no
        // networking involved at all, the audit found:
        //
        //     GAP=+1.20   under=Floor@1.30m   velY=0.00
        //
        // Feet at +1.20 with the floor 1.30 below them. The car in the scene
        // is simply not the car the builder describes - authored by an older
        // version of it, or moved by hand - and every constant measured from
        // the root has been off by that much ever since.
        //
        // So it is MEASURED now, not assumed. Whatever the scene contains,
        // this finds the top of the thing people stand on, and the spawn and
        // the rider volume both hang off it. A number the geometry can be
        // asked for should never be a constant written down twice.
        if (floor != null)
        {
            float topWorld = floor.position.y + floor.lossyScale.y * 0.5f;
            standLocalY = topWorld - transform.position.y;
        }
    }

    /// <summary>
    /// Height of the car's standing surface, in the elevator root's own space.
    /// Measured from the geometry - see MeasureRideVolume.
    /// </summary>
    public float StandLocalY => standLocalY;

    float standLocalY;

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
        }

        UpdateActiveSide();
    }

    // ------------------------------------------------------------------
    // WHICH SIDE FACES THE ROOM
    //
    // GrayboxBuilder always cuts a level's doorway in that level's LOCAL +X,
    // then rotates the whole level - 0, 90, 180, 270 - so which way the door
    // actually faces changes floor by floor. That is the design: arriving
    // somewhere should mean orienting yourself.
    //
    // The car does not turn. A different shutter opens instead.
    //
    // Read the level's rotation out of the scene rather than restating
    // GrayboxBuilder's table here. A copy of that array in this file would be
    // wrong the first time anyone edited the other one, and the failure -
    // opening onto a blank wall - gives no hint which copy lied. This way the
    // shaft is the single source of truth and Step 11 can rearrange floors
    // freely without touching the elevator at all.
    // ------------------------------------------------------------------

    /// <summary>
    /// Recompute the active door if this machine has learned a new floor
    /// since the last time it did.
    ///
    /// Public because ElevatorBridge has to be able to ask before it goes
    /// looking for a deck. The bridge reacts to IsMoving turning false, and
    /// that is an Update; this runs in FixedUpdate. On a frame where those two
    /// land the wrong way round the bridge would extend toward the side the
    /// PREVIOUS floor used - the same mistake as before, one frame wide
    /// instead of permanent, which is worse to find.
    /// </summary>
    public void EnsureActiveSideForCurrentFloor()
    {
        if (CurrentFloor == sideFloor) return;

        sideFloor = CurrentFloor;
        UpdateActiveSide();
    }

    void UpdateActiveSide()
    {
        if (shutters.Count == 0) return;

        var shaft = GameObject.Find("SHAFT");
        var level = shaft != null
            ? shaft.transform.Find($"Level_{CurrentFloor:00}")
            : null;

        if (level == null)
        {
            // Floor 0 is the surface and has no room. Fall back to the
            // inspector's side so the doors still open rather than sealing
            // the crew in a box with no explanation.
            active = shutters.Find(s => s.t.parent.name == activeSide);
            return;
        }

        // The doorway's outward direction in world space.
        Vector3 doorDir = level.rotation * Vector3.right;

        // Each Side_ group was authored facing +Z and placed by a yaw, so its
        // forward IS its outward normal - no lookup table needed.
        Shutter best = null;
        float bestDot = -2f;

        foreach (var s in shutters)
        {
            float d = Vector3.Dot(s.t.parent.forward, doorDir);
            if (d > bestDot) { bestDot = d; best = s; }
        }

        active = best;
        if (best != null) activeSide = best.t.parent.name;
    }

    // ------------------------------------------------------------------
    // DEBUG INPUT
    //
    // PageUp/PageDown were the Step 4 stand-in for the dashboard's UP and
    // DOWN, from before the dashboard existed. They are now clamped to
    // floor 1 as well: they call GoToFloor DIRECTLY, skipping the bridge's
    // retract warning AND the departure checks, so leaving them able to
    // reach floor 0 made them a debug key that could silently end a run.
    //
    // Kept rather than deleted - being able to move the car without walking
    // to the panel is genuinely useful while building the next steps - but
    // they can no longer do anything the dashboard would have refused.
    // ------------------------------------------------------------------

    void Update()
    {
        // A DEBUG SHORTCUT BELONGS TO A PERSON TOO. The lift is a world
        // object with no owner, so it borrows the local player's keyboard -
        // and gets null when nobody is holding one, which is the correct
        // amount of control for a body that is not being driven.
        var driver = PlayerRegistry.Local;
        var kb = driver != null ? driver.Keys : null;
        if (kb == null) { DriveShutters(); return; }

        if (kb.pageUpKey.wasPressedThisFrame && TargetFloor > 1) GoUp();
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

    bool localFast;
    bool useFast
    {
        get => Net != null ? Net.Fast.Value : localFast;
        set { if (Net != null) { if (Net.IsServer) Net.Fast.Value = value; } else localFast = value; }
    }

    public float FloorY(int floor) => surfaceY - floorHeight * floor;

    int FloorAt(float y) => Mathf.Clamp(
        Mathf.RoundToInt((surfaceY - y) / floorHeight), 0, lowestFloor);


    // ==================================================================
    // A PARENT DOES NOT CARRY A DYNAMIC RIGIDBODY. THE TELEPORT DOES.
    //
    // The last commit parented riders to the car so their position would
    // replicate as an offset, and then skipped the teleport for anyone
    // parented, on the reasoning that the transform hierarchy was already
    // moving them.
    //
    // It is not. A non-kinematic Rigidbody simulates in WORLD space and its
    // own pose is what counts - move its parent and the physics engine
    // overwrites the transform back on the next step, as if nothing had
    // happened. So the skip removed the only thing that was actually
    // carrying anybody.
    //
    // The visible result was precise and is worth recording, because it names
    // the cause exactly: the car descends, the body does not, so relative to
    // the car THE BODY RISES. Reported as "if i go down with elevator me and
    // my friend going up". Opposite to the direction of travel, both players,
    // every time - not a lag or a fight, a body simply standing still in the
    // world while the room left.
    //
    // So the teleport is back for everyone. Parenting stays, because it does
    // the OTHER half of the job perfectly well: it gives NetworkTransform a
    // frame of reference, so what crosses the wire is "where am I in the car"
    // rather than "where am I in the world". Two jobs, two mechanisms:
    //
    //   the TELEPORT moves the body      (physics)
    //   the PARENT decides what gets sent (replication)
    //
    // Conflating them cost one round trip. They do not overlap and neither
    // one can do the other's work.
    // ==================================================================


    /// <summary>
    /// Move a rider by the same distance the car just moved - BODY AND
    /// TRANSFORM BOTH.
    ///
    /// Writing only rb.position looks complete and is not. Rigidbody.position
    /// moves the physics body; the TRANSFORM does not catch up until the
    /// simulation runs, later in the same step. And PlayerMotor's ground check
    /// reads transform.position:
    ///
    ///     Vector3 origin = transform.position + Vector3.up * capsule.radius;
    ///
    /// So with the car ordered first, the motor was casting from where the
    /// body used to be, at a floor that had already moved. Going UP the origin
    /// sat 16cm low - level with or inside the floor it was trying to find -
    /// the cast found nothing, grounded came back false, and you could not
    /// jump. Reported exactly that way, and only going up, which is the tell:
    /// down, the ray merely got longer and the check went flaky instead of
    /// failing outright. That was the "hard to walk".
    ///
    /// Both written, so everything reading either one agrees within the step.
    /// </summary>
    static void CarryRider(Rigidbody r, Vector3 delta)
    {
        // Teleport, not MovePosition: the rider keeps its own velocity and
        // simply arrives where the floor put it, so you can still walk and
        // jump normally on a moving lift. At most 0.16m a step, far too small
        // to tunnel through anything.
        r.position += delta;
        r.transform.position = r.position;
    }

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
        // Gathered every physics step now, moving or not - Step 8's
        // ElevatorDeck needs a live "who is inside right now" answer while
        // the car is sitting still at a floor, not only during the seconds
        // it happens to be travelling. This used to live further down,
        // inside the IsMoving block; the ORDER relative to the position
        // update below is unchanged (still gathered before the move), only
        // WHETHER it also runs on a stationary frame is new.
        GatherRiders();

        // ---- WHICH WALL OPENS IS DECIDED BY THE FLOOR, NOT BY ARRIVING ----
        //
        // UpdateActiveSide is deterministic: it takes CurrentFloor, finds that
        // level, and picks the shutter whose outward normal best matches the
        // doorway. Same floor in, same door out, on any machine.
        //
        // But it was CALLED at the moment a machine finished moving, and the
        // two machines do not learn the new floor at the same instant. The
        // host sets CurrentFloor and then computes the side. A client reaches
        // the target a frame earlier or later and computes the side from the
        // floor it still thinks it is on - so the host opened SOUTH for
        // floor 2 while the client opened EAST for floor 1, and each crew
        // stood at a bridge the other could not see.
        //
        // Nothing was wrong with the choice. It was made from the wrong floor.
        //
        // So the trigger is the FLOOR CHANGING rather than the travelling
        // stopping. Every machine recomputes when, and only when, it learns
        // it is somewhere new - which for a client is the moment the number
        // arrives, whenever that is. Offline the two are the same instant and
        // nothing changes.
        EnsureActiveSideForCurrentFloor();

        // ==============================================================
        // ONE CAR DECIDES. EVERY MACHINE CARRIES ITS OWN RIDERS.
        //
        // A client does not simulate this car - the host does, and the
        // result arrives over the wire. But the CARRY still has to happen
        // here, on every machine, and it cannot be replicated.
        //
        // Riders are not pushed by friction. They cannot be; that was tried
        // and the comment further down records exactly what went wrong. They
        // are TELEPORTED by precisely the distance the car moved, every
        // physics step, so the solver is never handed a penetration to argue
        // about.
        //
        // And a rider's body is OWNER-AUTHORITATIVE. Your machine is the only
        // one allowed to move you. If the host teleported your body down the
        // shaft, NetworkTransform would drag it back up every frame - which
        // is precisely the rubber-banding this step is not allowed to have.
        //
        // So: the host decides where the floor is, and each machine answers
        // "and therefore where am I" for itself, using the distance the car
        // ACTUALLY MOVED since the last step. On the host that is the
        // distance it just chose. On a client it is the distance that
        // arrived. Same number, same teleport, same code below - and the only
        // body any machine touches is one it owns.
        // ==============================================================
        // ==============================================================
        // EVERY MACHINE DRAWS THE SAME DESCENT. IT DOES NOT WATCH ONE.
        //
        // Clients used to take the car's position from NetworkTransform and
        // carry riders by however far it had jumped since the last physics
        // step. The car travels a clean 0.16m per step. Here is what one
        // client actually observed over a single descent:
        //
        //     +0.000 (x12)  -0.111  -0.114  -0.161  -0.164  -0.182  +0.171
        //
        // Nothing, then a double step, then a step BACKWARDS - all while the
        // car was descending steadily. That is not the lift misbehaving, it
        // is what an interpolated stream looks like when you sample it from
        // FixedUpdate: the network ticks and the physics steps do not line
        // up, so some steps get two updates and some get none. Teleporting a
        // body by that noise IS the vibration.
        //
        // But the car's motion is a RECIPE, not a performance:
        //
        //     MoveTowards(y, FloorY(target), speed * fixedDeltaTime)
        //
        // Target floor, moving, and fast are all replicated. Given the same
        // three, every machine computes the same descent, to the same
        // 0.16m per step, forever - and each one carries its own riders by a
        // clean number instead of a sampled one.
        //
        // The host still DECIDES - which floor, when to leave, when to stop.
        // Clients only draw. That distinction is the whole reason this is not
        // a return to the two-elevator bug: nobody else gets an opinion about
        // where the car should go, only about how to animate getting there.
        //
        // CarY is the correction. If a client's simulation ever drifts - a
        // long stall, a join mid-trip - it snaps and re-baselines WITHOUT
        // carrying riders that distance, because a snap is news, not travel.
        // ==============================================================
        if (!ElevatorNet.Decides && ElevatorNet.Instance != null)
        {
            float hostY = ElevatorNet.Instance.CarY.Value;
            if (Mathf.Abs(rb.position.y - hostY) > DriftSnap)
            {
                var p = rb.position;
                rb.position = new Vector3(p.x, hostY, p.z);
                lastCarPosition = rb.position;
            }
        }

        lastCarPosition = rb.position;

        if (Net != null && Net.IsServer) Net.CarY.Value = rb.position.y;

        if (!IsMoving) return;

        float target = FloorY(TargetFloor);
        float speed = useFast ? fastSpeed : slowSpeed;
        float newY = Mathf.MoveTowards(rb.position.y, target, speed * Time.fixedDeltaTime);

        Vector3 from = rb.position;
        Vector3 to = new Vector3(from.x, newY, from.z);
        Vector3 delta = to - from;

        // ==============================================================
        // THE CAR AND ITS RIDERS MUST MOVE IN THE SAME INSTANT.
        //
        // This was rb.MovePosition(to) and it caused two bugs that looked
        // unrelated: violent vibration going down, and a phantom jump
        // animation going up.
        //
        // MovePosition on a kinematic body is DEFERRED - it is applied during
        // the physics step. Assigning .position on a rider is IMMEDIATE. So
        // inside one FixedUpdate the rider moved and the floor had not yet:
        //
        //   going DOWN  the rider teleported 4cm INTO the floor, the solver
        //               shoved it back out, and only then did the car move.
        //               An upward kick every single frame.
        //
        //   going UP    the rider teleported 4cm ABOVE the floor and fell,
        //               then the car arrived underneath. That momentary
        //               airborne frame with upward velocity is exactly what
        //               PlayerAnimatorDriver watches for, so it fired the
        //               Jump trigger over and over.
        //
        // Rigidbody.position teleports without sweeping. Car and riders both
        // move immediately, by the same delta, so their relative positions
        // never change and no penetration is ever created for the solver to
        // argue with. Nothing pushes anybody.
        // ==============================================================
        rb.position = to;

        // The car has the same body-vs-transform split its riders do, and it
        // matters for the same reason: ElevatorNet computes the deck height
        // from transform.position, and the audit read a steady drift=-0.05
        // between the two - one step of travel, every step. A remote body held
        // at "deck + h" against a deck that is permanently one step behind is
        // held permanently one step wrong.
        transform.position = to;

        foreach (var r in riders)
        {
            if (r == null) continue;

            CarryRider(r, delta);
        }

        if (Mathf.Approximately(newY, target))
        {
            // Only the host DECIDES it has arrived. A client that reached the
            // target floor a frame early simply stops moving and waits to be
            // told - it must never write the state that everyone else reads.
            if (ElevatorNet.Decides)
            {
                IsMoving = false;
                useFast = false;
                CurrentFloor = TargetFloor;
            }

            // No UpdateActiveSide here any more. Writing CurrentFloor above is
            // what triggers it, at the top of the next step, on every machine
            // - including the ones that only find out later.
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

        // Measured from the standing surface, not from the root. When those
        // two are a metre apart - and in this scene they are - a box built
        // from the root starts a metre underground and ends a metre short of
        // the ceiling, so anyone tall enough, or standing on anything, falls
        // out of the rider list and stops being carried.
        Vector3 centre = transform.TransformPoint(
            new Vector3(0f, standLocalY + rideHeight * 0.5f, 0f));
        Vector3 half = new Vector3(rideHalfXZ, rideHeight * 0.5f, rideHalfXZ);

        int n = Physics.OverlapBoxNonAlloc(centre, half, Overlap, transform.rotation,
                                           ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            var other = Overlap[i].attachedRigidbody;
            if (other == null) continue;
            if (other == rb) continue;          // the car's own thirty colliders
            // KINEMATIC MEANS "SOMEBODY ELSE MOVES THIS" - AND WHO THAT IS
            // MATTERS.
            //
            // For held loot it is the carrier, so the car must keep its hands
            // off: that is what this skip was written for and it still holds.
            //
            // But remote PLAYERS are kinematic too now - the network moves
            // them, because local gravity fighting the wire was making them
            // hover. This skip then quietly dropped every teammate out of the
            // rider list, and ElevatorNet's deck-height correction walks that
            // list, so the correction stopped running and a descending
            // teammate went straight back to rendering where the floor used to
            // be. Two fixes of mine, cancelling each other exactly.
            //
            // A remote player is still standing on this floor. It is still a
            // rider. It just is not one that PHYSICS carries.
            if (other.isKinematic && other.GetComponent<PlayerMotor>() == null) continue;
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
        string side = activeSide.StartsWith("Side_") ? activeSide.Substring(5) : activeSide;

        GUI.Label(new Rect(24f, Screen.height - 52f, 700f, 22f),
                  $"F at the panel          " +
                  $"doors {(DoorsLocked ? "LOCKED" : "open " + side.ToUpper())}", hint);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, rideHeight * 0.5f, 0f),
                            new Vector3(rideHalfXZ * 2f, rideHeight, rideHalfXZ * 2f));
    }
}
