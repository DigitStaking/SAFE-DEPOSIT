// PlayerCarry.cs  -  SAFE DEPOSIT
// E: pickup / clip to rope / drop. Held items follow camera in LateUpdate.

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
    public float clipRange = 2.5f;

    public bool IsCarrying => held != null;

    public float CarriedMass =>
        (held != null ? held.Mass : 0f) +
        (backpack != null ? backpack.TotalMass : 0f);

    public bool CanClimb => held == null || held.AllowsClimbing;
    public bool CanKick  => held == null || held.AllowsKicking;
    public bool CanJump  => held == null || held.AllowsJumping;
    public float SpeedMultiplier => held != null ? held.SpeedMultiplier : 1f;

    Carryable held;
    Carryable lookingAt;
    Rigidbody rb;
    Transform cam;
    MainRope rope;
    PlayerArms arms;
    PlayerBackpack backpack;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        arms = GetComponent<PlayerArms>();
        backpack = GetComponent<PlayerBackpack>();
    }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;
        rope = FindFirstObjectByType<MainRope>();
    }

    void Update()
    {
        lookingAt = held == null ? FindTarget() : null;
        if (arms != null && held != null)
            arms.SetCarryFor(held);
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
                    if (arms != null) arms.SetCarryFor(item);
                }
            }
            return;
        }

        if (NearRope(out float depth)) ClipToRope(depth);
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
        if (item.State == Carryable.CarryState.OnRope) item.UnclipFromRope();

        // Small items auto-stow if pack has room.
        if (item.CanStow && backpack != null && backpack.TryStow(item)) return;

        item.PickUp();
        held = item;
        if (arms != null) arms.SetCarryFor(item);
    }

    void DropHeld()
    {
        if (held == null) return;
        held.Drop(rb.linearVelocity);
        held = null;
        if (arms != null) arms.SetPose(PlayerArms.ArmPose.Idle);
    }

    public void ReceiveFromPack(Carryable item)
    {
        if (item == null || held != null) return;
        held = item;
        if (arms != null) arms.SetCarryFor(item);
    }

    void ClipToRope(float depth)
    {
        if (held == null || rope == null) return;
        held.ClipToRope(rope, depth);
        held = null;
        if (arms != null) arms.SetPose(PlayerArms.ArmPose.Idle);
    }

    bool NearRope(out float depth)
    {
        depth = 0f;
        if (rope == null) return false;
        Vector3 chest = rb.position + Vector3.up * 0.9f;
        depth = rope.NearestDepth(chest);
        return Vector3.Distance(chest, rope.PointAtDepth(depth)) <= clipRange;
    }

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        string prompt = null;
        Color colour = Color.white;

        if (held != null)
        {
            if (NearRope(out _))
            {
                prompt = $"E  clip {held.name} to the rope   ({held.Mass:0}kg, {held.Weight})";
                colour = new Color(0.4f, 0.85f, 1f);
            }
            else
            {
                prompt = $"carrying {held.name}  ({held.Mass:0}kg, {held.Weight})" +
                         (held.AllowsClimbing ? "" : "   -   TOO HEAVY TO CLIMB") +
                         "\nE  drop it";
                colour = held.AllowsClimbing ? Color.white : new Color(1f, 0.6f, 0.25f);
            }
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
