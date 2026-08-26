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
    public static bool Decides => Instance == null || Instance.IsServer;

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

    public override void OnNetworkSpawn()
    {
        Instance = this;
        Debug.Log($"[Net] elevator is {(IsServer ? "HOST-DRIVEN" : "following the host")}");
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
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
