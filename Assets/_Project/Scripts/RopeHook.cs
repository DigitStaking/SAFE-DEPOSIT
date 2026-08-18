// RopeHook.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/RopeHook.cs
// Goes on: the Player root.
//
// ========================================================================
// WHAT Q DOES
//
// You must be CLOSE to the rope - doorway distance, not across the
// building. Press Q and a hook bolts the rope to a fixed point, and it
// STAYS THERE. It does not follow you around afterwards.
//
// The rope does not gently bend. It KINKS. Above the hook it runs taut and
// diagonal from the winch down to the doorway; below the hook it hangs
// straight down FROM the doorway. Like a rope through a carabiner bolted to
// the wall.
//
// TWO THINGS THAT WERE WRONG BEFORE
//
// 1. The hold point was recomputed every physics step from the player's
//    current position, so the rope trailed you around the room like a pet.
//    It is now FROZEN at the instant you press Q.
//
// 2. Nothing checked for walls, so the rope cut straight through geometry.
//    The hook point is now raycast from the rope toward you and stops at
//    the first wall - which naturally parks it in the doorway opening when
//    you are standing behind one.
//
// Only ONE person can hold the rope at a time. If someone else has it, Q
// does nothing until they release. That wait is the mechanic.
// ========================================================================

using UnityEngine;
using UnityEngine.InputSystem;

// Before PlayerTether (100), so the rope's shape is settled before anyone
// asks where their clip point is.
[DefaultExecutionOrder(50)]
public class RopeHook : MonoBehaviour
{
    [Header("Rope")]
    [Tooltip("Leave empty - found automatically.")]
    public MainRope rope;

    [Header("Range")]
    [Tooltip("How close you must be to the rope to hook it, in metres.\n\n" +
             "Deliberately short - doorway distance. You have to get yourself " +
             "to the door first, by leaping or swinging, and only THEN can you " +
             "pin the rope. A long range would let you drag it around from " +
             "anywhere and delete the whole problem.")]
    public float hookRange = 6f;

    [Tooltip("Hook releases automatically past this distance from the pinned " +
             "point, so you cannot pin the rope and wander off with it.")]
    public float breakRange = 9f;

    [Header("Placement")]
    [Tooltip("Which layers block the rope. SET THIS TO ENVIRONMENT.\n\n" +
             "The hook point is raycast from the rope toward you against these " +
             "layers, so the rope stops at the doorway instead of cutting " +
             "through the wall behind you.")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("How far back from a wall the hook sits, so the rope never ends " +
             "up buried in geometry.")]
    public float wallClearance = 0.5f;

    [Tooltip("Furthest the rope can be pulled sideways from where it naturally " +
             "hangs, in metres.\n\n" +
             "THIS IS WHAT PARKS THE ROPE IN THE DOORWAY. The wall raycast " +
             "alone is not enough: stand directly in line with a doorway and " +
             "the ray passes straight through the opening, so the rope follows " +
             "you all the way into the room.\n\n" +
             "The shaft is 8m across, so its wall is 4m from the centre. Set " +
             "this to about 4 and the hook sits at the door frame with the " +
             "rope on the SHAFT side of it, hanging down outside the room - " +
             "which is how a rope run over a door frame actually behaves. Any " +
             "higher and the rope comes into the room with you.")]
    public float maxPinDistance = 4f;

    [Tooltip("Height above the player's feet the hook is placed. Chest height " +
             "reads far better than ankle height.")]
    public float hookHeight = 1.1f;

    [Header("Reel")]
    [Tooltip("Seconds for the rope to come over. NOT instant - the delay makes " +
             "hooking a decision rather than a reflex, and gives everyone else " +
             "time to notice their clip point moving.")]
    public float reelTime = 1.2f;

    [Header("Marker")]
    [Tooltip("Size of the block representing the hook bolted to the wall. " +
             "Created automatically at runtime.")]
    public float markerSize = 0.35f;

    public bool IsHooked => hooked;

    bool hooked;
    float hookedDepth;
    float reelProgress;

    // FROZEN at the moment of hooking. Never recomputed while hooked - that
    // is what stops the rope following the player around the room.
    Vector3 pinnedPoint;

    Rigidbody rb;
    Transform cam;
    PlayerArms arms;
    Transform marker;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        arms = GetComponent<PlayerArms>();
    }

    void Start()
    {
        if (rope == null) rope = FindFirstObjectByType<MainRope>();
        if (Camera.main != null) cam = Camera.main.transform;
        CreateMarker();
    }

    // A visible block standing in for the hook device bolted to the wall.
    // Placeholder art, but having SOMETHING there matters - players need to
    // see what is holding the rope, or the kink just looks like a bug.
    void CreateMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "HookMarker";
        Destroy(go.GetComponent<Collider>());   // must never push anyone
        go.transform.localScale = Vector3.one * markerSize;
        go.SetActive(false);
        marker = go.transform;
    }

    void OnHookRope(InputValue value)
    {
        if (!value.isPressed || rope == null) return;

        if (hooked) { Release(); return; }

        // Somebody else owns the rope. They have to let go first.
        if (!rope.HookAvailableTo(this)) return;

        // Find the nearest point on the rope by searching, not by arithmetic.
        // If the rope is already kinked by someone else, its position at your
        // height may be nowhere near you.
        float depth = rope.NearestDepth(rb.position);
        Vector3 ropePoint = rope.PointAtDepth(depth);

        if (Vector3.Distance(rb.position, ropePoint) > hookRange) return;

        hooked = true;
        hookedDepth = depth;
        reelProgress = 0f;
        pinnedPoint = ComputePinPoint(depth);
    }

    /// <summary>
    /// Where the rope will be pinned. Worked out ONCE, at the moment of
    /// hooking, and never touched again.
    ///
    /// The raycast is what keeps the rope out of walls: it fires from where
    /// the rope hangs toward where you are standing, and if a wall is in the
    /// way the hook stops just short of it. Stand in a doorway and the rope
    /// arrives in the doorway. Stand deep inside a room and the rope stops at
    /// the door frame rather than passing through it.
    /// </summary>
    Vector3 ComputePinPoint(float depth)
    {
        Vector3 origin = rope.StraightPointAtDepth(depth);
        Vector3 desired = rb.position + Vector3.up * hookHeight;
        desired.y = origin.y;                    // pin at the rope's own height

        Vector3 offset = desired - origin;
        float distance = offset.magnitude;
        if (distance < 0.01f) return desired;

        Vector3 dir = offset / distance;

        // Limit 1: never further from the shaft than the doorway. Without
        // this, standing in line with an open door lets the rope follow you
        // right into the room, because there is no wall for the ray to hit.
        distance = Mathf.Min(distance, maxPinDistance);

        // Limit 2: and never through a wall either, for when you are NOT
        // lined up with the opening.
        if (Physics.Raycast(origin, dir, out RaycastHit hit, distance,
                            obstacleMask, QueryTriggerInteraction.Ignore))
        {
            distance = Mathf.Max(0f, hit.distance - wallClearance);
        }

        return origin + dir * distance;
    }

    void Release()
    {
        hooked = false;
        reelProgress = 0f;
        if (rope != null) rope.ClearHook(this);
        if (marker != null) marker.gameObject.SetActive(false);
    }

    void FixedUpdate()
    {
        if (rope == null || !hooked) return;

        // Wander too far from the PINNED POINT and the hook lets go. Measured
        // against the pin, not against the rope, because the rope now moves
        // with the pin.
        if (Vector3.Distance(rb.position, pinnedPoint) > breakRange)
        {
            Release();
            return;
        }

        // Reel over about a second rather than snapping. Instant reads as a
        // glitch and removes the decision of WHEN to do it.
        reelProgress = Mathf.Clamp01(reelProgress + Time.fixedDeltaTime / Mathf.Max(0.01f, reelTime));

        // Smoothstep the blend: slow start, slow finish. Reads as a winch
        // spinning up and easing off rather than a linear machine.
        if (!rope.SetHook(this, pinnedPoint, hookedDepth, Smooth(reelProgress)))
        {
            Release();   // someone beat us to it
            return;
        }

        if (arms != null && reelProgress < 1f)
            arms.SetPose(PlayerArms.ArmPose.Climb);
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    void LateUpdate()
    {
        if (marker == null) return;

        if (!hooked || rope == null) { marker.gameObject.SetActive(false); return; }

        marker.gameObject.SetActive(true);
        marker.position = rope.PointAtDepth(hookedDepth);
    }

    void OnDestroy()
    {
        if (rope != null) rope.ClearHook(this);
    }

    void OnGUI()
    {
        // Don't draw gameplay chrome over the results/shop screen.
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        if (rope == null) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 14, alignment = TextAnchor.MiddleCenter };

        float w = 700f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 132f;

        if (hooked)
        {
            style.normal.textColor = new Color(0.4f, 0.85f, 1f);
            GUI.Label(new Rect(x, y, w, 22),
                reelProgress < 1f
                    ? "pulling the rope over..."
                    : "ROPE PINNED HERE  -  E to load cargo,  Q to let it go",
                style);
            return;
        }

        float distance = rope.DistanceToRope(rb.position);

        if (!rope.HookAvailableTo(this))
        {
            style.normal.textColor = new Color(1f, 0.6f, 0.3f);
            GUI.Label(new Rect(x, y, w, 22),
                "someone else has the rope  -  wait for them to release it", style);
        }
        else if (distance <= hookRange)
        {
            style.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(x, y, w, 22), "Q  pin the rope here", style);
        }
        else
        {
            style.normal.textColor = new Color(1f, 1f, 1f, 0.35f);
            GUI.Label(new Rect(x, y, w, 22),
                $"too far from the rope to hook  ({distance:0.0}m, need {hookRange:0}m)", style);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hookRange);

        if (hooked)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pinnedPoint, 0.4f);
        }
    }
}
