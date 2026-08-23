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
    [Tooltip("Top horizontal speed in metres per second. A brisk jog is 4-5.")]
    public float moveSpeed = 4.5f;

    [Tooltip("How fast horizontal velocity can change while standing on " +
             "something. High = snappy. Lower it if the character feels twitchy.")]
    public float groundAcceleration = 60f;

    [Tooltip("Same, airborne. KEEP THIS SMALL. It is what forces players to " +
             "swing on the rope rather than fly. Raising it will quietly ruin " +
             "the game.")]
    public float airAcceleration = 8f;

    [Header("Jump")]
    [Tooltip("Peak height of a standing jump, in metres. Launch velocity is " +
             "calculated from this and gravity, so the number means what it says.")]
    public float jumpHeight = 1.1f;

    [Tooltip("Extra gravity while falling. 1 = realistic. Higher feels snappier. " +
             "Only affects the fall, never the rise.")]
    public float fallGravityMultiplier = 1.8f;

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
    Transform cam;

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
    public bool IsLocal { get; private set; }

    public void MarkLocal(bool value) => IsLocal = value;

    /// <summary>
    /// Which crew member this body IS - the key its HP, bleed-out, Lost
    /// flag and backpack are stored under in Crew, which has to survive the
    /// scene being destroyed and rebuilt between rounds.
    ///
    /// Handed out by PlayerRegistry in registration order. Solo is always
    /// slot 0, so the same body gets its own HP back every round with no
    /// bookkeeping. Phase 4 replaces it with a network identity.
    /// </summary>
    public int Slot { get; private set; }

    public void AssignSlot(int slot) => Slot = slot;

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

    void Start()
    {
        cam = Eye;

        if (cam == null)
            Debug.LogError("[PlayerMotor] No camera has claimed this body. A " +
                           "FirstPersonCamera must have its target set to it.");
    }

    // --------------------------------------------------------------------
    // INPUT
    // Player Input is on "Send Messages", so Unity calls these by name on
    // every component of this GameObject. Each player has their own Player
    // Input component, so each gets their own messages - which is what makes
    // local multiplayer work later without changing this code.
    // --------------------------------------------------------------------

    void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

    void OnLook(InputValue value)
    {
        LookInput = value.Get<Vector2>();

        // Mouse look arrives as pixels moved since last frame; stick look as
        // a constant -1..1 while held. They need different scaling, so the
        // camera has to know which one it is.
        UsingGamepad = playerInput != null && playerInput.currentControlScheme == "Gamepad";
    }

    void OnJump(InputValue value)
    {
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
        jumpQueued = true;
    }

    // --------------------------------------------------------------------
    // PHYSICS. Everything touching the Rigidbody happens in FixedUpdate,
    // in lockstep with the simulation. Doing physics in Update is the most
    // common cause of jittery, framerate-dependent movement in Unity.
    // --------------------------------------------------------------------

    void FixedUpdate()
    {
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
        if (cam == null) return;

        // Being carried. Carryable.PickUp turns the body kinematic so it can
        // be positioned by the carrier, and AddForce on a kinematic body does
        // nothing except waste a call and confuse the next person to read a
        // profiler. The downed player is not driving anyway - SpeedFactor is
        // already 0 - but this is the difference between "asks for zero speed"
        // and "is not asking".
        if (rb.isKinematic) return;

        // Movement is relative to where the camera looks, flattened so that
        // looking up or down never changes how fast you walk.
        Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

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
        float acceleration = grounded ? groundAcceleration : airAcceleration;
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

    void FaceCameraYaw()
    {
        if (cam == null) return;

        // MoveRotation, not transform.rotation. Writing to the transform of a
        // Rigidbody teleports it as far as the solver is concerned, which
        // corrupts contacts and makes constraints explode.
        rb.MoveRotation(Quaternion.Euler(0f, cam.eulerAngles.y, 0f));
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