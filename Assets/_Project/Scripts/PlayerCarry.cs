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
    PlayerHealth health;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        backpack = GetComponent<PlayerBackpack>();
        health = GetComponent<PlayerHealth>();
    }

    void Start()
    {
        cam = PlayerRegistry.EyeOf(this);   // my eye, for the pickup ray
    }

    void Update()
    {
        lookingAt = held == null ? FindTarget() : null;
    }

    void LateUpdate()
    {
        if (held == null) return;

        // A REMOTE BODY HAS NO CAMERA, AND STILL HAS HANDS.
        //
        // cam is my eye, and PlayerRegistry.EyeOf returns null for anybody
        // else - only the local player has a camera. So this used to return
        // early for every teammate, and a crate they were carrying just hung
        // in the air where they picked it up while they walked off with
        // nothing.
        //
        // In front of the chest, from the body's own facing. Not as precise as
        // a camera-relative hold, and it does not need to be: what matters
        // from across a dark floor is that the crate is with them.
        if (cam == null)
        {
            Vector3 anchor = transform.position + Vector3.up * 1.15f
                           + transform.forward * 0.55f;

            held.transform.position = Vector3.Lerp(
                held.transform.position, anchor, holdSnapSpeed * Time.deltaTime);
            held.transform.rotation = Quaternion.Slerp(
                held.transform.rotation, transform.rotation, holdSnapSpeed * Time.deltaTime);
            return;
        }

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

        // Only my hands. PlayerInput broadcasts to every body that has one.
        if (!PlayerRegistry.IsLocalFor(this)) return;

        // PHASE2_SPEC: while downed you "cannot move, look freely, or
        // interact". Dropping is allowed - if you go down holding a crate it
        // has to leave your hands, or the load gauge charges the crew for a
        // box nobody can reach.
        if (health != null && health.IsDowned)
        {
            if (held != null) DropHeld();
            return;
        }

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
        // Small items auto-stow if pack has room. A person never qualifies -
        // 70kg is Massive and CanStow is Small-only - but the guard is spelled
        // out anyway, because "cannot stow a person" is a RULE and leaving it
        // implicit in a mass threshold means it silently stops being true the
        // day somebody retunes the weight classes.
        if (!item.IsPerson && item.CanStow && backpack != null &&
            backpack.TryStow(item)) return;

        item.PickUp();
        held = item;
        Announce(item, true);
    }

    // ==================================================================
    // PHASE 4 STEP 6 - TELL EVERYONE, BUT DO IT FIRST.
    //
    // My hands close IMMEDIATELY and the message goes out afterwards. Waiting
    // for a round trip before your own grab registers is the one lag a player
    // always notices, and there is nothing to be gained by it: if the host
    // refuses - somebody else got there in the same frame - the worst case is
    // that a crate briefly appeared in my hands and then did not.
    //
    // Everyone else finds out a moment later, which is fine, because for
    // everyone else this is somebody ELSE's hands.
    // ==================================================================
    void Announce(Carryable item, bool pickedUp)
    {
        var net = LootNet.Instance;
        if (net == null || !net.IsSpawned) return;      // offline: nobody to tell

        var loot = item != null ? item.GetComponent<LootItem>() : null;
        if (loot == null || loot.RosterIndex < 0) return;

        ulong me = Unity.Netcode.NetworkManager.Singleton.LocalClientId;

        if (pickedUp) net.RequestPickupServerRpc(loot.RosterIndex, me);
        else net.RequestDropServerRpc(loot.RosterIndex,
                                      item.transform.position,
                                      item.transform.rotation, me);
    }

    /// <summary>
    /// Somebody else's pickup, arriving. Puts the item in THIS body's hands
    /// without sending anything back - otherwise the confirmation would be
    /// re-announced and bounce around the session forever.
    /// </summary>
    public void ReceiveOverNetwork(Carryable item)
    {
        if (item == null) return;
        if (held != null && held != item) DropHeld();

        item.PickUp();
        held = item;
    }

    /// <summary>
    /// Let go without inheriting the carrier's motion. Used when a downed
    /// crewmate is revived in your arms: they should stand up where they are,
    /// not be thrown at whatever speed you happened to be walking.
    /// </summary>
    public void ForceDrop()
    {
        if (held == null) return;
        held.Drop(Vector3.zero);
        held = null;
    }

    void DropHeld()
    {
        if (held == null) return;

        // Announced BEFORE the drop, while the item is still in my hands and
        // therefore still where I can see it. Afterwards it is a falling
        // object and the position I would send is already a frame out of date.
        Announce(held, false);

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

        // MY HUD, not everyone's. Without this every body in the
        // scene draws its own copy on top of the same screen.
        if (!PlayerRegistry.IsLocalFor(this)) return;

        string prompt = null;
        Color colour = Color.white;

        if (held != null)
        {
            // A crewmate is not "Bottled_Water_Bulk (70kg, Massive)". The
            // numbers are identical and the sentence must not be: the whole
            // point of Step 6 is that the load gauge cannot tell the
            // difference and the crew can.
            prompt = held.IsPerson
                ? $"carrying a crewmate  ({held.Mass:0}kg)   -   TOO HEAVY TO JUMP" +
                  "\nE  put them down"
                : $"carrying {held.name}  ({held.Mass:0}kg, {held.Weight})" +
                  (held.AllowsJumping ? "" : "   -   TOO HEAVY TO JUMP") +
                  "\nE  drop it - counts toward the elevator's load once it is inside the car";

            colour = held.IsPerson
                ? new Color(1f, 0.45f, 0.4f)
                : held.AllowsJumping ? Color.white : new Color(1f, 0.6f, 0.25f);
        }
        else if (lookingAt != null)
        {
            prompt = lookingAt.IsPerson
                ? $"E  pick them up   ({lookingAt.Mass:0}kg - both hands)"
                : $"E  pick up {lookingAt.name}   ({lookingAt.Mass:0}kg, {lookingAt.Weight})";

            if (lookingAt.IsPerson) colour = new Color(1f, 0.45f, 0.4f);
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
