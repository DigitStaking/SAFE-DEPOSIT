// PlayerPush.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerPush.cs
// Goes on: the Player root.
//
// ========================================================================
// PHASE 5 - PUSH.
//
// "the game has no way to affect somebody without killing them"
//
// Until now the only things a player could do to another person were revive
// them or stand in their way. Push is the missing verb, and it is the most
// useful thing in INHABITANTS.md because it works on EVERYTHING - crewmates,
// the thief, the cannibal, the fat man - without a single per-target rule.
//
// MASS DECIDES THE OUTCOME, AND IT COSTS NOTHING TO MAKE THAT TRUE
//
// ForceMode.Impulse divides by mass. That one choice delivers the whole table
// from the design document with no cases in it:
//
//   a crewmate   70kg  -> a real shove, out of a doorway or into a shaft
//   the fat man 140kg  -> barely moves, and the shove TELLS you so
//
// Nothing to tune per inhabitant. A heavier character absorbs a push because
// that is what mass is, and PlayerMotor already sets rb.mass from
// Campaign.PlayerMass rather than leaving it at Unity's 1kg default - so the
// numbers here mean something.
//
// YES, YOU CAN PUSH A FRIEND INTO THE SHAFT
//
// That has to stay possible. A game where you cannot betray somebody is a
// game where trusting them means nothing, and this crew spends the whole run
// deciding whether the voice on the radio is worth believing.
//
// It is NOT a weapon, and it must never become one: no damage, a cooldown,
// and an impulse small enough that killing somebody with it requires them to
// already be standing somewhere stupid. The shaft does the killing, not you.
//
// WHY THE PUSHER DOES NOT MOVE THE PERSON THEY PUSH
//
// This is the netcode rule the whole project keeps arriving at: THE OWNER
// SIMULATES THEIR OWN BODY. If I shoved your Rigidbody on my machine, your
// machine would still be running your physics and would simply overwrite it -
// exactly how teammates ended up hovering off the deck in Phase 4, two
// authorities fighting over one body.
//
// So a push is a REQUEST. It goes to the server, the server hands it to the
// person being pushed, and their machine applies it to their own body. They
// stay the only authority on where they are, and the shove arrives as
// something that happened to them rather than something done to their
// transform.
//
// WHAT THIS DOES NOT PUSH YET, AND WHY
//
// Loot. LootItem says it plainly: there is no NetworkObject on a crate and
// none is wanted, because sixty replicated crates is a lot of traffic for
// scenery that mostly sits still. Pushing one therefore needs LootNet's
// roster id to say WHICH crate moved, which is a step of its own rather than
// a line in this one. Sliding a crate you cannot lift toward the lift is a
// good idea and it is still coming.
// ========================================================================

using Unity.Netcode;
using UnityEngine;

public class PlayerPush : NetworkBehaviour
{
    [Header("Reach")]
    [Tooltip("How far in front of the eye a shove lands, in metres. Arm's " +
             "length - this is a shove, not a force push.")]
    public float range = 1.9f;

    [Tooltip("Radius of the probe. A little forgiveness so a shove does not " +
             "need the accuracy of a rifle shot.")]
    public float radius = 0.45f;

    [Header("Force")]
    [Tooltip("Impulse in newton-seconds. Divided by the target's MASS, which " +
             "is what makes one number cover a 70kg crewmate and a 140kg man " +
             "who barely notices. 140 gives a person about 2 m/s.")]
    public float impulse = 140f;

    [Tooltip("How much of the shove goes upward, 0 to 1. A little lifts them " +
             "off the floor so friction does not eat the push immediately. Too " +
             "much turns a shove into a launch.")]
    [Range(0f, 0.5f)] public float upward = 0.14f;

    [Tooltip("Seconds between shoves. The only real cost, and what stops push " +
             "becoming a weapon by repetition.")]
    public float cooldown = 0.85f;

    [Header("What can be pushed")]
    [Tooltip("Layers a shove looks for. Include the player layer.")]
    public LayerMask mask = ~0;

    float lastPush = -999f;

    PlayerMotor motor;
    PlayerHealth health;
    PlayerCarry carry;
    PlayerAnimatorDriver anim;

    /// <summary>Seconds until this player may shove again. For the HUD.</summary>
    public float CooldownLeft => Mathf.Max(0f, cooldown - (Time.time - lastPush));

    void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        health = GetComponent<PlayerHealth>();
        carry = GetComponent<PlayerCarry>();
        anim = GetComponent<PlayerAnimatorDriver>();
    }

    void Update()
    {
        // Only the person at this keyboard, and only their own body. Read
        // through PlayerMotor.Keys rather than Keyboard.current, because
        // "the keyboard" is a global answer to a question that stopped being
        // global the moment there were two bodies.
        var keys = motor != null ? motor.Keys : null;
        if (keys == null) return;

        if (!keys.gKey.wasPressedThisFrame) return;

        TryPush();
    }

    void TryPush()
    {
        if (Time.time - lastPush < cooldown) return;

        // Downed players are not shoving anybody. Being downed is the absence
        // of standing up, not a speed penalty.
        if (health != null && health.IsDowned) return;

        // Both hands full. You can shove with a crate in your arms about as
        // well as you can open a door with them.
        if (carry != null && carry.IsCarrying && !carry.CanJump) return;

        Transform eye = motor != null ? motor.Eye : null;
        if (eye == null) return;

        // SphereCast rather than Raycast, for the same reason the ground check
        // uses one: a single ray demands you aim at a ribcage, and a shove
        // that misses because you were looking at somebody's belt reads as
        // broken rather than as inaccurate.
        if (!Physics.SphereCast(eye.position, radius, eye.forward,
                                out RaycastHit hit, range, mask,
                                QueryTriggerInteraction.Ignore))
            return;

        var body = hit.rigidbody;
        if (body == null) return;

        // Never yourself. The probe starts inside your own capsule.
        if (body.transform == transform || body.transform.IsChildOf(transform)) return;

        lastPush = Time.time;

        // The reach-out. Reuses the existing DoUse trigger rather than adding
        // an animation state - a shove and pressing a button are the same
        // motion from the shoulder, and one clip that already exists beats a
        // second one that does not.
        if (anim != null) anim.PlayUse();

        Vector3 push = Direction(eye) * impulse;

        var target = body.GetComponent<NetworkObject>();

        // Offline, or something with no network identity of its own: it is
        // ours to move and there is nobody to tell.
        if (target == null || !target.IsSpawned || NetworkManager.Singleton == null)
        {
            Apply(body, push);
            return;
        }

        RequestPushServerRpc(target.NetworkObjectId, push);
    }

    Vector3 Direction(Transform eye)
    {
        Vector3 flat = eye.forward;
        flat.y = 0f;

        if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;
        flat.Normalize();

        // Mostly along the floor with a little lift. Shoving straight down
        // where the camera happens to point would plant somebody rather than
        // move them.
        return (flat + Vector3.up * upward).normalized;
    }

    static void Apply(Rigidbody body, Vector3 push)
    {
        // Impulse, so MASS divides it. This is the whole design: the fat man
        // absorbs a shove because he weighs twice what you do, and nobody had
        // to write a rule saying so.
        if (body != null && !body.isKinematic)
            body.AddForce(push, ForceMode.Impulse);
    }

    // --------------------------------------------------------------------
    // A PUSH IS A REQUEST, NOT AN ASSIGNMENT
    // --------------------------------------------------------------------

    /// <summary>
    /// Asks the server to deliver a shove. The server does not apply it - it
    /// forwards it to whoever owns that body, because they are the machine
    /// running its physics.
    /// </summary>
    [ServerRpc]
    void RequestPushServerRpc(ulong targetId, Vector3 push)
    {
        var spawned = NetworkManager.SpawnManager.SpawnedObjects;
        if (!spawned.TryGetValue(targetId, out var target) || target == null) return;

        var to = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { target.OwnerClientId }
            }
        };

        ShovedClientRpc(targetId, push, to);
    }

    /// <summary>
    /// Arrives only on the machine that OWNS the body being pushed. It applies
    /// the impulse to its own Rigidbody, stays the sole authority on where it
    /// is, and NetworkTransform carries the result back out to everyone else -
    /// so nobody ever writes to a transform they do not own.
    /// </summary>
    [ClientRpc]
    void ShovedClientRpc(ulong targetId, Vector3 push, ClientRpcParams to = default)
    {
        var spawned = NetworkManager.SpawnManager.SpawnedObjects;
        if (!spawned.TryGetValue(targetId, out var target) || target == null) return;

        Apply(target.GetComponent<Rigidbody>(), push);
    }
}
