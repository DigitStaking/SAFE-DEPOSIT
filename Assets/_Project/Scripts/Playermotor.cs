// PlayerMotor.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerMotor.cs
// Goes on: the Player root.
//
// ========================================================================
// THE CENTRAL IDEA
//
// This controller never assigns velocity directly for horizontal movement.
// Every physics step it:
//
//     1. works out the velocity it WANTS
//     2. subtracts the velocity it HAS
//     3. CLAMPS that difference to an acceleration budget
//     4. applies the clamped result
//
// Step 3 is the whole design. The player can influence their motion but can
// never override physics. The rope, a teammate shoving them, a falling
// crate - all still work, because the controller only ever spends a limited
// budget per step.
//
// It also gives us swinging for free. Air acceleration is deliberately
// tiny, so while hanging you cannot fly to a doorway. You have to build
// pendulum momentum. That is the game.
//
// WHY RIGIDBODY AND NOT CHARACTERCONTROLLER
//
// CharacterController is easier and feels good instantly, but it is not
// part of the physics simulation. It has no mass and ignores forces. A rope
// cannot pull something that ignores forces. Physics character movement is
// harder to tune, and that cost is the price of the game existing.
// ========================================================================

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Top horizontal speed in metres per second. " +
             "2.5 is a purposeful walk. It was 4.5, which is a RUN - real " +
             "walking is about 1.4 - and that turned out to be most of why the " +
             "legs never looked like walking. No leg geometry makes a run read " +
             "as a walk, so the legs were being blamed for the speed. " +
             "Raising this again also lengthens the stride the legs ask for, " +
             "and that is capped by ProceduralLegs.maxReach - so past roughly " +
             "3.5 the feet start being dragged further than the leg can reach " +
             "until the hips move (step 4).")]
    public float moveSpeed = 2.5f;

    [Tooltip("How fast horizontal velocity can change while standing on " +
             "something. High = snappy. Lower it if the character feels twitchy.")]
    public float groundAcceleration = 60f;

    [Tooltip("Same, airborne. KEEP THIS SMALL. It is what forces players to " +
             "swing on the rope rather than fly. Raising it will quietly ruin " +
             "the game.")]
    public float airAcceleration = 8f;

    [Header("Being shoved")]
    [Tooltip("How hard the motor is allowed to fight a shove while one is " +
             "landing, in m/s^2. " +
             "The distance a shove throws you is roughly speed squared over " +
             "twice this - so at 8, a 5 m/s shove travels about 1.6m. Raise it " +
             "to recover faster, lower it to slide further.")]
    public float shoveControl = 8f;

    float shovedUntil;

    /// <summary>
    /// Somebody just shoved this body. Back off for a moment.
    ///
    /// Called on EVERY machine, not just the pusher's, because the shove
    /// arrives through a ClientRpc and each client simulates its own bodies -
    /// if only the pusher relaxed, everybody else would still watch the motor
    /// brake it flat.
    /// </summary>
    public void Shoved(float seconds)
    {
        shovedUntil = Mathf.Max(shovedUntil, Time.time + Mathf.Max(0f, seconds));
    }

    [Header("Jump")]
    [Tooltip("Peak height of a standing jump, in metres. Launch velocity is " +
             "calculated from this and gravity, so the number means what it says.")]
    public float jumpHeight = 1.1f;

    [Tooltip("Extra gravity while falling. 1 = realistic. Higher feels snappier. " +
             "Only affects the fall, never the rise.")]
    public float fallGravityMultiplier = 1.8f;

    [Tooltip("Seconds between pressing jump and actually leaving the ground, " +
             "so the crouch at the start of the Jumping Up clip can play " +
             "BEFORE the launch instead of after it. " +
             "0.11 is where that clip's take-off actually is - 17 frames at " +
             "30fps, with the push-off about a third of the way in. " +
             "This IS input latency and it is deliberate: without it the body " +
             "is already rising while the animation is still crouching, which " +
             "is what made the jump look detached from the character. Set it " +
             "to 0 to go back to launching on the keypress.")]
    public float jumpWindUp = 0.11f;

    /// <summary>
    /// True from the moment jump is pressed until the body actually leaves
    /// the ground.
    ///
    /// EXISTS TO BE READ BY THE ANIMATION, NEVER TO DRIVE IT. PlayerAnimatorDriver
    /// watches this and fires the jump trigger at the START of the wind-up, so
    /// the crouch plays into the launch rather than chasing it. That keeps the
    /// dependency one-directional exactly as that file's header demands -
    /// animation is a listener, and this is one more thing to listen to.
    /// </summary>
    public bool JumpWindingUp => jumpQueued;

    [Header("Ground check")]
    [Tooltip("Which layers count as ground. Set this to Environment.")]
    public LayerMask groundMask = ~0;

    [Tooltip("How far below the feet to look for ground. A little slack is " +
             "forgiving on stairs and small bumps.")]
    public float groundCheckDistance = 0.25f;

    // ---- TOP SPEED IS A PRODUCT, NOT A FIELD (Phase 2 Step 4) ----
    //
    // This used to be one shared float that anything could assign. Three
    // things want a say in how fast you walk - a hard external lock, your
    // injuries, and what you are carrying - and one variable can only hold
    // the opinion of whoever wrote to it last. That is not a tuning problem,
    // it is a correctness one:
    //
    //   * ElevatorDashboard set it to 0 on entry and 1 on exit. Walking away
    //     from the panel would have handed a crawling, 8-HP player their full
    //     speed back, because 1 is an ASSIGNMENT and not a release.
    //   * PlayerCarry.SpeedMultiplier has existed since Phase 1 and was never
    //     read by movement at all - only by PlayerAnimatorDriver. A 200kg
    //     cabinet ANIMATED heavy while you walked at full speed. Step 6's
    //     "feel the speed penalty" was never going to work.
    //
    // So the external lock keeps a field of its own, injury and carry weight
    // are READ FROM THEIR OWNERS every frame rather than pushed here, and the
    // three multiply. Nothing can overwrite anything else's opinion because
    // nobody stores anybody else's.

    /// <summary>Hard stop from something that took control of you outright -
    /// the dashboard. 0 = frozen, 1 = released. Not for injury or weight.</summary>
    [HideInInspector] public float externalSpeedLock = 1f;

    /// <summary>1 while healthy, less while hurt, 0 while downed.</summary>
    public float InjuryFactor => health != null ? health.SpeedFactor : 1f;

    /// <summary>1 empty-handed, ~0.7 heavy, ~0.45 massive.</summary>
    public float CarryFactor => carry != null ? carry.SpeedMultiplier : 1f;

    /// <summary>What actually scales moveSpeed.</summary>
    public float SpeedMultiplier => externalSpeedLock * InjuryFactor * CarryFactor;

    [Tooltip("Seconds after leaving the ground during which you still count as " +
             "grounded. Classic 'coyote time' - it makes jumping off a ledge " +
             "forgiving, and it stops the ground check flickering on and off " +
             "when something jostles you, which other systems read as noise.")]
    public float coyoteTime = 0.15f;

    public Vector2 LookInput { get; private set; }
    public bool UsingGamepad { get; private set; }

    /// <summary>How hard the player is asking to move, 0 to 1.</summary>
    public float MoveIntent => Mathf.Clamp01(moveInput.magnitude);

    /// <summary>
    /// Grounded, with coyote time. Use this for gameplay decisions.
    /// The raw check flickers whenever anything nudges you a centimetre off
    /// the floor, and systems that read it directly inherit that jitter.
    /// </summary>
    public bool IsGrounded => Time.time - lastGroundedTime <= coyoteTime;

    /// <summary>The unsmoothed check. Only for things that need the truth right now.</summary>
    public bool IsGroundedStrict => grounded;

    Rigidbody rb;
    CapsuleCollider capsule;
    PlayerInput playerInput;
    PlayerCarry carry;
    PlayerHealth health;
    Unity.Netcode.NetworkObject netObj;

    Vector2 moveInput;
    bool jumpQueued;
    bool grounded;
    float lastGroundedTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        playerInput = GetComponent<PlayerInput>();
        carry = GetComponent<PlayerCarry>();
        health = GetComponent<PlayerHealth>();
        netObj = GetComponent<Unity.Netcode.NetworkObject>();

        rb.isKinematic = false;

        // PLAYER_MASS from ECONOMY_AND_CAMPAIGN.md, read from Campaign so the
        // physics and the elevator's load gauge cannot disagree about what a
        // person weighs. Nothing here ever set it, so it sat at Unity's
        // default of 1kg: a person
        // who is nominally 70kg was colliding as if they weighed 1, which is
        // why walking into a 34kg filing cabinet could shove it around
        // without even picking it up. ForceMode.VelocityChange in
        // ApplyMovement() below is deliberately mass-independent for the
        // player's OWN acceleration - that part is correct and unrelated -
        // but the actual COLLISION response between two Rigidbodies is not,
        // and it needs a real mass to weigh correctly against loot that
        // already has one.
        rb.mass = Campaign.PlayerMass;

        // We turn the body ourselves via MoveRotation, so X and Z must be
        // frozen (no toppling) but Y must stay free.
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;
    }

    // Registered here rather than in Start, and unregistered in OnDisable,
    // so the list is true for the whole lifetime of the body rather than from
    // whenever Start happened to run. OnDisable also fires on destruction,
    // which is what empties the list cleanly as ReloadScene tears the scene
    // down - nothing to invalidate by hand.
    // ---- AM I THE ONE AT THIS KEYBOARD? (Phase 3 Step 2) ----
    //
    // Assigned by PlayerRegistry on registration: the first body to appear
    // claims local, everyone after it does not. Solo therefore needs no setup
    // at all, and a second prefab dropped into the scene is automatically NOT
    // local without anybody remembering to tick a box.
    //
    // Phase 4 takes the decision over - the network owns who is whose - which
    // is why it is settable rather than serialized. A serialized value would
    // be a stale opinion baked into a prefab, and the prefab is the one thing
    // that cannot know the answer.
    // ---- AM I THE ONE AT THIS KEYBOARD? ----
    //
    // ASKED LIVE WHEN THERE IS A NETWORK, not remembered from a callback.
    //
    // MarkLocal used to be the whole answer: NetworkPlayer set it once in
    // OnNetworkSpawn and everything trusted the stored value forever. One
    // missed call - a spawn ordering quirk, an ownership change, a body that
    // registered before the network had an opinion - and a remote body
    // believes it is yours for the rest of the session. Which is what "the
    // host can control two bodies" is: one press, two bodies that both think
    // the keyboard is theirs.
    //
    // NetworkObject.IsOwner is the authority and it cannot go stale. When a
    // spawned NetworkObject is present it wins outright; offline, where there
    // is no network to ask, the stored flag still decides.
    //
    // Fourth time today the same fix has been the right one - the eye, the
    // camera target, injury and carry factors, and now this. A cached answer
    // about somebody else goes stale; a live one cannot.

    bool localFlag;

    public bool IsLocal
    {
        get
        {
            if (netObj != null && netObj.IsSpawned) return netObj.IsOwner;
            return localFlag;
        }
    }

    /// <summary>
    /// Offline only, in practice. A spawned NetworkObject overrules it -
    /// deliberately, so nothing can talk a networked body out of knowing who
    /// owns it.
    /// </summary>
    public void MarkLocal(bool value) => localFlag = value;

    // ---- WHICH CREW MEMBER THIS BODY IS ----
    //
    // The key its HP, bleed-out, Lost flag and backpack are stored under in
    // Crew, which has to survive the scene being destroyed and rebuilt.
    //
    // SERIALIZED, NOT HANDED OUT IN REGISTRATION ORDER.
    //
    // It was registration order until the two-body test proved that order is
    // not stable: a freshly instantiated prefab registered BEFORE a body that
    // was already sitting in the scene, so the newcomer took slot 0 and the
    // original was pushed to slot 1.
    //
    // In a test rig that is merely confusing. In a real round it is a player
    // waking up with somebody else's injuries, because the slot is the key to
    // the whole per-player table. "Stable as long as bodies register in the
    // same order" was an assumption, and this is what checking it looked
    // like.
    //
    // A serialized value cannot shuffle. The registry still resolves
    // collisions, loudly, so two bodies set to the same number is a warning
    // rather than shared hit points.

    [Header("Crew")]
    [Tooltip("0-3. Which row of the crew table this body owns: its HP, its " +
             "bleed-out clock, its backpack. Leave at 0 for a single player. " +
             "Phase 4 replaces this with a network identity.")]
    [SerializeField] int crewSlot = 0;

    public int Slot => crewSlot;

    public void AssignSlot(int slot) => crewSlot = slot;

    // ---- MY DEVICES (Phase 3 Step 6) ----
    //
    // Seven scripts read Keyboard.current directly - the headlamp toggle, the
    // pack's number keys, the emotes, the debug keys. That is not input, it
    // is a GLOBAL: it means "the keyboard", and every body in the scene gets
    // the same answer. Two players, one press, both wave.
    //
    // Step 2 gated those on IsLocal, which stopped the crew waving in unison
    // but still cannot tell one local body from another. So the question
    // becomes ownership: does THIS player hold a keyboard?
    //
    // Answered from PlayerInput.devices when there is a PlayerInput to ask -
    // Unity's own pairing, so a gamepad handed to the second body is honoured
    // by every one of those seven scripts without any of them knowing a
    // second body exists. Falls back to IsLocal when there is no PlayerInput,
    // which is what keeps a bare prefab working.

    Keyboard keysCache;

    /// <summary>
    /// The keyboard THIS player holds, or null.
    ///
    /// CACHED, AND THE CACHE IS THE POINT. ElevatorDashboard disables this
    /// body's PlayerInput to stop it walking while somebody is at the panel -
    /// and a disabled PlayerInput reports no devices. Reading pairing live
    /// therefore said "this player holds no keyboard" for exactly as long as
    /// they were standing at the controls, so F got you in and nothing got
    /// you out.
    ///
    /// Disabling input is a GAMEPLAY action - it means "you cannot walk right
    /// now". It is not a statement about who owns the hardware, and it must
    /// not be read as one. So pairing refreshes the answer whenever it can,
    /// and the last known answer stands when it cannot.
    /// </summary>
    public Keyboard Keys
    {
        get
        {
            // NOT LOCAL, NOT YOUR KEYBOARD. FULL STOP.
            //
            // The two-body audit reported the rig "holding" the keyboard
            // while not being local, which is real: with one device in the
            // machine, Unity pairs it to EVERY PlayerInput by default.
            // neverAutoSwitchControlSchemes stops devices being re-handed
            // mid-play; it does not stop them being handed to everyone at
            // startup. So pressing L would have toggled both headlamps, Z
            // would have made both bodies wave, H would have hurt both.
            //
            // This game is online co-op, so there is exactly ONE local player
            // per machine - PHASE3_SPEC is explicit that split-screen is a
            // test rig and not a mode. Locality is therefore the real gate
            // and pairing is the tie-break BELOW it, not instead of it.
            if (!IsLocal) return null;

            if (playerInput != null && playerInput.enabled)
            {
                foreach (var d in playerInput.devices)
                    if (d is Keyboard k) { keysCache = k; return k; }

                // Paired with something, and none of it is a keyboard - a
                // gamepad player. That IS a live answer, so honour it.
                if (playerInput.devices.Count > 0) { keysCache = null; return null; }
            }

            if (keysCache == null && IsLocal) keysCache = Keyboard.current;
            return keysCache;
        }
    }

    Gamepad padCache;

    /// <summary>The gamepad THIS player holds, or null. Cached for the same
    /// reason as Keys above - see that note.</summary>
    public Gamepad Pad
    {
        get
        {
            if (!IsLocal) return null;   // same rule as Keys above

            if (playerInput != null && playerInput.enabled)
            {
                foreach (var d in playerInput.devices)
                    if (d is Gamepad g) { padCache = g; return g; }

                if (playerInput.devices.Count > 0) { padCache = null; return null; }
            }

            if (padCache == null && IsLocal) padCache = Gamepad.current;
            return padCache;
        }
    }

    // ---- MY CAMERA (Phase 3 Step 3) ----
    //
    // The camera is NOT a child of the player - it is a separate scene object
    // that points AT one, via FirstPersonCamera.target. So the link only runs
    // one way and the player cannot go looking for it; the camera has to
    // announce itself, exactly the way players announce themselves to
    // PlayerRegistry.
    //
    // That inversion is what kills Camera.main. "The main camera" is a global
    // answer to a question that stopped being global the moment there were two
    // bodies, and it was being asked fourteen times.
    public FirstPersonCamera View { get; private set; }

    /// <summary>My eye transform, or null if no camera has claimed me.</summary>
    public Transform Eye => View != null ? View.transform : null;

    public void BindView(FirstPersonCamera v) => View = v;

    void OnEnable() => PlayerRegistry.Register(this);
    void OnDisable() => PlayerRegistry.Unregister(this);

    // NOTHING IS CACHED HERE ANY MORE.
    //
    // `cam = Eye` in Start was correct for a body placed in the scene, where
    // the camera already exists and has already bound itself. It is wrong for
    // a NETWORK-SPAWNED body: Start runs when the object is instantiated and
    // OnNetworkSpawn runs afterwards, so the eye did not exist yet and the
    // field cached null for the rest of the body's life - leaving a player
    // who could not move, because ApplyMovement returns early without a
    // camera.
    //
    // Read live instead. The property is two null checks and it cannot go
    // stale, which is worth more than the lookup it saves.
    Transform Cam => Eye;

    // --------------------------------------------------------------------
    // INPUT
    // Player Input is on "Send Messages", so Unity calls these by name on
    // every component of this GameObject. Each player has their own Player
    // Input component, so each gets their own messages - which is what makes
    // local multiplayer work later without changing this code.
    // --------------------------------------------------------------------

    // ---- INPUT MESSAGES ARE GATED ON IsLocal, EVERY ONE OF THEM ----
    //
    // PlayerInput delivers OnMove / OnLook / OnJump by SendMessage to its own
    // GameObject, and every body in the scene has a PlayerInput. Both of them
    // are paired to the one keyboard in the machine, so both of them walked
    // when you pressed W. That is the "he moves when I move" bug, and the
    // second body was never a second player - it was yours, mirrored.
    //
    // Phase 3 Step 6 fixed the RAW Keyboard.current reads by routing them
    // through PlayerMotor.Keys. It did not fix this, because PlayerInput's
    // message callbacks are a completely separate path that never asks whose
    // keyboard it is. Same class of bug, missed half of it.
    //
    // Gated HERE rather than by disabling the PlayerInput component, because
    // ElevatorDashboard already enables and disables that component to lock
    // you at the panel - two systems assigning one flag is the exact trap
    // speedMultiplier turned out to be in Phase 2.

    void OnMove(InputValue value)
    {
        if (!IsLocal) return;
        moveInput = value.Get<Vector2>();
    }

    void OnLook(InputValue value)
    {
        if (!IsLocal) return;

        LookInput = value.Get<Vector2>();

        // Mouse look arrives as pixels moved since last frame; stick look as
        // a constant -1..1 while held. They need different scaling, so the
        // camera has to know which one it is.
        UsingGamepad = playerInput != null && playerInput.currentControlScheme == "Gamepad";
    }

    float jumpPressedAt = -999f;

    void OnJump(InputValue value)
    {
        if (!IsLocal) return;

        if (!value.isPressed) return;

        // Anything that needs two hands stops you jumping. You can shuffle it
        // around and you can set it down on the deck, but you cannot hop about
        // with a marble bust.
        //
        // This is what makes heavy loot a real decision instead of a speed
        // penalty: pick up something big and you are committed to walking it
        // back to the elevator.
        if (carry != null && !carry.CanJump) return;

        // Downed is not a speed penalty, it is the absence of standing up.
        if (health != null && health.IsDowned) return;

        // Queued, not acted on immediately. Input arrives on the render frame
        // but physics runs on its own clock; acting here would sometimes miss
        // a physics step and drop the input entirely.
        //
        // Now also the start of the WIND-UP: the animation begins here and the
        // launch happens jumpWindUp later, so the crouch leads into the jump
        // instead of playing after the body has already gone.
        if (jumpQueued) return;   // already winding up; do not restart it

        jumpQueued = true;
        jumpPressedAt = Time.time;
    }

    // --------------------------------------------------------------------
    // PHYSICS. Everything touching the Rigidbody happens in FixedUpdate,
    // in lockstep with the simulation. Doing physics in Update is the most
    // common cause of jittery, framerate-dependent movement in Unity.
    // --------------------------------------------------------------------

    void FixedUpdate()
    {
        // ==============================================================
        // A BODY YOU DO NOT OWN IS PLACED, NOT SIMULATED.
        //
        // This ran on every body in the scene, including teammates. So a
        // remote body had gravity applied locally, its own ground check run
        // locally, and its velocity damped locally - all while
        // NetworkTransform was writing its position from the wire.
        //
        // Two authorities, one body, every frame. The visible result was a
        // teammate hovering a few centimetres off the deck and refusing to
        // settle, and a small pop whenever the lift stopped: the network put
        // them on the floor, local gravity pulled them off it, and neither
        // ever won.
        //
        // Their machine already did all of this correctly. Doing it again
        // here was never going to agree with the answer that arrived, because
        // the answer that arrived is 100ms older than the physics running
        // now.
        //
        // Offline every body is local, so single player is untouched.
        // ==============================================================
        if (!IsLocal) return;

        GroundCheck();
        ApplyMovement();
        ApplyJump();
        ApplyFallGravity();
        FaceCameraYaw();
    }

    void GroundCheck()
    {
        // SphereCast, not Raycast. A single ray down from the centre misses
        // whenever you stand on the edge of a ledge, producing the classic
        // bug where you cannot jump near a drop.
        float radius = capsule.radius * 0.95f;
        Vector3 origin = transform.position + Vector3.up * capsule.radius;

        grounded = Physics.SphereCast(origin, radius, Vector3.down, out _,
                                      groundCheckDistance, groundMask,
                                      QueryTriggerInteraction.Ignore);

        if (grounded) lastGroundedTime = Time.time;
    }

    void ApplyMovement()
    {
        if (Cam == null) return;

        // Being carried. Carryable.PickUp turns the body kinematic so it can
        // be positioned by the carrier, and AddForce on a kinematic body does
        // nothing except waste a call and confuse the next person to read a
        // profiler. The downed player is not driving anyway - SpeedFactor is
        // already 0 - but this is the difference between "asks for zero speed"
        // and "is not asking".
        if (rb.isKinematic) return;

        // Movement is relative to where the camera looks, flattened so that
        // looking up or down never changes how fast you walk.
        Vector3 camForward = Vector3.ProjectOnPlane(Cam.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(Cam.right, Vector3.up).normalized;

        Vector3 wish = camForward * moveInput.y + camRight * moveInput.x;
        if (wish.sqrMagnitude > 1f) wish.Normalize();   // no faster on diagonals

        // 1. the velocity we want
        Vector3 targetVelocity = wish * moveSpeed * SpeedMultiplier;

        // 2. the horizontal velocity we have. Vertical is excluded so this
        //    never fights gravity or a jump.
        Vector3 velocity = rb.linearVelocity;
        Vector3 currentHorizontal = new Vector3(velocity.x, 0f, velocity.z);

        // 3. the difference, clamped to what we are allowed to spend
        Vector3 delta = targetVelocity - currentHorizontal;

        // ---- WHY A SHOVE USED TO MOVE SOMEBODY THREE CENTIMETRES ----
        //
        // With no input targetVelocity is ZERO, so delta is the negative of
        // whatever speed the body has and this brakes it out at the full
        // ground acceleration. That is correct for stopping when you let go of
        // W, and it is also what erased every push.
        //
        // The arithmetic: a 140 impulse on a 70kg body is 2 m/s. Braking at
        // groundAcceleration 60 kills that in 0.033s, over v^2/2a = 3.3cm. The
        // shove was landing perfectly and being deleted before anyone saw it.
        //
        // So for a moment after being shoved the motor stops fighting: it
        // still steers, but far more weakly, and the impulse gets to carry.
        // Distance is roughly v^2 / (2 * shoveControl), which is the number to
        // change if you want a different throw.
        float acceleration = grounded ? groundAcceleration : airAcceleration;

        if (Time.time < shovedUntil)
            acceleration = Mathf.Min(acceleration, shoveControl);
        Vector3 change = Vector3.ClampMagnitude(delta, acceleration * Time.fixedDeltaTime);

        // 4. apply. VelocityChange adds straight to velocity and ignores mass,
        //    so a heavier character does not accelerate more slowly just
        //    because we made them heavy.
        rb.AddForce(change, ForceMode.VelocityChange);

        // Note what this does NOT do: it never assigns rb.linearVelocity.
        // That is deliberate. Anything else pushing this body survives,
        // because we only add a limited amount on top of what physics
        // already decided.
    }

    void ApplyJump()
    {
        if (!jumpQueued) return;

        // ---- THE WIND-UP ----
        //
        // Hold the launch until the crouch has had time to play. The clip is
        // 17 frames at 30fps and its push-off is about a third of the way in,
        // so the body used to be airborne and rising before the animation had
        // even reached the part where it jumps.
        if (Time.time - jumpPressedAt < jumpWindUp)
        {
            // Walked off a ledge mid-crouch: cancel rather than launching from
            // thin air a moment later.
            if (!IsGrounded) jumpQueued = false;
            return;
        }

        jumpQueued = false;

        // Uses the coyote window, so stepping off a ledge and pressing jump a
        // frame later still works. Consumed immediately afterwards, or the
        // window would allow a second jump in mid-air.
        if (!IsGrounded) return;
        lastGroundedTime = -999f;

        // v = sqrt(2 * g * h). This is why jumpHeight is a real measurement
        // rather than a magic number you tune blind.
        float launchSpeed = Mathf.Sqrt(2f * jumpHeight * -Physics.gravity.y);

        Vector3 velocity = rb.linearVelocity;
        velocity.y = launchSpeed;
        rb.linearVelocity = velocity;
    }

    void ApplyFallGravity()
    {
        // Real gravity makes the top of a jump feel floaty. Extra gravity
        // only while descending keeps the rise natural and the fall weighty.
        if (rb.linearVelocity.y >= 0f) return;
        rb.AddForce(Physics.gravity * (fallGravityMultiplier - 1f), ForceMode.Acceleration);
    }

    /// <summary>
    /// THE BODY FACES THE CAMERA. ALWAYS. THIS IS NOT A TUNING CHOICE.
    ///
    /// An earlier attempt turned the body toward the direction of travel, so
    /// that walking left swung the whole character left. That is how PEAK
    /// looks, and it is NOT what this game wants:
    ///
    ///     "The character orientation is controlled by the camera, not by the
    ///      movement input. Do not rotate the character toward the movement
    ///      direction. This is the most important requirement."
    ///
    /// Press S and you walk backwards while still facing forward. Press A and
    /// you strafe left while still facing forward. Where you LOOK and where
    /// you GO are two independent things, and the body only ever answers the
    /// first one.
    ///
    /// That leaves a real problem - a character whose chest never turns has to
    /// show direction some other way, or every sideways movement is a shuffle.
    /// It is answered in ProceduralLegs, not here. The feet step in the true
    /// world direction of travel while the body holds still above them, which
    /// is what a person actually does when they side-step.
    ///
    /// So this file went back to exactly what it was, and the whole problem
    /// moved to the legs where it belongs.
    /// </summary>
    void FaceCameraYaw()
    {
        if (Cam == null) return;

        // Instant, not smoothed. Smoothing puts the body a few degrees behind
        // the camera during a fast mouse turn, and in first person that is
        // your own shoulders and arms lagging behind your view - which reads
        // as input lag on the one thing that must never have any.
        //
        // MoveRotation, not transform.rotation. Writing the transform of a
        // Rigidbody teleports it as far as the solver is concerned, which
        // corrupts contacts and makes constraints explode.
        rb.MoveRotation(Quaternion.Euler(0f, Cam.eulerAngles.y, 0f));
    }



    // Green when grounded, red when airborne. Free in a build, saves hours.
    void OnDrawGizmosSelected()
    {
        var c = GetComponent<CapsuleCollider>();
        if (c == null) return;

        Gizmos.color = Application.isPlaying && grounded ? Color.green : Color.red;
        Vector3 origin = transform.position + Vector3.up * c.radius;
        Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, c.radius * 0.95f);
    }
}