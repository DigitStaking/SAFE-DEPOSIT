// PlayerTether.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerTether.cs
// Goes on: the Player root, alongside PlayerMotor.
//
// ========================================================================
// THE MODEL  (this changed - read it)
//
// The tether is SHORT, about 2.5m. You hang just under your clip point,
// gravity always pulls you back beneath the rope, and it reads as a rope.
//
// An earlier version used an 8m tether. That was wrong: with that much
// slack you are not a person on a rope, you are a person with a very long
// leash who happens to be near one. You almost never reach the limit, so
// there is no pendulum and nothing pulls you back to centre.
//
// The consequence of a short tether is that you cannot reach a doorway by
// dangling. Correct - THE ROPE MOVES, NOT YOU. Three ways to move it:
//
//   Q       hook it and drag it to where you stand      (reliable)
//   walls   push off while taut and the rope bends      (skill)
//   SPACE   leap off it entirely toward where you look  (commitment)
//
// THREE STATES
//
//   CLIPPED   constrained to tetherLength of your clip point. Line visible
//             to the crew. Full speed.
//   LEAPING   you pressed Space. Free, full speed, and touching the rope
//             clips you straight back on. A quick trip into a room.
//   CUT       you pressed F. Free, but 60% speed, your line is invisible to
//             everyone, and auto-reclip is disabled for a few seconds so
//             swinging past the rope on your way out does not grab you.
//
// THE CONSTRAINT
//
// Inside tetherLength nothing happens - free fall. At the limit we cancel
// only the velocity pointing AWAY from the clip point. Sideways motion
// survives untouched, which turns a fall into a swing by itself. That one
// subtraction is where every pendulum behaviour comes from. There is no
// swing code anywhere.
// ========================================================================

using UnityEngine;
using UnityEngine.InputSystem;

// Runs AFTER PlayerMotor (default 0). The motor spends its acceleration
// budget first, then the rope has the final say on what is physically
// allowed. Reversed, the controller could walk through a taut rope for one
// step every frame.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody))]
public class PlayerTether : MonoBehaviour
{
    public enum TetherState { Clipped, Leaping, Cut }

    [Header("Rope")]
    [Tooltip("Leave empty - found automatically.")]
    public MainRope rope;

    [Header("Reach - self-retracting lanyard")]
    // ------------------------------------------------------------------
    // Modelled on a real fall-arrest lanyard: the spool on your back.
    //
    // It PAYS OUT under a slow steady pull - walk away from the rope and
    // line feeds out, so you can get into a room.
    // It LOCKS instantly under shock load - fall, and it catches you dead.
    //
    // That one behaviour settles the argument we kept having. Short on the
    // rope, long in a room, automatically, with no button. Falling gives you
    // the pendulum; walking gives you the reach.
    // ------------------------------------------------------------------

    [Tooltip("Resting length, in metres. What the spool retracts back to " +
             "whenever the line goes slack. Keep it short - this is what makes " +
             "hanging on the rope feel like a rope.")]
    public float shortTether = 2.5f;

    [Tooltip("Longest the spool will ever pay out. This is your reach into a " +
             "room. Buyable as a shop upgrade later.")]
    public float maxTether = 10f;

    [Tooltip("Metres per second the spool winds line back in once you are off " +
             "the ground. Multiplied internally - it needs to be quick, because " +
             "a pendulum only reads as a rope catching you if it happens fast.")]
    public float reelSpeed = 0.9f;

    [Tooltip("Spare slack kept beyond your actual distance while standing, so " +
             "the line never quite bites and you are never dragged on your feet.")]
    public float reelDeadzone = 0.6f;

    [Tooltip("How much line you may have out and still be able to swing.\n\n" +
             "Beyond shortTether + this, air control switches OFF entirely - " +
             "you are hanging limp on slack with nothing to push against. " +
             "Small: this is meant to be a clear on/off state, not a gradient.")]
    public float swingSlack = 0.8f;

    // The live length. Read by the constraint and the HUD.
    public float TetherLength => currentTether;
    float currentTether = 2.5f;

    [Tooltip("Set by the spool at runtime - shown here so you can watch it " +
             "work. Editing it does nothing.")]
    [SerializeField] bool spoolLocked;

    [Header("Descent")]
    [Tooltip("Metres down the rope. Set automatically on start.")]
    public float attachDepth;

    [Tooltip("Metres per second paying out line. Down should feel quick and " +
             "slightly out of control.")]
    public float descendSpeed = 4f;

    [Tooltip("Metres per second hauling up. Far slower than descending on " +
             "purpose - climbing out is the hard part of the game and this is " +
             "the number that makes it hard.")]
    public float ascendSpeed = 1.6f;

    [Header("Jump off the rope (Space)")]
    [Tooltip("Seconds the spool stays fully released after a jump.\n\n" +
             "Long enough to complete an arc into a doorway; short enough that " +
             "the line reels you back afterwards rather than leaving you " +
             "drifting on ten metres of slack.")]
    public float jumpSlackTime = 2.5f;

    [Tooltip("Seconds between jumps. Without a cooldown you can spam your way " +
             "anywhere and the swinging skill disappears.")]
    public float jumpCooldown = 0.6f;

    [Tooltip("How much of your look direction is mixed into a jump made from " +
             "solid ground. 0 is straight up; 0.35 is a natural forward hop. " +
             "Keep it low - you cannot launch yourself sideways off a floor.")]
    [Range(0f, 1f)] public float groundJumpLean = 0.35f;

    [Tooltip("Speed you launch at when leaping off the rope, in m/s. This is " +
             "how you get into a room. If you can never make it across, raise " +
             "this; if you make it every time without thinking, lower it.")]
    public float leapSpeed = 7f;

    [Tooltip("Upward bias baked into every leap so it never just drives you " +
             "into the floor.")]
    public float leapUpBias = 0.3f;

    [Header("Cut tether (F)")]
    [Tooltip("How close to the rope you must be to clip back on.")]
    public float reclipRange = 1.8f;

    [Tooltip("Seconds after a DELIBERATE cut during which auto-clip is off. " +
             "Without this, cutting loose to reach a far room would re-clip you " +
             "the instant you passed the rope on the way out. Does not apply to " +
             "leaping - a leap always wants you back.")]
    public float autoClipGrace = 3f;

    [Tooltip("Movement speed while cut. Below 1 - cutting buys range at the " +
             "cost of everything else. Leaping has no penalty.")]
    [Range(0.2f, 1f)] public float cutSpeedMultiplier = 0.6f;

    [Header("Constraint feel")]
    [Tooltip("How fast stretch is pulled out, per second. The correction is " +
             "CLAMPED so it can never overshoot the limit, which is what stops " +
             "the rope vibrating.")]
    public float correctionRate = 8f;

    [Tooltip("Slop before the rope counts as taut, so the constraint does not " +
             "flicker on and off at the exact limit.")]
    public float slackDeadzone = 0.02f;

    [Tooltip("Hard ceiling on how fast the rope may drag you, in m/s.\n\n" +
             "A safety net. If anything ever moves the rope a long way in one " +
             "step, this makes the constraint pull you along firmly rather " +
             "than flinging you across the shaft. Around 8 is a brisk but " +
             "survivable haul.")]
    public float maxCorrectionSpeed = 4.5f;

    [Tooltip("Fastest the winch can ever haul you toward the rope, in m/s.\n\n" +
             "Separate from the correction cap and much lower, because inward " +
             "velocity ACCUMULATES: without this, holding T built up speed you " +
             "kept the instant you let go, and the character launched.")]
    public float maxReelSpeed = 1.4f;

    [Tooltip("Bleeds energy out of swings so they settle. Zero swings forever.")]
    public float swingDamping = 0.22f;

    [Header("Rope feedback")]
    [Tooltip("How hard this player drags the rope sideways. Raise it until " +
             "pushing off a wall visibly bends the line - that bend is how you " +
             "reach doorways without the hook.")]
    public float pullStrength = 1.6f;

    [Header("Visual")]
    public LineRenderer tetherLine;

    [Tooltip("Where the tether visually attaches on the body. Use ChestPivot.")]
    public Transform tetherOrigin;

    [Header("Prototype UI")]
    public bool showHints = true;

    [Tooltip("Shows live input state at the top of the screen. Turn this on " +
             "when a key seems to do nothing - it tells you instantly whether " +
             "the input is arriving or the game is refusing the action.")]
    public bool showInputDebug = true;

    public TetherState State { get; private set; } = TetherState.Clipped;
    public bool IsAttached => State == TetherState.Clipped;
    public bool IsTaut { get; private set; }

    /// <summary>
    /// Line fully paid out and still pulling. The only situation where the
    /// rope actually stops you going somewhere - so it gets its own prompt.
    /// </summary>
    public bool AtFullExtension { get; private set; }

    /// <summary>
    /// No movement input at all. Used to decide when the spool may act on its
    /// own - it is only ever allowed to move the line while you are doing
    /// nothing, so it can never fight you.
    /// </summary>
    bool StandingStill => motor == null || motor.MoveIntent < 0.15f;
    public float Depth => attachDepth;

    Rigidbody rb;
    PlayerArms arms;
    PlayerMotor motor;
    PlayerCarry carry;
    Transform cam;

    bool descendHeld, ascendHeld, reelHeld;
    UnityEngine.InputSystem.InputAction descendAction, ascendAction, reelAction;
    float cutTime = -999f;

    // False from the moment you leave the rope until you have actually got
    // clear of it. Blocks the auto-reclip from immediately undoing your leap.
    bool hasClearedRope;

    // Used to measure how fast the clip point is travelling, so the spool can
    // hold still while a hook drags the rope around rather than chasing it.
    Vector3 lastAttachPoint;
    bool lastAttachValid;

    // While this is in the future the spool stays fully released, so a jump
    // gets its whole arc before the line starts pulling you home.
    float jumpSlackUntil = -99f;
    float nextJumpTime;

    string lastRefusal = "";
    float refusalTime = -999f;

    // True only while T is actively shortening the line this physics step.
    bool isActuallyReeling;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        arms = GetComponent<PlayerArms>();
        motor = GetComponent<PlayerMotor>();
        carry = GetComponent<PlayerCarry>();
    }

    void Start()
    {
        if (rope == null) rope = FindFirstObjectByType<MainRope>();
        if (rope == null)
        {
            Debug.LogError("[PlayerTether] No MainRope found in the scene.");
            enabled = false;
            return;
        }

        if (Camera.main != null) cam = Camera.main.transform;

        // Grab the hold-style actions so we can poll them instead of relying
        // on messages that can be missed. See PollHoldInputs.
        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
        {
            descendAction = playerInput.actions.FindAction("Descend", false);
            ascendAction  = playerInput.actions.FindAction("Ascend",  false);
            reelAction    = playerInput.actions.FindAction("ReelIn",  false);

            if (descendAction == null || ascendAction == null)
                Debug.LogWarning("[PlayerTether] Could not find the Descend/Ascend " +
                                 "actions by name. Check the spelling in PlayerControls.inputactions.");
        }

        // Clip in at whatever depth we already are, not at the top. Otherwise
        // a player placed at the bottom is yanked 20m upward on frame one.
        attachDepth = Mathf.Clamp(rope.AnchorPosition.y - rb.position.y, 0f, rope.Length);
    }

    // --------------------------------------------------------------------
    // INPUT. Send Messages delivers these to every component on this object.
    // --------------------------------------------------------------------

    // Kept only as a fallback for setups where the action asset cannot be
    // found. The real reading is POLLED in FixedUpdate - see PollHoldInputs.
    void OnDescend(InputValue value) { if (descendAction == null) descendHeld = value.isPressed; }
    void OnAscend(InputValue value)  { if (ascendAction  == null) ascendHeld  = value.isPressed; }
    void OnReelIn(InputValue value)  { if (reelAction    == null) reelHeld    = value.isPressed; }

    /// <summary>
    /// Reads the hold-style buttons straight from the action asset instead of
    /// waiting for messages.
    ///
    /// THIS IS THE FIX FOR CTRL AND SHIFT WORKING ONLY SOMETIMES.
    ///
    /// "Send Messages" fires once on press and once on release, and we cached
    /// the result in a bool. If a release message was ever missed - window
    /// focus lost, control scheme switched, action re-enabled mid-press - the
    /// flag stayed true forever. And with BOTH descend and ascend stuck on,
    /// they cancel each other out and the controls appear to die at random.
    /// That is exactly what the debug line showed: "descend HELD  ascend HELD".
    ///
    /// Polling has no stuck state to get into. If the key is not down it reads
    /// false, every step, always.
    ///
    /// Still per-player: playerInput.actions belongs to this player's own
    /// device, so local multiplayer is unaffected.
    /// </summary>
    void PollHoldInputs()
    {
        if (descendAction != null) descendHeld = descendAction.IsPressed();
        if (ascendAction  != null) ascendHeld  = ascendAction.IsPressed();
        if (reelAction    != null) reelHeld    = reelAction.IsPressed();
    }

    void OnJump(InputValue value)
    {
        if (!value.isPressed || cam == null) return;

        if (State != TetherState.Clipped) return;

        // Long line in the air: only T brings you back. Space is locked.
        bool groundedNow = motor != null && motor.IsGrounded;
        bool longLine = currentTether > shortTether + swingSlack;
        if (!groundedNow && longLine)
        {
            Refuse("long line — HOLD T to reel in. Space is locked");
            return;
        }

        if (carry != null && !carry.CanKick)
        {
            Refuse("too heavy to leap");
            return;
        }

        if (Time.time < nextJumpTime) return;
        nextJumpTime = Time.time + jumpCooldown;

        // JUMP, STILL CLIPPED ON.
        //
        // This used to detach you, which is why jumping off the rope dropped
        // you to the bottom of the shaft. Wrong. You have a ten metre line -
        // it should pay out, let you reach a doorway, and then swing you back
        // to roughly where you left.
        //
        // So the spool is RELEASED to full length for the arc of the jump,
        // and winds back in once the window closes. Reach, then return.
        //
        // It also makes cutting your tether properly lethal: no line, no
        // catch, and the bottom of the shaft is a long way down.
        currentTether = maxTether;
        jumpSlackUntil = Time.time + jumpSlackTime;

        bool grounded = motor != null && motor.IsGrounded;

        if (grounded)
        {
            // STANDING: a real jump. Mostly up, with a little of where you are
            // looking mixed in.
            //
            // It used to fire you along your look direction whatever you were
            // doing - so glancing down while standing drove you into the floor
            // and read as "jump does nothing sometimes". You cannot jump
            // sideways off a floor. You can off a rope.
            Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 dir = (Vector3.up + flat * groundJumpLean).normalized;
            rb.AddForce(dir * leapSpeed, ForceMode.VelocityChange);
        }
        else
        {
            // HANGING: push where you are looking, with a little lift so it
            // never just drives you downward.
            Vector3 dir = (cam.forward + Vector3.up * leapUpBias).normalized;
            rb.AddForce(dir * leapSpeed, ForceMode.VelocityChange);
        }
    }

    void OnToggleTether(InputValue value)
    {
        if (!value.isPressed) return;

        if (State == TetherState.Clipped)
        {
            // Cut is free. Reclip is manual F only (no auto-grab).
            // Intended room entry: Q pin → F cut → loot → F reclip.
            State = TetherState.Cut;
            IsTaut = false;
            AtFullExtension = false;
            cutTime = Time.time;
            hasClearedRope = false;
            if (motor != null) motor.airControlBlocked = false;
        }
        else if (CanReclip())
        {
            ClipOn();
        }
        else
        {
            Refuse("too far from the rope - get closer, then F to clip on");
        }
    }

    // --------------------------------------------------------------------
    // PHYSICS
    // --------------------------------------------------------------------

    void FixedUpdate()
    {
        if (rope == null) return;

        // The anchor tore out. Free fall for everyone on the line.
        if (rope.Snapped)
        {
            State = TetherState.Cut;
            IsTaut = false;
            AtFullExtension = false;
            if (motor != null)
            {
                float m = 1f;
                if (carry != null) m *= carry.SpeedMultiplier;
                motor.speedMultiplier = m;
                motor.airControlBlocked = false;
            }
            UpdateArmPose();
            return;
        }

        PollHoldInputs();

        // NO auto-reclip. F cut means off until YOU press F near the rope.
        if (State != TetherState.Clipped)
        {
            IsTaut = false;
            AtFullExtension = false;
            ApplySpeedPenalties();

            if (descendHeld || ascendHeld)
                Refuse("not on the rope - press F near it to clip on");

            if (reelHeld)
                Refuse("not on the rope");

            UpdateArmPose();
            return;
        }

        ApplySpeedPenalties();
        UpdateDepth();
        UpdateSpool();
        ApplyConstraint();
        rope.AddLoad(TotalMass);
        UpdateArmPose();
    }

    float TotalMass => rb.mass + (carry != null ? carry.CarriedMass : 0f);

    // Records why the game just refused an action, with a timestamp so the
    // message fades instead of sticking around forever. Every refusal in this
    // script goes through here - if a key ever appears to do nothing, the
    // reason is on screen within a frame.
    void Refuse(string reason)
    {
        lastRefusal = reason;
        refusalTime = Time.time;
    }

    void ApplySpeedPenalties()
    {
        if (motor == null) return;

        // Leaping costs nothing. Only a deliberate cut slows you down.
        float multiplier = State == TetherState.Cut ? cutSpeedMultiplier : 1f;
        if (carry != null) multiplier *= carry.SpeedMultiplier;
        motor.speedMultiplier = multiplier;

        // YOU CANNOT STEER ON A LONG LINE.
        //
        // Swinging needs a short tether - that is the only way you get any
        // leverage against the rope. With metres of slack out you are limp on
        // the end of it, and steering would be nonsense.
        //
        // So: clipped, in the air, more than swing length out -> no air
        // control at all. Hold T to bring it in, then you can swing.
        //
        // Only while CLIPPED. Cut loose you are on your own and always have
        // full control, which is exactly what you paid for.
        motor.airControlBlocked =
            State == TetherState.Clipped &&
            currentTether > shortTether + swingSlack &&
            Time.time > jumpSlackUntil;
    }

    void UpdateDepth()
    {
        float direction = (descendHeld ? 1f : 0f) - (ascendHeld ? 1f : 0f);
        if (Mathf.Approximately(direction, 0f)) return;

        // Shift/Ctrl are rope-climb controls, not ground controls.
        // On the ground they made the tether clip slide up/down the main rope
        // while the player stayed put, turning the line horizontal and causing
        // distance/vibration bugs. Ground movement should only feed/pull line;
        // rope depth changes happen after you are hanging.
        if (motor != null && motor.IsGrounded)
        {
            Refuse("climb controls only work while hanging");
            return;
        }

        // You can only climb the main rope while clipped short enough to have
        // your hands on it. At 10m you are on a loose tether; first hold T to
        // reel back to the 2.5m working length, then Shift/Ctrl can move depth.
        if (currentTether > shortTether + 0.25f)
        {
            Refuse($"reel in to {shortTether:0.0}m before climbing");
            return;
        }

        // Heavy things need both hands. You cannot haul yourself up holding
        // one - you become dependent on the winch, or on a friend. That
        // dependency is the co-op.
        if (direction < 0f && carry != null && !carry.CanClimb)
        {
            Refuse("too heavy to climb - drop it or clip it to the rope");
            return;
        }

        if (direction < 0f && attachDepth <= 0.01f)
        {
            Refuse("at the top of the rope - this is the way out");
            return;
        }

        if (direction > 0f && attachDepth >= rope.Length - 0.01f)
        {
            Refuse("end of the rope - buy more to go deeper");
            return;
        }

        float speed = direction > 0f ? descendSpeed : ascendSpeed;
        attachDepth = Mathf.Clamp(attachDepth + direction * speed * Time.fixedDeltaTime,
                                  0f, rope.Length);
    }

    /// <summary>
    /// The self-retracting lanyard.
    ///
    /// Real fall-arrest spools work on rate, not on force: a slow steady pull
    /// feeds line, a sudden one jams the drum. Copying that exactly gives us
    /// both behaviours we wanted from one rule -
    ///
    ///   walk out of the shaft into a room  ->  slow  ->  line feeds out
    ///   fall off the rope                  ->  fast  ->  locks, you swing
    ///
    /// which is why there is no button for this and no mode to remember.
    /// </summary>
    void UpdateSpool()
    {
        Vector3 attachPoint = rope.PointAtDepth(attachDepth);
        Vector3 offset = rb.position - attachPoint;
        float distance = offset.magnitude;

        // How fast the clip point itself is travelling. While a hook is
        // dragging the rope past you, the spool should sit still and let it
        // happen rather than chasing a moving target - chasing it is half of
        // where the vibration came from.
        float attachSpeed = lastAttachValid
            ? (attachPoint - lastAttachPoint).magnitude / Time.fixedDeltaTime
            : 0f;
        lastAttachPoint = attachPoint;
        lastAttachValid = true;

        bool ropeIsMoving = attachSpeed > 0.6f;

        bool grounded = motor != null && motor.IsGrounded;
        spoolLocked = !grounded;

        // ----------------------------------------------------------------
        // ONE RULE, TWO BEHAVIOURS:
        //
        //   FEET ON SOMETHING  ->  the line always has enough slack to reach
        //                          you. It NEVER pulls a standing player.
        //   IN THE AIR         ->  it winds back to the short length, catches
        //                          you, and you swing.
        //
        // The previous version tried to model a real spool with payout rates,
        // lock thresholds and movement intent. Every one of those was a piece
        // of state that could disagree with the world, and each disagreement
        // became a feedback loop: the rope drags you, you lift off the floor,
        // the state flips, the drag changes, you land, it flips back.
        //
        // This has no rate and no lock to get stuck in. Standing is safe by
        // construction, because the length is simply defined as "far enough".
        // Falling is a pendulum, because it winds in fast and there is nothing
        // that can stop it.
        //
        // The one case that still constrains a standing player is when you
        // walk past maxTether - and that is exactly right. You have run out
        // of line.
        // ----------------------------------------------------------------

        // ----------------------------------------------------------------
        // THE SPOOL IS NOW MANUAL, AND THAT IS THE FIX.
        //
        // Every automatic version of this went wrong in a different way -
        // paying out when it should hold, winding in mid-jump, sticking at
        // ten metres forever. All of them were the game guessing what you
        // wanted from physical state, and guessing wrong.
        //
        // Three rules, no guessing:
        //
        //   FEET DOWN   line feeds out to reach you. Never pulls you.
        //   AIRBORNE    the length HOLDS wherever it is. Nothing surprises you.
        //   T           wind it in. Your choice, your timing.
        //
        // The cost is one extra button. What you get is a line that does
        // exactly what you last told it to, which is worth far more than a
        // clever spool you cannot predict.
        // ----------------------------------------------------------------

        // ----------------------------------------------------------------
        // SPOOL RULES (playtest lock):
        //
        //   FEET DOWN     line feeds out only. Never pulls. T does nothing.
        //   AIRBORNE      length HOLDS. No auto-reel. Jump slack stays out.
        //   T (air only)  shortens only while hanging under the rope.
        //                 Deep in a room: Q pin then F cut.
        // ----------------------------------------------------------------

        isActuallyReeling = false;

        if (grounded && Time.time > jumpSlackUntil)
        {
            float needed = distance + reelDeadzone;
            if (needed > currentTether) currentTether = needed;

            if (reelHeld)
                Refuse("T only while hanging under the rope");
        }
        else if (reelHeld && !grounded && !ropeIsMoving && Time.time > jumpSlackUntil)
        {
            Vector3 flat = offset;
            flat.y = 0f;
            float horizontal = flat.magnitude;

            if (horizontal > shortTether + swingSlack + 0.75f)
            {
                Refuse("too far from the line - Q pin the rope, or F cut");
            }
            else if (currentTether > shortTether + 0.02f)
            {
                currentTether = Mathf.MoveTowards(currentTether, shortTether,
                    reelSpeed * 1.15f * Time.fixedDeltaTime);
                isActuallyReeling = true;
            }
        }

        currentTether = Mathf.Clamp(currentTether, shortTether, maxTether);
    }


    void ApplyConstraint()
    {
        Vector3 attachPoint = rope.PointAtDepth(attachDepth);
        Vector3 offset = rb.position - attachPoint;
        float distance = offset.magnitude;

        if (distance <= currentTether + slackDeadzone || distance < 0.0001f)
        {
            IsTaut = false;
            AtFullExtension = false;
            return;
        }

        IsTaut = true;
        AtFullExtension = currentTether >= maxTether - 0.05f;
        Vector3 dir = offset / distance;   // clip point -> player

        // --- 1. cancel outward velocity ---
        // The one line that makes this rope and not elastic. Only the part
        // pointing away from the clip point is removed; everything sideways
        // survives, which is what turns a fall into a swing.
        float outward = Vector3.Dot(rb.linearVelocity, dir);
        if (outward > 0f)
            rb.AddForce(-dir * outward, ForceMode.VelocityChange);

        // --- 2. pull the stretch out without overshooting ---
        // stretch / fixedDeltaTime is exactly the speed that closes the gap in
        // one step. Clamping to it means the correction approaches the limit
        // but never crosses it, so there is nothing to oscillate about. A
        // force-based spring here is what caused the earlier vibration.
        float stretch = distance - currentTether;
        float correction = Mathf.Min(stretch * correctionRate, stretch / Time.fixedDeltaTime);

        bool grounded = motor != null && motor.IsGrounded;

        // ON FEET at the limit: cancel outward only — never haul backward.
        // Hauling is what made the body vibrate past 10m.
        if (grounded)
        {
            correction = 0f;
        }
        else if (!isActuallyReeling)
        {
            // Airborne, not holding T: soft settle only, no winch yank.
            correction = Mathf.Min(correction, maxCorrectionSpeed * 0.35f);
        }
        else
        {
            // Holding T: slow controlled reel.
            correction = Mathf.Min(correction, maxCorrectionSpeed);
        }

        if (correction > 0f)
            rb.AddForce(-dir * correction, ForceMode.VelocityChange);

        // Cap inward speed. When T is released, dump residual inward velocity
        // so the character does not jump off the winch.
        float inward = -Vector3.Dot(rb.linearVelocity, dir);
        float inwardCap = isActuallyReeling ? maxReelSpeed : (grounded ? 0.05f : 0.8f);
        if (inward > inwardCap)
            rb.AddForce(dir * (inward - inwardCap), ForceMode.VelocityChange);

        // --- 3. bleed energy out of the swing ---
        Vector3 tangential = rb.linearVelocity - dir * Vector3.Dot(rb.linearVelocity, dir);
        rb.AddForce(-tangential * swingDamping, ForceMode.Acceleration);

        // --- 4. drag the rope toward us ---
        // With a short tether this is how you reach a doorway without the
        // hook: push off a wall, go taut, and the whole rope bends with you.
        // Weighted by mass, so someone carrying a bust drags it far harder.
        rope.AddPull(dir * TotalMass * pullStrength, attachDepth);
    }

    void UpdateArmPose()
    {
        if (arms == null) return;
        if (carry != null && carry.IsCarrying) return;   // carry pose wins

        bool working = State == TetherState.Clipped && (descendHeld || ascendHeld || IsTaut);
        arms.SetPose(working ? PlayerArms.ArmPose.Climb : PlayerArms.ArmPose.Idle);
    }

    void ClipOn()
    {
        State = TetherState.Clipped;
        hasClearedRope = false;

        // The spool winds back to its resting length when you clip on, so you
        // always start tight against the rope rather than with ten metres of
        // slack left over from the last room.
        currentTether = shortTether;

        // Search for the nearest depth rather than deriving it from Y.
        // Working it out from height alone was why you could stand right next
        // to the rope at the bottom of the shaft and still fail to clip on:
        // once the rope is kinked by a hook, its position at your height can
        // be metres away from the part of it you are actually touching.
        attachDepth = rope.NearestDepth(rb.position + Vector3.up * 0.9f);
    }

    /// <summary>
    /// You must get clear of the rope before it is allowed to grab you again.
    ///
    /// THIS IS WHY SPACE USED TO NEED TWO PRESSES. At the instant you leap you
    /// are still standing next to the rope, so one physics step later the
    /// auto-reclip found you in range and clipped you straight back on. The
    /// first press leapt and re-attached; only the second one got you clear.
    ///
    /// A timer would have worked too, but this is better: it does not care how
    /// fast you are moving, only whether you actually left. Leap hard and you
    /// are free immediately; drop half a metre and dangle, and you stay on.
    /// </summary>
    void UpdateReclipEligibility()
    {
        if (hasClearedRope) return;

        if (rope.DistanceToRope(rb.position + Vector3.up * 0.9f) > reclipRange * 1.6f)
            hasClearedRope = true;
    }

    public bool CanReclip()
    {
        if (rope == null) return false;

        // Measured from chest height, not from the feet. Feet-height was
        // unforgiving whenever you were standing on a floor beside the rope.
        return rope.DistanceToRope(rb.position + Vector3.up * 0.9f) <= reclipRange;
    }

    // --------------------------------------------------------------------
    // VISUAL. LateUpdate at render rate, or the line stutters next to the
    // smoothly interpolated player.
    // --------------------------------------------------------------------

    void LateUpdate()
    {
        if (tetherLine == null) return;

        // Cut, and your line vanishes for you and for everyone else. Being
        // invisible to the crew is the real cost of going alone.
        if (State != TetherState.Clipped || rope == null)
        {
            tetherLine.enabled = false;
            return;
        }

        tetherLine.enabled = true;
        tetherLine.positionCount = 2;
        tetherLine.useWorldSpace = true;
        tetherLine.SetPosition(0, rope.PointAtDepth(attachDepth));
        tetherLine.SetPosition(1, tetherOrigin != null ? tetherOrigin.position : transform.position);
    }

    // --------------------------------------------------------------------
    // PROTOTYPE HINTS. OnGUI: wrong for a shipping game, right for a
    // prototype - no canvas, no prefabs, ten seconds to change.
    // --------------------------------------------------------------------

    void OnGUI()
    {
        // Don't draw gameplay chrome over the results/shop screen.
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        if (rope == null) return;

        if (showInputDebug) DrawInputDebug();
        if (!showHints) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 15, alignment = TextAnchor.MiddleCenter };

        float w = 940f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 96f;

        switch (State)
        {
            case TetherState.Clipped:
                style.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y, w, 24),
                    "CTRL descend   SHIFT climb   SPACE jump   T reel in   Q pull rope   E pick up / clip   F cut tether",
                    style);
                break;

            // Kept for the anchor-snapped case and any future use. Space no
            // longer detaches you - it jumps you on a released line.
            case TetherState.Leaping:
                style.normal.textColor = new Color(0.5f, 0.9f, 1f);
                GUI.Label(new Rect(x, y, w, 24),
                    "OFF THE ROPE  -  touch it again to clip back on", style);
                break;

            case TetherState.Cut:
                            style.normal.textColor = new Color(1f, 0.55f, 0.2f);
                            GUI.Label(new Rect(x, y, w, 24),
                                CanReclip()
                                    ? "TETHER CUT  -  F clip back on  (you are near the rope)"
                                    : "TETHER CUT  -  slower, invisible.  Get to the rope, then F to clip on.",
                                style);
                            break;
        }

        style.fontSize = 13;
        style.normal.textColor = rope.Overloaded
            ? new Color(1f, 0.3f, 0.25f)
            : new Color(1f, 1f, 1f, 0.6f);

        string depth = State == TetherState.Clipped
            ? $"depth {attachDepth:0.0}m of {rope.Length:0}m" + (IsTaut ? "   taut" : "   slack")
            : "off rope";

        GUI.Label(new Rect(x, y + 24, w, 20),
            $"{depth}      load {rope.CurrentLoad:0}kg / {rope.loadLimit:0}kg" +
            (rope.Overloaded ? "   ANCHOR FAILING" : ""), style);

        // ----------------------------------------------------------------
        // THE SWING PROMPT.
        //
        // A long line does not swing - you just hang there wondering why the
        // pendulum stopped working. Nothing in the world tells you that, so
        // the game has to.
        //
        // Shown whenever your line is longer than swing length, with the
        // actual numbers, because "10.0m of line, swings under 3.5m" is
        // something a player can act on and "reel in" alone is not.
        // ----------------------------------------------------------------
        if (State == TetherState.Clipped)
        {
            var swing = new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter };

            string message = null;

            // OUT OF LINE. The one state where the rope genuinely stops you,
            // so it needs the loudest and most actionable prompt: it names the
            // exact key that lets you keep going, and what it costs.
            // Only when the rope is ACTUALLY stopping you going somewhere -
            // fully out, taut, and you are pushing against it.
            //
            // It used to fire on taut alone, which is most of the time you are
            // simply hanging there. A warning that appears during normal play
            // is a warning nobody reads.
            bool onGround = motor != null && motor.IsGrounded;

            if (isActuallyReeling)
            {
                swing.normal.textColor = new Color(0.5f, 0.9f, 1f);
                message = $"reeling in...  {currentTether:0.0}m";
            }
            else if (!onGround && motor != null && motor.airControlBlocked)
            {
                // AIR only — long slack hang. One message: T to recover.
                swing.normal.textColor = new Color(1f, 0.8f, 0.4f);
                message = $"HOLD T to reel in  ({currentTether:0.0}m → {shortTether:0.0}m)  ·  Space locked";
            }
            else if (onGround && AtFullExtension && !StandingStill)
            {
                // GROUND only — walked to the end of the line.
                swing.normal.textColor = new Color(1f, 0.45f, 0.3f);
                message = $"END OF THE LINE  -  {maxTether:0}m max.  F cuts tether to go further";
            }
            else if (onGround && currentTether > shortTether + swingSlack)
            {
                swing.normal.textColor = new Color(1f, 1f, 1f, 0.4f);
                message = $"{currentTether:0.0}m of line out";
            }

            if (message != null)
                GUI.Label(new Rect(x, Screen.height * 0.5f + 92f, w, 22f), message, swing);
        }

        // Why the last action was refused, shown big and briefly. A control
        // that silently does nothing is the worst thing in any prototype -
        // you cannot tell a broken binding from a rule you did not know about.
        if (Time.time - refusalTime < 2f && !string.IsNullOrEmpty(lastRefusal))
        {
            var warn = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            warn.normal.textColor = new Color(1f, 0.6f, 0.25f,
                Mathf.Clamp01(2f - (Time.time - refusalTime)));

            GUI.Label(new Rect(x, Screen.height * 0.5f - 60f, w, 24), lastRefusal, warn);
        }
    }

    // Turn this on whenever a key "does nothing". It separates the two
    // possible causes immediately: either the input is not arriving (a
    // binding problem) or the game is refusing the action (a rule).
    void DrawInputDebug()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        style.normal.textColor = new Color(0.6f, 1f, 0.6f);

        string line = $"state {State}    descend {(descendHeld ? "HELD" : "-")}" +
                              $"    ascend {(ascendHeld ? "HELD" : "-")}" +
                              $"    reel {(reelHeld ? "HELD" : "-")}" +
                              $"    depth {attachDepth:0.0}" +
                              $"    tether {currentTether:0.0}m" +
                              (spoolLocked ? " (airborne - hold T to reel)" : " (grounded, line feeds out)");

        if (!string.IsNullOrEmpty(lastRefusal)) line += $"    refused: {lastRefusal}";

        GUI.Label(new Rect(12f, 12f, 900f, 20f), line, style);
    }

    void OnDrawGizmosSelected()
    {
        if (rope == null || State != TetherState.Clipped) return;

        Vector3 attachPoint = rope.PointAtDepth(attachDepth);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(attachPoint, 0.2f);

        Gizmos.color = IsTaut ? new Color(1f, 0.4f, 0.1f, 0.6f)
                              : new Color(0.3f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(attachPoint, currentTether);
    }
}