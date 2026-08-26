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

        Debug.Log($"[Net] elevator is {(IsServer ? "HOST-DRIVEN" : "drawing the host's trips")}");
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
