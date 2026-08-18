// PlayerCarry.cs  -  SAFE DEPOSIT
// E: pickup / drop / place on deck. Held items follow camera in LateUpdate.

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCarry : MonoBehaviour
{
    [Header("Hold position")]
    public Vector3 holdOffset = new Vector3(0.35f, -0.35f, 1.15f);
    public float holdSnapSpeed = 18f;

    [Header("Reach")]
    public LayerMask pickupMask = ~0;
    public float pickupRange = 2.5f;
    public float pickupRadius = 0.4f;

    [Header("Deck (Step 8)")]
    [Tooltip("How far from the elevator's DeckAnchor, in X/Z, counts as " +
             "'on the deck' for placing cargo there instead of just dropping it.")]
    public float deckPlaceRange = 1.5f;

    public bool IsCarrying => held != null;

    public float CarriedMass =>
        (held != null ? held.Mass : 0f) +
        (backpack != null ? backpack.TotalMass : 0f);

    public bool CanJump  => held == null || held.AllowsJumping;
    public float SpeedMultiplier => held != null ? held.SpeedMultiplier : 1f;

    Carryable held;
    Carryable lookingAt;
    Rigidbody rb;
    Transform cam;
    PlayerBackpack backpack;
    Transform deckAnchor;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        backpack = GetComponent<PlayerBackpack>();
    }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;

        // Single-player lookup, same caveat as everywhere else this pattern
        // appears: Phase C replaces this with a player registry.
        var deck = Object.FindFirstObjectByType<ElevatorDeck>();
        if (deck != null) deckAnchor = deck.DeckAnchor;
    }

    void Update()
    {
        lookingAt = held == null ? FindTarget() : null;
    }

    void LateUpdate()
    {
        if (held == null || cam == null) return;

        // Hold offset by weight: small crate close, cabinet two-hand, vending hug.
        Vector3 offset = holdOffset;
        if (held.Weight == Carryable.WeightClass.Heavy)
            offset = new Vector3(0f, -0.45f, 1.25f);
        else if (held.Weight == Carryable.WeightClass.Massive)
            offset = new Vector3(0f, -0.55f, 1.45f);
        else
            offset = new Vector3(0.25f, -0.30f, 1.05f);

        Vector3 target = cam.position + cam.rotation * offset;
        held.transform.position = Vector3.Lerp(
            held.transform.position, target, holdSnapSpeed * Time.deltaTime);
        held.transform.rotation = Quaternion.Slerp(
            held.transform.rotation, cam.rotation, holdSnapSpeed * Time.deltaTime);
    }

    void OnInteract(InputValue value)
    {
        if (!value.isPressed) return;

        if (held == null)
        {
            if (lookingAt != null)
            {
                PickUp(lookingAt);
            }
            else if (backpack != null && backpack.Count > 0)
            {
                var item = backpack.TakeLast();
                if (item != null)
                {
                    item.PickUp();
                    held = item;
                }
            }
            return;
        }

        if (NearDeck(out Vector3 placePos)) PlaceOnDeck(placePos);
        else DropHeld();
    }

    Carryable FindTarget()
    {
        if (cam == null) return null;

        if (!Physics.SphereCast(cam.position, pickupRadius, cam.forward,
                                out RaycastHit hit, pickupRange,
                                pickupMask, QueryTriggerInteraction.Ignore))
            return null;

        var carryable = hit.collider.GetComponentInParent<Carryable>();
        if (carryable == null) return null;

        if (carryable.State == Carryable.CarryState.Held ||
            carryable.State == Carryable.CarryState.Stowed)
            return null;

        return carryable;
    }

    void PickUp(Carryable item)
    {
        // Taking cargo back off the deck - a crew that overestimated what
        // they could afford needs to be able to change their mind.
        if (item.State == Carryable.CarryState.OnDeck) item.RemoveFromDeck();

        // Small items auto-stow if pack has room.
        if (item.CanStow && backpack != null && backpack.TryStow(item)) return;

        item.PickUp();
        held = item;
    }

    void DropHeld()
    {
        if (held == null) return;
        held.Drop(rb.linearVelocity);
        held = null;
    }

    /// <summary>
    /// Within deckPlaceRange of DeckAnchor on X/Z. The car never rotates, so
    /// a plain axis-aligned box check is exact - no need to transform into
    /// the deck's local space.
    /// </summary>
    bool NearDeck(out Vector3 placePos)
    {
        placePos = default;
        if (deckAnchor == null) return false;

        Vector3 feet = rb.position;
        if (Mathf.Abs(feet.x - deckAnchor.position.x) > deckPlaceRange) return false;
        if (Mathf.Abs(feet.z - deckAnchor.position.z) > deckPlaceRange) return false;

        // At your feet, at deck height - not snapped to the anchor's own
        // position, so cargo spreads out across the deck instead of
        // stacking on one point.
        placePos = new Vector3(feet.x, deckAnchor.position.y, feet.z);
        return true;
    }

    void PlaceOnDeck(Vector3 pos)
    {
        if (held == null || deckAnchor == null) return;
        held.PlaceOnDeck(deckAnchor, pos);
        held = null;
    }

    public void ReceiveFromPack(Carryable item)
    {
        if (item == null || held != null) return;
        held = item;
    }

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        string prompt = null;
        Color colour = Color.white;

        if (held != null)
        {
            bool onDeck = NearDeck(out _);
            prompt = $"carrying {held.name}  ({held.Mass:0}kg, {held.Weight})" +
                     (held.AllowsJumping ? "" : "   -   TOO HEAVY TO JUMP") +
                     (onDeck ? "\nE  place on deck - counts toward load" : "\nE  drop it");
            colour = onDeck ? new Color(0.5f, 0.95f, 0.6f)
                   : held.AllowsJumping ? Color.white : new Color(1f, 0.6f, 0.25f);
        }
        else if (lookingAt != null)
        {
            string verb = lookingAt.State == Carryable.CarryState.OnDeck ? "take off the deck" : "pick up";
            prompt = $"E  {verb} {lookingAt.name}   ({lookingAt.Mass:0}kg, {lookingAt.Weight})";
        }

        if (prompt == null) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = colour;

        float w = 700f;
        GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f + 60f, w, 46),
                  prompt, style);
    }
}
