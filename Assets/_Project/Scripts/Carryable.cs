// Carryable.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Carryable.cs
// Goes on: every loot object.
//
// ========================================================================
// WEIGHT LIMITS WHAT YOU CAN DO, NOT JUST HOW FAST YOU MOVE.
//
//   SMALL    under 8kg    one hand, fits in the backpack
//   HEAVY    8 - 60kg     two hands, no jumping, 70% speed
//   MASSIVE  over 60kg    45% speed, no jumping
//
// The class is derived from the Rigidbody's mass rather than set by hand,
// so an object's behaviour always matches its stated weight. Change the
// mass and everything follows.
//
// The point of Heavy is not the speed penalty. It is that both your hands
// are gone - you cannot open a door, you cannot help anyone, and you have
// committed to walking this thing all the way back to the elevator.
// ========================================================================

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Carryable : MonoBehaviour
{
    public enum WeightClass { Small, Heavy, Massive }

    public enum CarryState
    {
        Free,     // lying in the world - counts toward the elevator's load
                  // the moment it is physically inside the car, wherever
                  // that is. See ElevatorDeck.cs - reverted from a Stow-like
                  // deliberate-placement design after playtest: forcing a
                  // marked square made the crew argue about positioning
                  // instead of just piling loot wherever there was room.
        Held,     // in a player's hands
        Stowed    // in a player's backpack - small items only
    }

    [Header("Value")]
    [Tooltip("What the black market pays for this. Later the run's quota is " +
             "measured against the sum of these.")]
    public int value = 100;

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

    // Anything needing two hands stops you jumping. Only one-handed loot lets
    // you move freely - which is the entire reason the backpack exists.
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

    int lootLayer = -1;

    // ------------------------------------------------------------------
    // SOME CARGO IS A PERSON (PHASE2_SPEC Step 6)
    //
    // A downed crewmate is 70kg of Massive cargo - two hands, no jumping,
    // 0.45 speed, counted by the load gauge - and every one of those falls
    // out of the mass alone with no special case. That is the whole reason
    // the step is cheap.
    //
    // What does NOT fall out is the housekeeping this class does to LOOT,
    // which would quietly wreck a player:
    //
    //   * SetLayerRecursive(lootLayer) on Drop would move a revived
    //     teammate onto the Loot layer permanently - so the ground check
    //     stops seeing them, and a third player could pick them up while
    //     they are walking around perfectly healthy.
    //   * SetRenderersEnabled fights LocalFirstPersonBodyCull, which owns
    //     what the local player can see of their own body.
    //   * ApplyPushResistance sets horizontal damping equal to MASS. At 70
    //     that is a factor of 0.42 bled off every physics step - a healthy
    //     player wearing it could barely walk.
    //
    // So the class asks whether it is attached to a person and skips exactly
    // those three. Detected here rather than set from outside, because a flag
    // somebody has to remember to set before Awake is a flag that will be
    // wrong once.
    // ------------------------------------------------------------------

    public bool IsPerson { get; private set; }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();

        lootLayer = LayerMask.NameToLayer("Loot");

        IsPerson = GetComponent<PlayerMotor>() != null;

        if (!IsPerson) ApplyPushResistance();
    }

    // --------------------------------------------------------------------
    // WHY DRAG, AND NOT MORE MASS
    //
    // Mass already has a job here: it drives Weight, SpeedMultiplier,
    // backpack eligibility, and the whole $/kg density table in
    // ECONOMY_AND_CAMPAIGN.md. Inflating it just to make a Heavy item feel
    // properly heavy to shove would silently reclassify it - a filing
    // cabinet bumped up to "feel Massive" starts BEING Massive, with a
    // different speed penalty and a different price tier nobody asked for.
    //
    // The actual bug was not the mass ratio - a 34kg cabinet against a 70kg
    // player is realistically push-able even at correct masses, the same
    // way a person can shove real furniture. It was that NOTHING resisted
    // a SUSTAINED push: zero drag meant a collision's velocity persisted
    // and kept accumulating every frame you stayed in contact, so walking
    // into anything, however light, eventually walked it across the room.
    //
    // Damping fixes exactly that, without touching mass or the WeightClass
    // boundaries it drives: it bleeds off velocity picked up from a
    // collision every physics step, so contact can still nudge something
    // but can no longer walk it anywhere.
    //
    // SET EQUAL TO MASS, on request, after "very hard to push anything" -
    // heavier items resist proportionally more with no separate tuning
    // table to keep in sync as loot masses change.
    //
    // HORIZONTAL ONLY - THIS IS THE PART THE FIRST VERSION GOT WRONG.
    //
    // Rigidbody.linearDamping resists ALL motion equally, on every axis -
    // including the vertical one gravity uses to make something fall. At
    // damping = mass, a 34kg cabinet resisted falling exactly as hard as it
    // resisted being shoved sideways, which is not "heavy", it is "barely
    // affected by gravity": freshly spawned loot never finished settling
    // onto the floor, and anything just dropped hung in the air and sank in
    // slow motion. Both are the "floating" and "going down slowly" reports.
    //
    // body.linearDamping stays at Unity's default of 0 - gravity is left
    // completely alone. horizontalDamping is applied by hand, in
    // FixedUpdate below, to the x/z components only, so a push is resisted
    // exactly as hard as before while a fall is not slowed at all.
    // angularDamping is untouched by this problem - gravity has no angular
    // component - so tipping/tumbling resistance stays simple.
    // --------------------------------------------------------------------

    float horizontalDamping;

    void ApplyPushResistance()
    {
        horizontalDamping = body.mass;
        body.angularDamping = body.mass;

        // Caps how fast PhysX is allowed to shove two overlapping bodies
        // apart in one step. A held item's colliders are OFF (see PickUp
        // below) so it can pass through walls and the player while you
        // carry it - if you then drop it while it happens to be clipped
        // into a doorframe, colliders switch back on into that overlap and
        // an uncapped solver resolves it with a single explosive impulse,
        // which is the "item goes flying" report. Capped here, the same
        // overlap still gets pushed out, just over a few gentle frames
        // instead of one violent one.
        body.maxDepenetrationVelocity = 3f;
    }

    /// <summary>
    /// Only runs while Free - Held and Stowed are kinematic and do not
    /// simulate at all, so there is nothing here for them to fight.
    /// Same exponential form Unity's own linearDamping uses internally,
    /// applied to x/z only so y (the fall) is never touched.
    /// </summary>
    void FixedUpdate()
    {
        if (IsPerson) return;      // a person is not shoved-around furniture
        if (State != CarryState.Free) return;

        Vector3 v = body.linearVelocity;
        float k = 1f / (1f + horizontalDamping * Time.fixedDeltaTime);
        v.x *= k;
        v.z *= k;
        body.linearVelocity = v;
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
        transform.SetParent(null, true);   // may be coming out of the backpack

        State = CarryState.Held;
        body.isKinematic = true;
        SetCollidersEnabled(false);
        SetCollidersAsTriggers(false);
        if (!IsPerson) SetRenderersEnabled(true);
    }

    /// <summary>
    /// Onto a player's back. Parented, unlike a held item - a frame of lag is
    /// invisible on something behind you, and parenting means it follows for
    /// free.
    /// </summary>
    public void Stow(Transform backAnchor)
    {
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
        if (!IsPerson)
        {
            SetRenderersEnabled(true);
            SetLayerRecursive(gameObject, lootLayer);
        }
    }

    public void Drop(Vector3 velocity)
    {
        transform.SetParent(null, true);
        if (!IsPerson) SetRenderersEnabled(true);

        State = CarryState.Free;
        body.isKinematic = false;
        SetCollidersEnabled(true);
        SetCollidersAsTriggers(false);
        if (!IsPerson) SetLayerRecursive(gameObject, lootLayer);

        // Inherit the carrier's motion, so dropping something while running
        // throws it rather than parking it in mid-air.
        body.linearVelocity = velocity;
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