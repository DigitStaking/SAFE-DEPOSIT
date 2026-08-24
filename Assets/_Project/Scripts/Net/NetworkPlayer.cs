// NetworkPlayer.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/NetworkPlayer.cs
// Goes on: the PLAYER prefab, alongside NetworkObject and NetworkTransform.
// Added by SAFE DEPOSIT -> Network -> Prepare Player Prefab.
//
// ====================================================================
// PHASE 4 STEP 2 - THE BODY ON THE WIRE.
//
// "Done when: you watch your friend walk, and neither of you is headless."
//
// This file is thirty lines of real work, and that is the whole point of
// Phase 3. Everything it does is hand an answer to a question the game
// already knew how to ask:
//
//     IsOwner        ->  PlayerMotor.MarkLocal
//     OwnerClientId  ->  PlayerMotor.AssignSlot
//
// Two lines. The body cull, the headlamp, the four HUD drawers, the input
// gates, the arm IK - none of them are touched, none of them know a network
// exists, and all of them start behaving correctly the moment those two
// values are right. That is what six steps of "stop assuming there is one of
// everything" bought.
//
// ====================================================================
// THE CREW SLOT WAS ALWAYS A PLACEHOLDER FOR THIS
//
// Phase 3 Step 4 keyed per-player state - HP, the bleed-out clock, the
// backpack - on a slot number, and said outright that it was "the crudest
// thing that works, because a network identity is the real answer and it
// arrives with the network."
//
// This is the network arriving. OwnerClientId IS the identity, and because
// every read and write already went through Crew.Of(slot), swapping what a
// slot MEANS is this one line rather than a hunt through the codebase.
//
// ====================================================================
// OWNER AUTHORITY, NOT SERVER AUTHORITY
//
// NetworkTransform is set to AuthorityModes.Owner: you move your own body and
// everyone else receives it. The server does not correct you.
//
// That is the wrong choice for a competitive game and the right one here.
// Server authority plus prediction exists so a shooter cannot be cheated and
// so 50ms does not decide who won. This is co-op PvE - nobody is being shot,
// there is nothing to cheat for, and the worst artefact is a crate settling a
// few centimetres differently on somebody else's screen.
//
// It is also why NGO was affordable at all: PHASE4_SPEC's stack decision
// turned on this exact point, that the prediction Photon Fusion sells is a
// capability this genre does not collect.
// ====================================================================

using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public class NetworkPlayer : NetworkBehaviour
{
    PlayerMotor motor;

    void Awake() => motor = GetComponent<PlayerMotor>();

    public override void OnNetworkSpawn()
    {
        if (motor == null) return;

        // ---- who is this, and is it me ----
        //
        // Deliberately BOTH, and in this order. PlayerRegistry.Register runs
        // in OnEnable, which is before OnNetworkSpawn, so by now the body has
        // already claimed a slot and possibly claimed local by the offline
        // rule (slot 0). Those guesses were made without the network in the
        // room; these two lines are the network correcting them.
        motor.AssignSlot((int)OwnerClientId);
        motor.MarkLocal(IsOwner);

        if (IsOwner) ClaimCamera();

        gameObject.name = IsOwner
            ? $"Player {OwnerClientId} (me)"
            : $"Player {OwnerClientId}";
    }

    /// <summary>
    /// Point the scene camera at this body.
    ///
    /// The camera is not part of the player prefab - it is a separate scene
    /// object that points AT one, which is exactly what Phase 3 Step 3 found
    /// and built around. A spawned body therefore has to introduce itself,
    /// the same way FirstPersonCamera introduces itself to its target.
    ///
    /// Only the owner does this. A remote body claiming the camera is how you
    /// end up watching somebody else's game.
    /// </summary>
    void ClaimCamera()
    {
        var cam = Object.FindFirstObjectByType<FirstPersonCamera>();
        if (cam == null)
        {
            Debug.LogError("[Net] No FirstPersonCamera in the scene for the " +
                           "spawned player to claim.");
            return;
        }

        // SetTarget, not `cam.target = transform`. The camera caches the
        // motor and the rigidbody from whoever it was pointed at, and
        // assigning the field alone would leave those pointing at the body
        // that was here before - offline a destroyed object, online somebody
        // else entirely.
        cam.SetTarget(transform);
        motor.BindView(cam);
    }
}
