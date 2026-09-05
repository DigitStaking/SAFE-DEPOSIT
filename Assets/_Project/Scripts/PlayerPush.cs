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

    [Tooltip("How wide the shove arc is, as a dot product against where you " +
             "are looking. 0.35 is about 70 degrees each way, 0.2 about 78, " +
             "0 is a full half-circle in front of you. Lower is more " +
             "forgiving; too low and you shove people you are not looking at.")]
    [Range(0f, 0.9f)] public float pushCone = 0.2f;

    [Tooltip("Radius of the probe. A little forgiveness so a shove does not " +
             "need the accuracy of a rifle shot.")]
    public float radius = 0.6f;

    [Header("Force")]
    [Tooltip("Impulse in newton-seconds. Divided by the target's MASS, which " +
             "is what makes one number cover a 70kg crewmate and a 140kg man " +
             "who barely notices. 140 gives a person about 2 m/s.")]
    public float impulse = 140f;
    // ====================================================================
    // A PERSON IS LAUNCHED BY TRAJECTORY, NOT BY FORCE.
    //
    // These used to be impulses in newton-seconds, which put the victim's mass
    // and whatever they were already doing between the number and the result.
    // "450 and 280" says nothing about what you will see.
    //
    // Two distances instead, with the velocities solved from them:
    //
    //     vY   = sqrt(2 * g * height)      straight ballistics
    //     air  = rise + fall               the fall is FASTER, because
    //                                      PlayerMotor adds extra gravity
    //                                      while descending
    //     vX   = distance / air
    //
    // and the result is WRITTEN as a velocity rather than added as a force, so
    // mass cancels, whatever they were doing is replaced, and the arc is
    // exactly the one solved for. Change the metres, get those metres.
    // ====================================================================

    [Header("Knockback - in metres, not newtons")]
    [Tooltip("How high a shoved person is thrown, in metres. Solved into an " +
             "upward velocity with vY = sqrt(2*g*h), so this is the height " +
             "they actually reach.")]
    public float knockbackHeight = 1f;

    [Tooltip("How far away a shoved person lands, in metres. Divided by the " +
             "flight time the height above produces, so the two together are " +
             "the whole arc - raising the height lengthens the flight, and " +
             "this stays the distance either way.")]
    public float knockbackDistance = 2f;

    [Tooltip("EXTRA seconds of no-control AFTER landing, on top of the flight " +
             "time - which is worked out from the height, so this is only the " +
             "tail. Small: just enough that they do not snap into a walk the " +
             "instant their feet touch.")]
    public float shoveRecovery = 0.12f;

    // Apply is static - the RPC path has no instance to hand - so the tuned
    // value is mirrored here once in Awake rather than duplicated as a second
    // constant somebody would forget to keep in step.
    static float ShoveRecovery = 0.9f;

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
        // Matched to the arc rather than typed in, so changing the height
        // cannot leave control coming back halfway up or long after landing.
        ShoveRecovery = KnockbackAirtime + shoveRecovery;

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

        // The SAME finder Connect uses, so the gesture is chosen for the thing
        // that will actually be shoved. It had its own copy of the SphereCast
        // and therefore its own copy of the point-blank blind spot - two
        // probes that could disagree about what you were aiming at, which is
        // exactly the sort of split this project keeps having to undo.
        Rigidbody body = FindTarget(eye);

        return body != null ? Pushable.For(body) : null;
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
        Rigidbody body = FindTarget(eye);
        if (body == null) return;

        // Pushable.Allows is asked inside FindTarget now, so anything that
        // comes back here is already something this shove may move.

        // ---- A PERSON IS NOT A CRATE ----
        //
        // The impulse that slides a filing cabinet barely registers on someone
        // standing up: a crate is resisted only by friction, a person by their
        // own motor. So the shove has to still be moving them once the Shoved
        // window closes.
        bool person = body.GetComponent<PlayerMotor>() != null;

        Vector3 push = person
            ? Knockback(body.transform)
            : Direction(eye) * impulse;

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

    /// <summary>
    /// Reused, so a shove does not allocate.
    ///
    /// SIXTY-FOUR, NOT SIXTEEN. OverlapSphereNonAlloc does not report the
    /// nearest colliders - it reports the first ones it finds and then stops.
    /// Sixteen sounded like plenty for arm's reach until you count what is
    /// actually inside a 1.9m sphere in a corridor: floor, several wall
    /// panels, a door leaf and its frame, the skirting, whatever loot is on
    /// the ground. The person you are trying to shove can easily be number
    /// seventeen, and then they simply are not in the results.
    ///
    /// That is the "sometimes it works" and the "cannot push near a door" -
    /// both are geometry density, not aim.
    /// </summary>
    static readonly Collider[] nearby = new Collider[64];

    /// <summary>
    /// What is in front of the eye, at ANY range - including point blank.
    ///
    /// ---- WHY THE SPHERECAST ALONE COULD NEVER SHOVE A PERSON ----
    ///
    /// Physics.SphereCast does not report colliders that already overlap the
    /// sphere at its START position. That is not an edge case here, it is the
    /// normal case:
    ///
    ///     two players stand 0.84m apart (0.42 capsule each)
    ///     so their surface is 0.42m from your eye
    ///     the cast's start sphere is 0.45m
    ///     -> overlapping at t=0, and the cast returns NOTHING
    ///
    /// It was the same before the capsule was widened - 0.30m surface against
    /// the same 0.45m sphere - so a point-blank shove on a person has never
    /// once worked. Crates escaped it only because you stand further from a
    /// crate than from a person.
    ///
    /// So the cast still does the reaching, and an overlap check covers the
    /// range the cast is blind to. Same detection, one owner, no second
    /// raycast system.
    /// </summary>
    Rigidbody FindTarget(Transform eye)
    {
        // 1. THE REACH. A swept sphere down the look direction, as before.
        if (Physics.SphereCast(eye.position, radius, eye.forward,
                               out RaycastHit hit, range, mask,
                               QueryTriggerInteraction.Ignore) &&
            Usable(hit.rigidbody))
            return hit.rigidbody;

        // 2. POINT BLANK. Everything already touching us, nearest first, and
        //    only what is roughly in front - so backing into somebody does not
        //    shove them.
        Vector3 forward = eye.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return null;
        forward.Normalize();

        int found = Physics.OverlapSphereNonAlloc(eye.position, range, nearby,
                                                  mask, QueryTriggerInteraction.Ignore);

        Rigidbody best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < found; i++)
        {
            var rb = nearby[i] != null ? nearby[i].attachedRigidbody : null;
            if (!Usable(rb)) continue;

            Vector3 toward = rb.transform.position - eye.position;
            toward.y = 0f;

            float distance = toward.sqrMagnitude;
            if (distance < 1e-6f || distance >= bestDistance) continue;

            // Within about 70 degrees of where you are looking. Wide enough
            // that you do not have to aim at a ribcage, narrow enough that it
            // is still a shove rather than an area attack.
            if (Vector3.Dot(toward.normalized, forward) < pushCone) continue;

            best = rb;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Your own body. The probe starts inside your own capsule, so
    /// this has to be asked every time.</summary>
    bool IsSelf(Transform t) => t == transform || t.IsChildOf(transform);

    /// <summary>
    /// Something this shove could actually move.
    ///
    /// ---- WHY A DOOR USED TO EAT THE PUSH ----
    ///
    /// Pushable.Allows was checked in Connect, AFTER the finder had already
    /// committed to a target. So standing near a door, the cast would return
    /// the DOOR - nearer, and perfectly valid as far as the finder knew - and
    /// then Connect would find it unpushable and give up, without ever
    /// considering the person standing right behind it.
    ///
    /// Asking here instead means an unpushable thing is simply not a target,
    /// and the search carries on to the next one. "Sometimes he cannot be
    /// pushed near a door" was that, exactly.
    /// </summary>
    bool Usable(Rigidbody rb) =>
        rb != null && !IsSelf(rb.transform) && Pushable.Allows(rb);

    /// <summary>
    /// A shove that lifts somebody off their feet a little and throws them.
    ///
    /// ---- DIRECTION COMES FROM THE TWO BODIES, NOT FROM THE CAMERA ----
    ///
    /// Where you are LOOKING and where they are STANDING are different things.
    /// Shoulder-barging somebody at your side while looking down a corridor
    /// should throw them sideways, away from you - not down the corridor.
    /// So it is target minus pusher, flattened, which is what a shove
    /// physically is.
    ///
    /// ---- AND A SMALL LIFT, NOT A LAUNCH ----
    ///
    /// The upward part is what turns a slide into a shove: they leave the
    /// ground for a moment, travel, and land. Kept small on purpose - big
    /// enough to read as physical, nowhere near enough to be a jump.
    /// </summary>
    Vector3 Knockback(Transform victim)
    {
        Vector3 away = victim.position - transform.position;
        away.y = 0f;

        // Standing exactly on top of each other: fall back to facing, since
        // there is no "away" to compute and something has to happen.
        if (away.sqrMagnitude < 0.0001f)
        {
            away = transform.forward;
            away.y = 0f;
        }

        away.Normalize();

        // ---- SOLVE THE ARC ----
        //
        // fallGravityMultiplier is read off the VICTIM rather than assumed,
        // because PlayerMotor adds extra gravity only while descending - so the
        // fall is shorter than the rise and the flight time is not 2*vY/g. Get
        // that wrong and the horizontal distance misses by a third.
        float g = Mathf.Abs(Physics.gravity.y);
        if (g < 0.01f) g = 9.81f;

        var victimMotor = victim.GetComponent<PlayerMotor>();
        float fall = victimMotor != null
            ? Mathf.Max(1f, victimMotor.fallGravityMultiplier) : 1f;

        float height = Mathf.Max(0.01f, knockbackHeight);

        float vY = Mathf.Sqrt(2f * g * height);
        float airborne = vY / g + Mathf.Sqrt(2f * height / (g * fall));
        float vX = knockbackDistance / Mathf.Max(0.01f, airborne);

        return away * vX + Vector3.up * vY;
    }

    /// <summary>Seconds of flight the configured height produces. The
    /// no-control window is matched to it, so movement comes back on landing
    /// rather than halfway up or long after.</summary>
    public float KnockbackAirtime
    {
        get
        {
            float g = Mathf.Abs(Physics.gravity.y);
            if (g < 0.01f) g = 9.81f;

            float fall = motor != null ? Mathf.Max(1f, motor.fallGravityMultiplier) : 1f;
            float height = Mathf.Max(0.01f, knockbackHeight);

            return Mathf.Sqrt(2f * g * height) / g + Mathf.Sqrt(2f * height / (g * fall));
        }
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
        if (body == null || body.isKinematic) return;

        var motor = body.GetComponent<PlayerMotor>();

        if (motor != null)
        {
            // ---- SET, NOT ADD ----
            //
            // AddForce(Impulse) divides by mass and adds to whatever they were
            // already doing, so the same shove produced a different arc on
            // somebody walking toward you than on somebody standing still, and
            // neither matched the numbers.
            //
            // Assigning the velocity makes the launch exactly the arc that was
            // solved for, on any mass, from any starting motion. Safe here
            // because PlayerMotor never assigns horizontal velocity itself - it
            // only ever ADDS a clamped amount - so nothing is being fought.
            body.linearVelocity = push;

            motor.Shoved(ShoveRecovery);
            return;
        }

        // Crates are unchanged: an impulse, divided by mass, so a filing
        // cabinet still resists more than a can.
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
