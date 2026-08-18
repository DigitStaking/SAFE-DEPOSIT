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
        Free,     // lying in the world
        Held,     // in a player's hands
        Stowed,   // in a player's backpack - small items only
        OnDeck    // placed as cargo - counts toward the elevator's load
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

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();

        lootLayer = LayerMask.NameToLayer("Loot");
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
        SetRenderersEnabled(true);
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
        SetRenderersEnabled(true);
        SetLayerRecursive(gameObject, lootLayer);
    }

    // --------------------------------------------------------------------
    // CARGO DECK  (Step 8)
    //
    // Deliberate, like Stow - not "anything physically inside the car
    // counts". Parented under DeckAnchor, so it rides the car through
    // ordinary transform hierarchy and ElevatorDeck can count the load by
    // enumerating DeckAnchor's own children, no second physics query needed.
    //
    // Unlike Stow, the renderer stays ON. This is cargo on open display in
    // the middle of the room, not something zipped into a bag - the whole
    // point of the deck markings is that everyone can see the pile grow.
    // --------------------------------------------------------------------

    /// <summary>
    /// worldPosition is set BEFORE parenting, so the item lands exactly
    /// where the caller (PlayerCarry) computed - typically the player's own
    /// feet, at deck height - rather than snapping to the anchor's origin.
    /// </summary>
    public void PlaceOnDeck(Transform deckAnchor, Vector3 worldPosition)
    {
        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;
        transform.SetParent(deckAnchor, true);

        State = CarryState.OnDeck;
        body.isKinematic = true;
        SetCollidersEnabled(true);
        SetCollidersAsTriggers(false);
        SetRenderersEnabled(true);
    }

    public void RemoveFromDeck()
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

        State = CarryState.Free;
        body.isKinematic = false;
        SetCollidersEnabled(true);
        SetCollidersAsTriggers(false);
        SetLayerRecursive(gameObject, lootLayer);

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