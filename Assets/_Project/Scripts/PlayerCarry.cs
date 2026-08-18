// PlayerCarry.cs  -  SAFE DEPOSIT
// E: pickup / drop. Held items follow camera in LateUpdate.
//
// Step 8 briefly required carrying it to a marked deck square and pressing E
// there specifically. Reverted after playtest: it made the crew argue about
// exact positioning instead of just piling loot wherever there was room.
// ElevatorDeck.cs now counts anything physically inside the car, so a plain
// drop is enough - see Carryable.CarryState.Free for where that is decided.

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

    public bool IsCarrying => held != null;

    /// <summary>
    /// What is in the player's hands right now, or null. Read-only on
    /// purpose - PriceScanner (Step 9) needs to inspect it, but only this
    /// script may change what is being carried.
    /// </summary>
    public Carryable Held => held;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        backpack = GetComponent<PlayerBackpack>();
    }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;
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

        DropHeld();
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
            prompt = $"carrying {held.name}  ({held.Mass:0}kg, {held.Weight})" +
                     (held.AllowsJumping ? "" : "   -   TOO HEAVY TO JUMP") +
                     "\nE  drop it - counts toward the elevator's load once it is inside the car";
            colour = held.AllowsJumping ? Color.white : new Color(1f, 0.6f, 0.25f);
        }
        else if (lookingAt != null)
        {
            prompt = $"E  pick up {lookingAt.name}   ({lookingAt.Mass:0}kg, {lookingAt.Weight})";
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
