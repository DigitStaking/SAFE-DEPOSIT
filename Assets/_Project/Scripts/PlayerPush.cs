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

    [Tooltip("Seconds of rest AFTER the swing has finished, not from the last " +
             "keypress. " +
             "This used to be a separate number racing the animation, which " +
             "meant setting it below armTime silently allowed a new shove to " +
             "start on top of one still playing - the arms would snap back to " +
             "the wind-up mid-thrust. Now the swing always completes first and " +
             "this is only the pause on the end, so spamming is impossible by " +
             "construction rather than by two numbers being kept in step.")]
    public float restAfterSwing = 0.4f;

    /// <summary>The full gap between one shove starting and the next being
    /// allowed: the whole swing, then the rest.</summary>
    public float TotalCooldown => armTime + restAfterSwing;

    [Header("What can be pushed")]
    [Tooltip("Layers a shove looks for. Include the player layer.")]
    public LayerMask mask = ~0;

    [Header("The shove itself")]
    [Tooltip("Seconds the arms take to wind up, thrust and return. The impulse " +
             "lands at the moment of the thrust, not on the keypress, so the " +
             "hit follows the hands. " +
             "THE ONE DIAL FOR SPEED - every other timing here is a fraction " +
             "of it, and the lockout follows automatically, so nothing else " +
             "needs touching to change the feel. " +
             "7 seconds is a deliberate, asked-for 4x on the 1.75 it was. " +
             "Worth knowing what that costs in play: the swing cannot be " +
             "interrupted, so this is also how long a player is committed for, " +
             "and a shove becomes a once-every-seven-seconds decision rather " +
             "than a reaction. If that turns out to be too long to use, this " +
             "is the only number to change.")]
    public float armTime = 7f;

    [Tooltip("How far through the swing contact happens, 0 to 1. A shove lands " +
             "when the arms reach out, not when they start moving. " +
             "0.62 is where PlayerPushArms actually reaches full extension. " +
             "It was 0.34, which the corrected curve revealed was still inside " +
             "the WIND-UP - the impulse was landing while the hands were moving " +
             "backwards.")]
    [Range(0.1f, 0.9f)] public float contactAt = 0.62f;

    float lastPush = -999f;
    bool contacted;

    /// <summary>
    /// How far through the shove this body is, 0 to 1, or -1 when idle.
    ///
    /// Read by PlayerPushArms on EVERY machine. The arms are animated locally
    /// from this rather than replicated as a pose - a push is a quarter of a
    /// second, and sending one bool beats sending an arm.
    /// </summary>
    public float PushProgress
    {
        get
        {
            float age = Time.time - lastPush;
            return age >= 0f && age <= armTime ? age / armTime : -1f;
        }
    }

    PlayerMotor motor;
    PlayerHealth health;
    PlayerCarry carry;

    /// <summary>Seconds until this player may shove again. For the HUD.</summary>
    public float CooldownLeft => Mathf.Max(0f, TotalCooldown - (Time.time - lastPush));

    void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        health = GetComponent<PlayerHealth>();
        carry = GetComponent<PlayerCarry>();
    }

    void ReadKey()
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

    void Update()
    {
        // ---- CONTACT HAPPENS WHEN THE ARMS ARRIVE ----
        //
        // Resolved here rather than on the keypress, because a shove that
        // lands before the hands have moved reads as telekinesis. The probe
        // also happens at the moment of contact rather than at the moment of
        // input, so somebody who steps out of the way during the wind-up
        // actually gets away with it.
        if (!contacted && PushProgress >= contactAt)
        {
            contacted = true;
            Connect();
        }

        // ---- A TEST PROFILE LASTS EXACTLY ONE SWING ----
        //
        // Without this it would last forever: press Test once while tuning,
        // and every real shove for the rest of the session quietly uses the
        // profile you were experimenting with. That is the kind of bug that
        // survives a whole playtest and gets blamed on the wrong system,
        // because nothing about it looks like a leftover.
        if (previewOverride != null && PushProgress < 0f)
            previewOverride = null;

        ReadKey();
    }

    void TryPush()
    {
        // ---- THE SWING MUST FINISH BEFORE ANOTHER CAN START ----
        //
        // Two guards rather than one, and the first is the one that matters:
        // whatever anybody types into the rest field, a shove already in the
        // air cannot be interrupted. A second press mid-thrust used to snap the
        // arms back to the wind-up, which is both ugly and a free extra push.
        if (PushProgress >= 0f) return;

        if (Time.time - lastPush < TotalCooldown) return;

        // Downed players are not shoving anybody. Being downed is the absence
        // of standing up, not a speed penalty.
        if (health != null && health.IsDowned) return;

        // Both hands full. You can shove with a crate in your arms about as
        // well as you can open a door with them.
        if (carry != null && carry.IsCarrying && !carry.CanJump) return;

        if (motor == null || motor.Eye == null) return;

        // ---- THE SWING STARTS NOW. THE HIT COMES LATER. ----
        //
        // Nothing is probed here. A shove that connects on the keypress, before
        // the hands have moved, reads as telekinesis - and it also means
        // whiffing is impossible, because a miss simply never plays. Both are
        // fixed by separating the ANIMATION from the CONTACT: the arms always
        // swing, and Connect decides a third of the way through whether they
        // found anybody.
        StartSwing();

        // Everyone else needs to see the same arms move. One announcement, and
        // each machine animates locally from it - a push is under half a
        // second, so sending "it happened" beats streaming an arm.
        if (NetworkManager.Singleton != null && IsSpawned && IsOwner)
            AnnounceSwingServerRpc();
    }

    // ====================================================================
    // WHICH GESTURE THIS SHOVE USES.
    //
    // "This object -> Heavy Door Push Profile -> load these hand settings"
    //
    // Resolved when the SWING STARTS, not when it CONNECTS. Those are 0.62 of
    // armTime apart, and the hands begin moving at the first of them - a
    // profile picked at contact would change the gesture two thirds of the way
    // through it, which is the definition of a snap.
    //
    // It does mean the gesture can be for a door you then miss. That is the
    // correct trade and the same one the whiff already makes: you commit to a
    // shove when you throw it, not when it lands.
    // ====================================================================

    /// <summary>The profile this swing is using, or null for the default.
    /// Read by the viewmodel every frame while the hands are moving.</summary>
    public PushProfile ActiveProfile { get; private set; }

    /// <summary>
    /// Forces the next swing to use this profile regardless of what is in
    /// front of you. Set by the Push Library's Test button so a gesture can be
    /// tuned without finding a door first, and cleared the moment the swing
    /// ends.
    /// </summary>
    [System.NonSerialized] public PushProfile previewOverride;

    /// <summary>
    /// Swing the arms with no physics and no networking.
    ///
    /// For tuning only. It deliberately skips the cooldown and the carrying
    /// check, because being unable to test a gesture until the game agrees you
    /// are allowed to shove is how tuning sessions turn into ten minutes of
    /// walking around.
    /// </summary>
    public void TestSwing(PushProfile profile)
    {
        previewOverride = profile;
        ActiveProfile = profile;

        lastPush = Time.time;
        contacted = true;          // already "connected", so Connect never probes
    }

    void StartSwing()
    {
        lastPush = Time.time;
        contacted = false;

        ActiveProfile = previewOverride != null ? previewOverride : LookUpProfile();
    }

    /// <summary>
    /// What is in front of the eye right now, and what gesture it wants.
    ///
    /// A second spherecast, on the keypress only, with the same shape as the
    /// one Connect uses - so the thing whose profile you get is the thing you
    /// would have hit had you connected this instant.
    /// </summary>
    PushProfile LookUpProfile()
    {
        Transform eye = motor != null ? motor.Eye : null;
        if (eye == null) return null;

        if (!Physics.SphereCast(eye.position, radius, eye.forward,
                                out RaycastHit hit, range, mask,
                                QueryTriggerInteraction.Ignore))
            return null;

        if (hit.collider == null) return null;

        // Never yourself, same as Connect - the probe starts inside your own
        // capsule and you do not have a push profile.
        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return null;

        return Pushable.For(hit.collider);
    }

    /// <summary>
    /// The moment the hands arrive. Probes, and shoves whatever is there.
    ///
    /// Runs only on the pusher's own machine - it is their aim and their
    /// reach, and a target that has stepped aside on THEIR screen is a target
    /// they missed.
    /// </summary>
    void Connect()
    {
        if (NetworkManager.Singleton != null && IsSpawned && !IsOwner) return;

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
    /// <summary>
    /// Tells everyone this body just swung, so the arms move on every screen.
    ///
    /// Deliberately NOT tied to whether the shove connected. A whiff is worth
    /// seeing - it is how a crewmate knows somebody just tried to put them
    /// down the shaft and missed.
    /// </summary>
    [ServerRpc]
    void AnnounceSwingServerRpc() => SwingClientRpc();

    [ClientRpc]
    void SwingClientRpc()
    {
        // The owner already started their own swing on the keypress. Replaying
        // it here would restart the arms a round trip later, which is a visible
        // hitch on the one machine that should never see one.
        if (IsOwner) return;

        lastPush = Time.time;

        // Remote bodies never probe - Connect early-returns for them - but the
        // flag has to be armed or the guard in Update would fire on the next
        // real swing.
        contacted = true;
    }

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
