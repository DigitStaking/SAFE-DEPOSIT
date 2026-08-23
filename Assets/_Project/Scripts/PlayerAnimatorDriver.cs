// PlayerAnimatorDriver.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerAnimatorDriver.cs
//
// Translates what the player is DOING into Animator parameters. It does not
// decide anything and it does not move anything - PlayerMotor owns the
// physics, this only reports on it.
//
// ========================================================================
// DESIGN RULE: THIS SCRIPT READS, IT NEVER WRITES TO GAMEPLAY.
//
// Every value below is derived from state that already exists - velocity,
// grounded, hands full. Nothing else in the project had to be modified to
// add animation, and nothing breaks if this component is deleted.
// Animation is a LISTENER, never a participant.
//
// That is why pickup and stow are detected by watching for a change rather
// than by PlayerCarry calling us: it keeps the dependency one-directional.
// ========================================================================

using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(20)]
public class PlayerAnimatorDriver : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Animator on PlayerModel_FBX_VISUAL. Auto-found if empty.")]
    public Animator animator;

    [Header("Tuning")]
    [Tooltip("Movement speed MoveZ = 1 corresponds to. Match PlayerMotor.moveSpeed " +
             "or the walk cycle plays at the wrong rate and the feet slide.")]
    public float walkSpeed = 4.5f;

    [Tooltip("Smoothing on the blend tree inputs. Too low = twitchy, too high = mushy.")]
    public float moveDamp = 0.10f;

    [Tooltip("Upward speed at the moment you leave the ground that counts as a jump " +
             "rather than walking off a ledge.")]
    public float jumpDetectSpeed = 1.0f;

    [Header("Emote keys")]
    public bool emotesEnabled = true;

    // ---- parameter hashes. StringToHash once, not every frame. ----
    static readonly int MoveXId  = Animator.StringToHash("MoveX");
    static readonly int MoveZId  = Animator.StringToHash("MoveZ");
    static readonly int SpeedId  = Animator.StringToHash("Speed");
    static readonly int VelYId   = Animator.StringToHash("VelY");
    static readonly int GroundId = Animator.StringToHash("Grounded");
    static readonly int JumpId   = Animator.StringToHash("Jump");
    static readonly int CarryId  = Animator.StringToHash("Carry");
    static readonly int PickUpId = Animator.StringToHash("DoPickUp");
    static readonly int StowId   = Animator.StringToHash("DoStow");
    static readonly int UseId    = Animator.StringToHash("DoUse");
    static readonly int EmoteId  = Animator.StringToHash("Emote");
    static readonly int DoEmoteId= Animator.StringToHash("DoEmote");
    static readonly int DownedId = Animator.StringToHash("Downed");
    static readonly int DoStunId = Animator.StringToHash("DoStun");

    const int ArmsLayer = 1;

    PlayerMotor motor;
    PlayerCarry carry;
    PlayerHealth health;
    PlayerBackpack pack;
    Rigidbody rb;

    bool wasGrounded = true;
    bool wasCarrying;
    int  lastPackCount;
    float armsWeight = 1f;
    bool downed;

    void Awake()
    {
        motor  = GetComponent<PlayerMotor>();
        carry  = GetComponent<PlayerCarry>();
        health = GetComponent<PlayerHealth>();
        pack   = GetComponent<PlayerBackpack>();
        rb     = GetComponent<Rigidbody>();

        if (animator == null)
        {
            var visual = transform.Find("PlayerModel_FBX_VISUAL");
            if (visual != null) animator = visual.GetComponentInChildren<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (motor != null) walkSpeed = motor.moveSpeed;
    }

    void Start()
    {
        if (pack != null) lastPackCount = pack.Count;
    }

    void Update()
    {
        if (animator == null || !animator.enabled) return;
        if (animator.runtimeAnimatorController == null) return;

        float dt = Time.deltaTime;

        // ---------------------------------------------------------------
        // MOVEMENT
        //
        // Velocity is converted into the MODEL's local space, not the world's.
        // The blend tree asks "am I moving forward or sideways relative to
        // where I am facing" - a world-space vector cannot answer that, and
        // you would strafe while walking north.
        // ---------------------------------------------------------------
        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        float speed = flat.magnitude;

        Transform facing = animator.transform;
        Vector3 local = facing.InverseTransformDirection(flat);
        float unit = Mathf.Max(0.1f, walkSpeed);

        animator.SetFloat(MoveXId, local.x / unit, moveDamp, dt);
        animator.SetFloat(MoveZId, local.z / unit, moveDamp, dt);
        animator.SetFloat(SpeedId, speed, moveDamp, dt);

        // ---------------------------------------------------------------
        // GROUND AND JUMP
        //
        // Jump is detected, not requested. The frame the feet leave the floor
        // while still moving upward is a jump; leaving the floor while moving
        // downward is walking off a ledge, and only one of those deserves a
        // launch animation. This means PlayerMotor needed no changes at all.
        // ---------------------------------------------------------------
        // Raw, unsmoothed vertical speed. The Falling state is gated on this
        // rather than on a timer, so that a short hop never reaches the
        // skydiving clip. Not damped - a lag here would let the free-fall
        // pose linger after you have already landed.
        animator.SetFloat(VelYId, vel.y);

        bool grounded = motor == null || motor.IsGrounded;
        bool strict   = motor == null || motor.IsGroundedStrict;

        if (wasGrounded && !strict && vel.y > jumpDetectSpeed)
            animator.SetTrigger(JumpId);

        wasGrounded = strict;
        animator.SetBool(GroundId, grounded);

        // ---------------------------------------------------------------
        // CARRY
        // ---------------------------------------------------------------
        int carryLevel = 0;
        if (carry != null && carry.IsCarrying)
        {
            float sm = carry.SpeedMultiplier;      // small 1, heavy ~0.7, massive ~0.45
            carryLevel = sm <= 0.5f ? 3 : sm <= 0.85f ? 2 : 1;
        }
        animator.SetInteger(CarryId, carryLevel);

        // Hands went from empty to full - that was a pickup.
        bool carrying = carryLevel > 0;
        if (carrying && !wasCarrying) animator.SetTrigger(PickUpId);
        wasCarrying = carrying;

        // Something entered the backpack - that was a stow.
        if (pack != null)
        {
            if (pack.Count > lastPackCount) animator.SetTrigger(StowId);
            lastPackCount = pack.Count;
        }

        // ---------------------------------------------------------------
        // ARMS LAYER WEIGHT
        //
        // The case the avatar mask cannot handle on its own:
        //
        //   emoting - emotes are full-body on the BASE layer. If the arms
        //   layer kept holding a carry pose, the dance would have the legs of
        //   a dancer and the arms of a removal man.
        //
        // Solved by fading the whole layer out rather than fighting it state
        // by state.
        // ---------------------------------------------------------------
        if (animator.layerCount > ArmsLayer)
        {
            bool emoting = animator.GetCurrentAnimatorStateInfo(0).IsTag("FreeArms") ||
                           animator.GetNextAnimatorStateInfo(0).IsTag("FreeArms");

            float target = emoting ? 0f : 1f;
            armsWeight = Mathf.MoveTowards(armsWeight, target, dt * 6f);
            animator.SetLayerWeight(ArmsLayer, armsWeight);
        }

        // ---------------------------------------------------------------
        // DOWNED
        //
        // The kneel state, the Downed bool and the emote guard have all been
        // sitting in this file and in AnimatorBuilder since Phase 1, waiting
        // for something to say WHEN. PlayerHealth is that something.
        //
        // POLLED every frame rather than driven by PlayerHealth's Downed
        // EVENT, and the reason is Campaign: HP survives a scene reload, so a
        // player who bled out last run is still at 0 when the next scene
        // loads - and the event fired in a scene that no longer exists. An
        // event-driven version puts a 0-HP player back on their feet every
        // time the level rebuilds. Polling a value cannot miss an edge.
        // ---------------------------------------------------------------
        if (health != null && downed != health.IsDowned)
            SetDowned(health.IsDowned);

        // ---------------------------------------------------------------
        // EMOTES
        // ---------------------------------------------------------------
        if (emotesEnabled && PlayerRegistry.IsLocalFor(this)) ReadEmoteKeys();
    }

    void ReadEmoteKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if      (kb.zKey.wasPressedThisFrame) PlayEmote(1);   // wave
        else if (kb.xKey.wasPressedThisFrame) PlayEmote(2);   // point
        else if (kb.cKey.wasPressedThisFrame) PlayEmote(3);   // hip hop dance
        else if (kb.bKey.wasPressedThisFrame) PlayEmote(4);   // clap
        else if (kb.nKey.wasPressedThisFrame) PlayEmote(5);   // salute
        else if (kb.mKey.wasPressedThisFrame) PlayEmote(6);   // silly dance
    }

    // ---- public hooks for the rest of the game --------------------------

    /// <summary>1 wave, 2 point, 3 dance, 4 clap, 5 salute, 6 silly dance.</summary>
    public void PlayEmote(int id)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // Emoting while downed would look absurd and would also fight the
        // kneel pose on the base layer.
        if (downed) return;

        animator.SetInteger(EmoteId, id);
        animator.SetTrigger(DoEmoteId);
    }

    /// <summary>Call from keypads, the winch, puzzle switches.</summary>
    public void PlayUse()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetTrigger(UseId);
    }

    /// <summary>Trap hits, falling debris. Half a second of "not in control".</summary>
    public void PlayStun()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetTrigger(DoStunId);
    }

    /// <summary>
    /// Knocked down, waiting for a teammate. NOT death - pass false to revive.
    ///
    /// When a PlayerHealth is present it drives this from Update and is the
    /// only writer, so calling it by hand will simply be corrected on the next
    /// frame. That is deliberate: HP is the single source of truth for whether
    /// you are down, and the alternative is two systems assigning one flag -
    /// exactly the bug that PlayerMotor.speedMultiplier turned out to be.
    /// </summary>
    public void SetDowned(bool value)
    {
        downed = value;
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(DownedId, downed);
    }

    public bool IsDowned => downed;
}
