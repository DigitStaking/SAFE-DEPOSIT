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

        if (IsOwner)
        {
            MoveToSpawn();
            ClaimCamera();
        }

        gameObject.name = IsOwner
            ? $"Player {OwnerClientId} (me)"
            : $"Player {OwnerClientId}";

        // Says what happened, because the alternative is reasoning about it
        // from a screenshot - which this project has now paid for twice.
        Debug.Log($"[Net] spawned {gameObject.name}   owner={IsOwner}   " +
                  $"slot={motor.Slot}   local={motor.IsLocal}   " +
                  $"eye={(motor.Eye != null ? motor.Eye.name : "NONE")}");
    }

    /// <summary>
    /// Put the body somewhere survivable.
    ///
    /// NGO spawns a player at the PREFAB's authored position, which is the
    /// world origin - and in this game the world origin is the top of the
    /// shaft. So a spawning player appeared in mid-air and fell down it. The
    /// host screenshot that reported this was sitting on 92/100 HP: that is
    /// PlayerFallDamage doing its job on a body that had no business falling.
    ///
    /// The exact same trap as the loot bug in Phase 2, whose whole diagnosis
    /// was "everything ends up at 0,0,0 and falls". Different system, same
    /// origin, same shaft.
    ///
    /// Spawned inside the lift, spread sideways by client id so four people
    /// do not arrive inside each other. The OWNER does the move because the
    /// transform is owner-authoritative - anybody else writing it would be
    /// overwritten a frame later.
    /// </summary>
    void MoveToSpawn()
    {
        var lift = SceneRefs.Lift;
        if (lift == null) return;

        // FOUR CORNERS, not a line.
        //
        // The first version spread people 0.6m apart along one axis, which is
        // less than two shoulder-widths - so at eye height the next body's
        // torso simply filled your screen and it read as "the host has two
        // bodies". They were 60cm apart and 1.6m tall.
        //
        // A corner each, 2.4m across inside a 4x4 car: far enough to see each
        // other as PEOPLE rather than as a wall of red, close enough that
        // nobody clips the doors. Four is Crew.MaxMembers, so the pattern
        // covers everyone the demo supports.
        Vector2[] corners =
        {
            new Vector2(-1.2f, -1.2f),
            new Vector2( 1.2f, -1.2f),
            new Vector2(-1.2f,  1.2f),
            new Vector2( 1.2f,  1.2f),
        };

        var spot = corners[(int)OwnerClientId % corners.Length];
        Vector3 where = lift.transform.TransformPoint(new Vector3(spot.x, 0.2f, spot.y));

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // rb.position, not transform.position. Same lesson the loot roof
            // bug taught: the transform is what you see, the body is what
            // physics believes, and moving only the first leaves the second
            // where it was.
            rb.position = where;
            rb.linearVelocity = Vector3.zero;
        }

        transform.position = where;

        // Turned to face the centre of the car. Spawning everyone pointing
        // the same way means three players staring at a wall while the fourth
        // wonders where they went - and the first thing this game should show
        // you is the other people in the lift.
        Vector3 inward = lift.transform.position - where;
        inward.y = 0f;
        if (inward.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(inward.normalized, Vector3.up);
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

        // The camera has to be ON for any of that to matter. ElevatorDashboard
        // disables it while somebody is at the panel and restores it on exit -
        // but if a session starts while it is down, nothing ever turns it back
        // on and the view simply stops where it was.
        cam.enabled = true;

        Debug.Log($"[Net] camera '{cam.gameObject.name}' now follows " +
                  $"{gameObject.name}");
    }
}
