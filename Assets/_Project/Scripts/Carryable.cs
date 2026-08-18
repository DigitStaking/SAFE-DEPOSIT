// Carryable.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Carryable.cs
// Goes on: every loot object.
//
// ========================================================================
// WEIGHT LIMITS WHAT YOU CAN DO, NOT JUST HOW FAST YOU MOVE.
//
//   SMALL    under 8kg    one hand, climb normally
//   HEAVY    8 - 60kg     two hands, CANNOT climb the rope, 70% speed
//   MASSIVE  over 60kg    45% speed, no climbing, no kicking off the rope
//
// The class is derived from the Rigidbody's mass rather than set by hand,
// so an object's behaviour always matches its stated weight. Change the
// mass and everything follows.
//
// The point of Heavy is not the speed penalty. It is that you cannot haul
// yourself up while holding one - you become dependent on being winched, or
// on a friend. That dependency is the co-op.
// ========================================================================

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Carryable : MonoBehaviour
{
    public enum WeightClass { Small, Heavy, Massive }

    public enum CarryState
    {
        Free,     // lying in the world
        Held,     // in a player's hands
        Stowed,   // in a player's backpack - small items only
        OnRope    // clipped to the main rope, riding it
    }

    [Header("Value")]
    [Tooltip("What the black market pays for this. Later the run's quota is " +
             "measured against the sum of these.")]
    public int value = 100;

    [Header("Rope cargo")]
    [Tooltip("Depth on the main rope once clipped. Set automatically.")]
    public float ropeDepth;

    public CarryState State { get; private set; } = CarryState.Free;
    public float Mass => body != null ? body.mass : 1f;

    /// <summary>
    /// Derived from mass, never set by hand, so behaviour can never disagree
    /// with the stated weight.
    /// </summary>
    public WeightClass Weight =>
        Mass <= 8f ? WeightClass.Small :
        Mass <= 60f ? WeightClass.Heavy :
                      WeightClass.Massive;

    public bool AllowsClimbing => Weight == WeightClass.Small;

    // Anything needing two hands stops you jumping AND leaping off the rope.
    // Only one-handed loot lets you move freely - which is the entire reason
    // the backpack exists.
    public bool AllowsKicking => Weight == WeightClass.Small;
    public bool AllowsJumping => Weight == WeightClass.Small;

    /// <summary>Small enough to go on your back and leave your hands free.</summary>
    public bool CanStow => Weight == WeightClass.Small;

    public float SpeedMultiplier => Weight switch
    {
        WeightClass.Small => 1f,
        WeightClass.Heavy => 0.7f,
        _ => 0.45f
    };

    Rigidbody body;
    Collider[] colliders;
    Renderer[] renderers;
    MainRope rope;

    int lootLayer = -1;
    int ropeLayer = -1;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();

        lootLayer = LayerMask.NameToLayer("Loot");
        ropeLayer = LayerMask.NameToLayer("Rope");

        if (ropeLayer < 0)
            Debug.LogWarning("[Carryable] Layer 'Rope' missing. Cargo on the rope " +
                             "will collide with players and shove them around.");
    }

    void Start()
    {
        rope = FindFirstObjectByType<MainRope>();
    }

    // --------------------------------------------------------------------
    // PICK UP / DROP
    //
    // A held item goes kinematic and stops colliding with anything. Keeping
    // it as a live physics body while a player drags it around is the
    // classic source of items exploding through walls, launching players
    // across the room, and jittering against the character's own collider.
    // Kinematic while held is boring and correct.
    // --------------------------------------------------------------------

    // NOT parented to anything. PlayerCarry positions this relative to the
    // CAMERA every LateUpdate instead.
    //
    // Parenting it to the body looked broken: the body only turns in
    // FixedUpdate at 60Hz while the camera turns at the monitor's refresh
    // rate, so every time the player turned the item lagged a frame behind
    // and visibly slid sideways across the screen.
    public void PickUp()
    {
        if (rope != null) rope.UnregisterCargo(this);

        transform.SetParent(null, true);   // may be coming out of the backpack

        State = CarryState.Held;
        body.isKinematic = true;
        SetCollidersEnabled(false);
        SetCollidersAsTriggers(false);
        SetRenderersEnabled(true);
    }

    /// <summary>
    /// Onto a player's back. Parented, unlike a held item - a frame of lag is
    /// invisible on something behind you, and parenting means it follows for
    /// free.
    /// </summary>
    public void Stow(Transform backAnchor)
    {
        if (rope != null) rope.UnregisterCargo(this);

        State = CarryState.Stowed;
        body.isKinematic = true;
        SetCollidersEnabled(false);
        SetCollidersAsTriggers(false);

        transform.SetParent(backAnchor, false);
        transform.localRotation = Quaternion.identity;

        // Hidden, because it is INSIDE the bag. Leaving it visible meant a
        // cube floating behind your shoulder, swinging a frame late every
        // time you turned - the body only rotates at 60Hz while the camera
        // runs at your monitor's rate.
        //
        // What you are carrying still reads at a glance: the slot boxes in
        // the corner for you, and the pack itself for everyone else once
        // there is a real model on the character's back.
        SetRenderersEnabled(false);
    }

    public void Unstow()
    {
        transform.SetParent(null, true);
        State = CarryState.Free;
        body.isKinematic = false;
        SetCollidersEnabled(true);
        SetRenderersEnabled(true);
        SetLayerRecursive(gameObject, lootLayer);
    }

    public void Drop(Vector3 velocity)
    {
        transform.SetParent(null, true);
        SetRenderersEnabled(true);

        if (rope != null) rope.UnregisterCargo(this);

        State = CarryState.Free;
        body.isKinematic = false;
        SetCollidersEnabled(true);
        SetCollidersAsTriggers(false);
        SetLayerRecursive(gameObject, lootLayer);

        // Inherit the carrier's motion, so dropping something while swinging
        // throws it rather than parking it in mid-air.
        body.linearVelocity = velocity;
    }

    // --------------------------------------------------------------------
    // CLIP TO ROPE
    //
    // Cargo on the rope is kinematic and driven to follow PointAtDepth. It
    // is not simulated - it is carried by the same maths that carries the
    // players, which means it bends the rope and counts against the load
    // exactly like a person does.
    // --------------------------------------------------------------------

    /// <summary>
    /// Rough radius of this object, used to space cargo out along the rope so
    /// a big statue gets more room than a cash bundle.
    /// </summary>
    public float ClearanceRadius
    {
        get
        {
            float max = 0.3f;
            foreach (var c in colliders)
                if (c != null) max = Mathf.Max(max, c.bounds.extents.magnitude);
            return max;
        }
    }

    public void ClipToRope(MainRope mainRope, float depth)
    {
        rope = mainRope;

        // Ask the rope for a gap rather than taking the depth we were given.
        // Without this every item clipped at the same height and the whole
        // run's loot ended up inside itself.
        ropeDepth = rope.FindFreeDepth(depth, ClearanceRadius * 2f + 0.2f);
        rope.RegisterCargo(this);

        State = CarryState.OnRope;

        transform.SetParent(null, true);
        body.isKinematic = true;
        SetCollidersEnabled(true);
        SetRenderersEnabled(true);

        // THIS FIXES BEING SHOVED AND SHAKEN AFTER LOADING CARGO.
        //
        // Cargo on the rope sits exactly where the rope is - which is exactly
        // where the players hanging on it are. It is kinematic and driven by
        // MovePosition, and a kinematic body wins every contact it makes, so
        // it was throwing people around the shaft. Worse, the rope moves the
        // cargo, the cargo shoves the player, the player pulls the rope: a
        // feedback loop, which is what the vibration was.
        //
        // Two guards, because this must never happen again:
        SetLayerRecursive(gameObject, ropeLayer);   // collision matrix: Player never hits Rope
        SetCollidersAsTriggers(true);               // and a trigger cannot push anything at all

        // The cost of the trigger is that cargo no longer clatters off walls
        // while it hangs. Worth it - a piece of loot clipping through a wall
        // is a small visual oddity, a player being flung across the shaft by
        // their own cargo is unplayable.
    }

    public void UnclipFromRope()
    {
        if (rope != null) rope.UnregisterCargo(this);

        State = CarryState.Free;
        body.isKinematic = false;
        SetCollidersEnabled(true);
        SetCollidersAsTriggers(false);

        SetLayerRecursive(gameObject, lootLayer);
    }

    void OnDestroy()
    {
        if (rope != null) rope.UnregisterCargo(this);
    }

    void SetCollidersAsTriggers(bool on)
    {
        foreach (var c in colliders)
            if (c != null) c.isTrigger = on;
    }

    void SetRenderersEnabled(bool on)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = on;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    void FixedUpdate()
    {
        if (State != CarryState.OnRope || rope == null) return;

        // MovePosition, not transform.position. On a kinematic body
        // MovePosition sweeps to the new spot and generates proper contacts;
        // writing the transform teleports it and things fall through it.
        body.MovePosition(rope.PointAtDepth(ropeDepth));

        // Cargo weighs on the winch and drags the rope sideways, same as a
        // player. This is why loading three statues is a decision.
        rope.AddLoad(Mass);
    }

    void SetCollidersEnabled(bool on)
    {
        foreach (var c in colliders)
            if (c != null) c.enabled = on;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Weight switch
        {
            WeightClass.Small => Color.green,
            WeightClass.Heavy => Color.yellow,
            _ => Color.red
        };
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.1f);
    }
}