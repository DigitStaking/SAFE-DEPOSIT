// ElevatorNet.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/ElevatorNet.cs
// Goes on: the ELEVATOR object, beside Elevator and ElevatorBridge.
//
// ====================================================================
// PHASE 4 STEP 5 - THE LIFT. PHASE4_SPEC calls this the hard one.
//
// WHAT WAS ACTUALLY WRONG
//
// Nothing in Elevator, ElevatorBridge, ElevatorDashboard or RunManager
// contains the word "Netcode". Not one line. So there were never two people
// in one elevator - there were TWO ELEVATORS, each simulating happily on its
// own machine, and pressing RETURN in one window sent that window's car to the
// surface while the other stood in a room on floor 1 wondering what happened.
// Reported exactly that way.
//
// ONE CAR DECIDES. EVERY MACHINE CARRIES ITS OWN RIDERS.
//
// This is the part worth understanding, because it is not the obvious design.
//
// The obvious design is: replicate the car, and let the physics on each
// machine sort out the people standing on it. That does not work, and Part 3
// of the spec flagged it in advance. Elevator does not push its riders with
// friction - it CANNOT, that was tried and the comment at line 336 records
// what happened. It teleports them by exactly the distance the car moved,
// every physics step, so no penetration is ever created for the solver to
// argue about.
//
// That teleport has to keep happening on every machine. And it cannot be
// replicated, because a rider's body is OWNER-AUTHORITATIVE - your machine is
// the only one allowed to move you. If the host moved your body down the
// shaft, NetworkTransform would fight it back up every frame, and that is
// precisely the rubber-banding the done-when forbids.
//
// So the split is:
//
//   THE HOST decides. State machine, target floor, doors, and where the car
//   physically is. Clients do not simulate the car at all.
//
//   EVERY MACHINE carries whoever is standing on ITS copy of the car, using
//   the distance the car ACTUALLY MOVED since the last physics step. On the
//   host that is the distance it just chose to move. On a client it is the
//   distance that arrived over the wire. Same number, same teleport, same
//   code path - and the only body each machine touches is one it owns.
//
// Nobody is corrected, so nobody rubber-bands. The car is the single source
// of truth for WHERE THE FLOOR IS, and each machine answers "and therefore
// where am I" for itself.
//
// A BUTTON PRESS IS A REQUEST
//
// Same shape as Step 3's shop. ElevatorBridge.RequestGoToFloor was already
// the one and only way anything commanded this car - the dashboard, the
// buttons and the debug keys all go through it, and Elevator.GoToFloor is
// called from nowhere else in the project. So there is exactly one place to
// put the redirect, which is the second time this phase that Phase 1 and 2
// architecture has paid for itself.
// ====================================================================

using Unity.Netcode;
using UnityEngine;

public class ElevatorNet : NetworkBehaviour
{
    public static ElevatorNet Instance { get; private set; }

    /// <summary>
    /// True when this machine decides what the car does: always offline,
    /// host only online. Asked live, never cached - the sixth time that has
    /// been the right call today.
    /// </summary>
    public static bool Decides
    {
        get
        {
            if (Instance != null) return Instance.IsServer;

            // ---- THE GAP BEFORE THIS COMPONENT EXISTS ----
            //
            // The editor log settled the order: a joining client spawns its
            // PLAYER before the elevator arrives.
            //
            //     [Net] spawned Player 2 (me)  owner=True  local=True
            //     [Net] elevator is following the host
            //
            // For those frames Instance is null, and "null means offline"
            // would have told a connected client it was the authority - so it
            // would simulate its own car for a moment before being told it
            // does not get to. Brief, but it is the two-elevator bug in
            // miniature, and it happens on every single join.
            //
            // So ask the NetworkManager instead when the component is not up
            // yet. Genuinely offline there is no manager and nothing is
            // listening, and the answer is still yes.
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.IsServer;
        }
    }

    static NetworkVariableWritePermission Host => NetworkVariableWritePermission.Server;

    public readonly NetworkVariable<int>  Current = new NetworkVariable<int>(0, default, Host);
    public readonly NetworkVariable<int>  Target  = new NetworkVariable<int>(0, default, Host);
    public readonly NetworkVariable<bool> Moving  = new NetworkVariable<bool>(false, default, Host);

    /// <summary>
    /// ElevatorBridge.State as an int.
    ///
    /// The bridge mostly takes care of itself: it starts EXTENDING when the
    /// car stops, and "the car stopped" is Moving above, which every machine
    /// already receives. What a client cannot work out on its own is the
    /// other half - the five-second warning and the retract - because those
    /// begin with a BUTTON PRESS, and on a client the press left for the host
    /// instead of starting anything locally.
    ///
    /// Without this the client's bridge stays cheerfully extended while the
    /// car drops out from under it. Which is worse than it sounds: the bridge
    /// is the thing that is supposed to make leaving late frightening, and a
    /// player watching a bridge that never retracts has been told a lie about
    /// how much time they have.
    /// </summary>
    public readonly NetworkVariable<int> Bridge = new NetworkVariable<int>(0, default, Host);

    /// <summary>Which speed the current trip is using. Part of the movement
    /// recipe, so every machine needs it to draw the same descent.</summary>
    public readonly NetworkVariable<bool> Fast = new NetworkVariable<bool>(false, default, Host);

    /// <summary>
    /// The host's actual car height. NOT how the car moves - how the car is
    /// CORRECTED, if a client's own simulation ever drifts.
    /// </summary>
    public readonly NetworkVariable<float> CarY = new NetworkVariable<float>(0f, default, Host);

    public override void OnNetworkSpawn()
    {
        Instance = this;

        // ---- NO NetworkTransform ON THE CAR ----
        //
        // The car is not streamed any more, it is REPRODUCED: every machine
        // runs the same MoveTowards from the same replicated target, so every
        // machine gets the same clean 0.16m per step. A NetworkTransform would
        // write the position on top of that at network-tick rate and hand back
        // the exact interpolation noise this replaced - the +0.000, +0.171,
        // -0.182 sequence that was being teleported into people's bodies.
        //
        // Switched off here as well as removed by the builders, because the
        // scene file is the one thing in this project a script cannot safely
        // edit while Unity has it open.
        foreach (var t in GetComponents<Unity.Netcode.Components.NetworkTransform>())
        {
            if (!t.enabled) continue;
            t.enabled = false;
            Debug.Log("[Net] disabled a NetworkTransform on the elevator - the " +
                      "car is simulated on every machine, not streamed.");
        }

        // ---- THE HOST BRINGS ITS LIFT WITH IT ----
        //
        // Every one of these starts at 0, and 0 means "parked at the surface,
        // going nowhere". So hosting told the whole session the car was at the
        // top no matter where it actually was - and the dashboard refused
        // RETURN with ALREADY AT SURFACE while the crew stood on floor 1.
        //
        // Reported as exactly that, and it is the same omission twice: Step 3
        // carries the campaign in on OnNetworkSpawn and Step 4 carries each
        // Crew row in, and the lift got the replication and not the handover.
        //
        // Read from the RAW locals, not the properties - by this line Instance
        // is already set, so the properties would answer from the network, and
        // the network is the blank slate we are trying to fill.
        if (IsServer)
        {
            var lift = SceneRefs.Lift;
            if (lift != null)
            {
                Current.Value = lift.RawCurrentFloor;
                Target.Value = lift.RawTargetFloor;
                Moving.Value = lift.RawMoving;
                Fast.Value = lift.RawFast;
                CarY.Value = lift.CarWorldY;
            }
        }

        Debug.Log($"[Net] elevator is {(IsServer ? "HOST-DRIVEN" : "drawing the host's trips")}" +
                  $" - floor {Current.Value}, target {Target.Value}, moving {Moving.Value}");
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    // ================================================================
    // RIDERS ARE NOT PARENTED. THIS IS THE SECOND ATTEMPT AND IT IS SHORTER.
    //
    // Parenting was meant to give NetworkTransform a frame of reference, so a
    // rider sent "where am I in the car" instead of "where am I in the world"
    // and could not arrive late. Sound idea. It cannot be done this way,
    // because parenting a DYNAMIC RIGIDBODY perturbs physics, and it perturbs
    // it differently depending on when Unity happens to sync transforms.
    //
    // Both failure modes are on record, and they are opposites:
    //
    //   WITH the teleport skipped for parented riders, the body ignored its
    //   parent entirely - a Rigidbody's own pose wins - so the car descended
    //   and the body stood still in the world. Reported as "if i go down with
    //   elevator me and my friend going up".
    //
    //   WITH the teleport restored, the parent-moved transform got synced
    //   INTO the body and the teleport was applied on top. Down twice, into
    //   the floor, and the solver ejected the body upward. The audit caught
    //   it mid-bounce: GAP=+0.94 and growing, velY=-8.99 and accelerating,
    //   under=Floor@0.98m - carried and falling at the same time - with
    //   myY and rbY disagreeing by 0.19m, which is exactly one step of car
    //   travel. The transform had moved and the body had not yet agreed.
    //
    // So the parent either does nothing or does it twice, and which one
    // depends on frame timing. That is not a mechanism, it is a coin toss.
    //
    // The explicit teleport alone is known-good: the host has never once
    // logged a ride fault, in any version of this bug.
    //
    // The lag it was meant to solve is real and still unsolved - a teammate
    // renders where the floor was about 100ms ago. That is a SMOOTHNESS
    // problem. Being thrown out of a descending lift is a CORRECTNESS one,
    // and trading the second for the first was a bad deal. Smoothness gets
    // its own attempt, without touching the physics.
    // ================================================================

    // ================================================================
    // A TEAMMATE'S HEIGHT ABOVE THE DECK, NOT THEIR HEIGHT IN THE WORLD.
    //
    // The last thing left, and it is now precisely described: your own body
    // rides correctly, and the OTHER one appears to fly or sink - and it looks
    // that way from both machines at once. Each machine carries its own rider
    // properly; what it cannot do is un-delay somebody else's.
    //
    // A remote body's position arrives in world space, about 100ms old. While
    // the car travels at 8m/s that is 80cm, so their body renders where the
    // floor WAS. Nothing is broken and no smoothing helps, because the number
    // is right and simply late.
    //
    // Parenting was the textbook answer and it failed twice - see the note
    // further up. So this does the same job arithmetically, on the one axis
    // that needs it:
    //
    //   WHILE THE CAR IS STILL, measure how high each remote body stands
    //   above the deck. There is no lag error to speak of when nothing is
    //   moving, so this number is trustworthy.
    //
    //   WHILE THE CAR MOVES, hold that height and render them at deck + h.
    //
    // X and Z are left completely alone, and that is not laziness: the shaft
    // is vertical, the car never moves sideways, so a late horizontal
    // position is not late at all. Only Y ever needed correcting.
    //
    // The cost is honest and small: a teammate who jumps or crouches DURING a
    // trip will hold their previous height until the car stops. Standing
    // still in a moving lift is what people actually do, and a frozen crouch
    // for four seconds is a far better trade than a friend who floats out
    // through the ceiling every time you press a button.
    //
    // LateUpdate, because NetworkTransform writes during Update - a
    // correction applied any earlier is simply overwritten.
    // ================================================================

    /// <summary>How long the car has been stopped. The stream keeps
    /// delivering trip-era positions for a moment after it does.</summary>
    float stillFor;

    readonly System.Collections.Generic.Dictionary<Rigidbody, float> deckHeights =
        new System.Collections.Generic.Dictionary<Rigidbody, float>();

    void LateUpdate()
    {
        if (!IsSpawned) return;

        var lift = SceneRefs.Lift;
        if (lift == null) return;

        stillFor = lift.IsMoving ? 0f : stillFor + Time.deltaTime;

        float deck = lift.transform.position.y + lift.StandLocalY;

        foreach (var r in lift.Riders)
        {
            if (r == null) continue;

            // Mine is already right - this machine carried it. Touching it
            // here would fight the physics that just got it correct.
            var no = r.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned || no.IsOwner) continue;

            float measured = r.transform.position.y - deck;

            // ALWAYS RENDERED AT deck + h. THE HEIGHT EASES; THE POSITION
            // NEVER JUMPS.
            //
            // The first version simply stopped correcting when the car
            // stopped, which handed the body straight back to its raw network
            // position - still about 80cm stale, in the direction of travel.
            // So it popped, and the direction was the giveaway: stop after
            // going DOWN and the stale position is ABOVE the deck, so they
            // appear to jump; stop after going UP and they dip. Reported
            // exactly that way round.
            //
            // There is no moment of handover now. The body is always drawn at
            // deck + h; only h changes, and while the car is still it EASES
            // toward what is actually arriving. A real crouch or jump still
            // reads, about a tenth of a second behind, and the catch-up after
            // a trip is a glide instead of a snap.
            if (!deckHeights.TryGetValue(r, out float h)) h = measured;

            // WAIT FOR THE STREAM TO CATCH UP BEFORE BELIEVING IT AGAIN.
            //
            // Easing began the instant the car stopped, and for the first
            // tenth of a second after that, what is still arriving was SENT
            // DURING THE TRIP - stale by the height of one interpolation
            // buffer. So h chased a wrong value, then came back, and that
            // round trip is the small dip and the small hop that were left.
            //
            // stillFor holds h steady until the tail has passed. It costs a
            // quarter second of a teammate not being able to crouch after a
            // ride, and it removes the last visible artefact of the whole
            // step.
            // GENTLY JUST AFTER A TRIP, BRISKLY AFTERWARDS.
            //
            // What is left is not a bug any more, it is the noise floor. When
            // the car stops, the teammate's body settles onto the deck on
            // THEIR machine - a real, small, physical settle - and it reaches
            // here a tenth of a second later. Shown at full speed it reads as
            // a dip.
            //
            // So for the first second and a half after a trip the height eases
            // slowly, which turns that settle into a glide nobody notices.
            // After that it goes back to being responsive, so a crouch or a
            // jump while standing around still reads promptly.
            //
            // This is smoothing, and it is worth being honest about the
            // difference: everything before this commit removed a wrong
            // position. This one renders a right position more kindly. There
            // is no third fix hiding behind it - below this is just latency,
            // and latency is not a defect.
            if (!lift.IsMoving && stillFor > 0.25f)
            {
                float rate = stillFor < 1.5f ? 3f : 12f;
                h = Mathf.Lerp(h, measured, 1f - Mathf.Exp(-rate * Time.deltaTime));
            }

            deckHeights[r] = h;

            var p = r.transform.position;
            r.transform.position = new Vector3(p.x, deck + h, p.z);
        }
    }

    /// <summary>
    /// A client asking for a floor.
    ///
    /// The host re-runs the whole request through ElevatorBridge, not through
    /// Elevator - so the bridge still gets retracted first, the doors still
    /// close in order, and a request for the floor you are already on is still
    /// refused. Every rule the offline lift has, a client's press goes through
    /// unchanged. It is the same method; only the machine running it differs.
    ///
    /// RequireOwnership = false: the ELEVATOR belongs to nobody, and it is a
    /// client that needs to call this.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestFloorServerRpc(int floor, bool fast)
    {
        var bridge = SceneRefs.Lift != null
            ? SceneRefs.Lift.GetComponent<ElevatorBridge>()
            : null;

        if (bridge != null) bridge.RequestGoToFloor(floor, fast);
        else Debug.LogWarning("[Net] a client asked for a floor but the host " +
                              "has no ElevatorBridge to ask.");
    }
}
