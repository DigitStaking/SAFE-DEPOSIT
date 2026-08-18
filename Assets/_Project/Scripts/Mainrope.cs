// MainRope.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/MainRope.cs
// Goes on: an empty GameObject named "MainRope" at the scene root.
//
// ========================================================================
// THE ROPE IS NOT A PHYSICS OBJECT.
//
// No rigidbodies, no joints, no rope segments. It is an anchor position, a
// length, and a sideways bend. Three pieces of data.
//
// A jointed rope chain becomes unstable the moment something heavy hangs on
// it, which in this game is always. It also means syncing dozens of bodies
// per player over the network. This model syncs a handful of floats.
//
// THE BEND IS THE POINT.
//
// A rigid rope reads as a painted stripe, and worse, it isolates players
// from each other. So the rope bends toward whatever pulls on it, and
// PointAtDepth returns the BENT position. Every player asks that same
// function where their clip point is - so one player's pull physically
// moves everyone else's anchor. The yank, the whip, the friend who wrecks
// your swing by climbing at the wrong moment: all of it comes from here,
// for the cost of one spring simulated once per frame.
//
// The bend is zero at the anchor and strongest at the free bottom end,
// which is how a rope fixed at one end actually behaves.
// ========================================================================

using UnityEngine;

// Runs before the players so this frame's bend is settled before anyone
// asks where their clip point is.
[DefaultExecutionOrder(-50)]
public class MainRope : MonoBehaviour
{
    [Header("Anchor")]
    [Tooltip("The Winch_Anchor transform at the top of the shaft. If empty, " +
             "this object's own position is used.")]
    public Transform anchor;

    [Header("Length")]
    [Tooltip("How far down the shaft the rope reaches, in metres.\n\n" +
             "THIS IS THE PROGRESSION OF THE WHOLE GAME. Players buy more of it " +
             "between runs while the floors above them are demolished behind " +
             "them. Start it short.")]
    public float ropeLength = 20f;

    [Tooltip("Longest the rope can ever be - the bottom of the building.")]
    public float maxRopeLength = 60f;

    [Header("Load")]
    [Tooltip("Total kilograms the winch can take. Every attached player and " +
             "every clipped item counts.")]
    public float loadLimit = 400f;

    [Tooltip("Seconds the anchor holds while overloaded before it tears out.\n\n" +
             "NOT instant, on purpose. An instant kill with no warning reads " +
             "as a bug. Five seconds of the anchor screaming, with a visible " +
             "countdown, is long enough to hit G and ditch your pack, unclip " +
             "the thing you just loaded, or shout at whoever did it.\n\n" +
             "The load limit is not there to kill you. It is there to make you " +
             "choose what to throw away, in a hurry, out loud.")]
    public float overloadGrace = 5f;

    /// <summary>True once the anchor has torn out. The run is over.</summary>
    public bool Snapped { get; private set; }

    /// <summary>0 to 1 while overloaded. Drive audio and shake off this.</summary>
    public float FailureProgress =>
        overloadGrace <= 0.01f ? 0f : Mathf.Clamp01(overloadTimer / overloadGrace);

    /// <summary>Seconds left before the anchor tears out. Only meaningful while overloaded.</summary>
    public float SecondsToFailure => Mathf.Max(0f, overloadGrace - overloadTimer);

    float overloadTimer;

    [Header("Sway")]
    [Tooltip("How strongly the rope springs back to vertical. Higher is " +
             "stiffer and twitchier; lower is heavier and lazier.")]
    public float swayStiffness = 10f;

    [Tooltip("How fast sway motion dies out. Too low and it wobbles for ages " +
             "after every pull; too high and it feels like a metal bar.")]
    public float swayDamping = 3.5f;

    [Tooltip("Metres of bend per kilogram of pull. The main feel knob - raise " +
             "it until hauling on the line visibly drags it toward you.")]
    public float swayResponse = 0.012f;

    [Tooltip("Hard cap on bend. Must be large enough that a hook can drag the " +
             "rope all the way to a doorway - roughly half the shaft width " +
             "plus a metre.")]
    public float maxSway = 5f;

    [Header("Visual")]
    public LineRenderer line;

    [Tooltip("Points used to draw the rope. Two would be a straight line; we " +
             "need several so the bend is visible.")]
    [Range(2, 64)] public int visualSegments = 40;

    [Tooltip("Rendered thickness. A real 11mm climbing rope is about 0.011 - " +
             "but at this camera distance that is invisible, so we cheat " +
             "upward. Around 0.09 reads as rope without looking like a pipe.")]
    public float visualWidth = 0.09f;

    [Tooltip("How far the rope dips between the winch and a hook, as a " +
             "fraction of the horizontal distance between them.\n\n" +
             "A rope pulled sideways hangs in a curve, and without this the " +
             "diagonal reads as a steel cable. 0 is dead straight; 0.12 is a " +
             "well-tensioned rope; above 0.25 looks slack and useless.")]
    [Range(0f, 0.4f)] public float sagFactor = 0.12f;

    [Tooltip("Seconds for the rope to swing back to centre after a hook is " +
             "released. NEVER set this to 0 - clearing the kink in one frame " +
             "teleports every clip point on the rope several metres sideways, " +
             "and the tether constraint then whips everyone hanging on it " +
             "after the rope.")]
    public float hookReleaseTime = 0.9f;

    [Tooltip("Draws a weighted end on the rope. Ropes have ends; a line that " +
             "just stops in mid-air looks unfinished and, worse, players " +
             "cannot tell where the rope actually runs out.")]
    public bool showEndStopper = true;
    public float endStopperSize = 0.22f;

    [Header("Debug")]
    [Tooltip("Pretends this many extra players are hanging on the rope and " +
             "pulling exactly as you are.\n\n" +
             "Because pulls are SUMMED, players pulling opposite ways cancel " +
             "out and the rope does not move at all - a crew has to pump it in " +
             "rhythm, shouting at each other, to swing it across the shaft. " +
             "Solo you can barely bend it.\n\n" +
             "Set to 3 to feel a coordinated four-person crew before " +
             "multiplayer exists. Back to 0 before shipping.")]
    [Range(0, 5)] public int debugExtraPullers = 0;

    // ---- state ----
    Vector3 swayOffset;      // current bend, horizontal only
    Vector3 swayVelocity;

    Vector3 pullAccumulator; // soft pulls from hanging players, this step
    float loadAccumulator;

    // ---- hook state ----
    // ONE hook at a time, owned by whoever grabbed it first. That exclusivity
    // is deliberate: while somebody has the rope pinned to a doorway, nobody
    // else can take it, so the crew has to wait and talk.
    Transform endStopper;

    bool hookActive;
    bool hookReleasing;
    object hookOwner;
    float hookDepth;
    Vector3 hookPoint;
    float hookBlend;         // 0 = free, 1 = fully redirected

    public Vector3 AnchorPosition => anchor != null ? anchor.position : transform.position;
    public float Length => ropeLength;
    public Vector3 Sway => swayOffset;
    public float CurrentLoad { get; private set; }
    public float LoadFraction => loadLimit > 0.01f ? CurrentLoad / loadLimit : 0f;
    public bool Overloaded => CurrentLoad > loadLimit;

    public bool IsHooked => hookActive;
    public Vector3 HookPoint => hookPoint;
    public float HookDepth => hookDepth;

    /// <summary>
    /// Where the rope actually is, this many metres down from the anchor.
    /// Everything that talks to the rope goes through this one function -
    /// which is precisely why one player's pull, or one player's hook, moves
    /// every other player's clip point.
    /// </summary>
    public Vector3 PointAtDepth(float depth)
    {
        float d = Mathf.Clamp(depth, 0f, ropeLength);

        Vector3 free = StraightPointAtDepth(d) + swayOffset * BendProfile(DepthFraction(d));
        if (!hookActive || hookBlend <= 0.001f) return free;

        return Vector3.Lerp(free, RedirectedPoint(d), hookBlend);
    }

    /// <summary>
    /// The rope with a KINK in it at the hook.
    ///
    /// This is not a gentle bend - it is a redirect, like a rope run through
    /// a carabiner bolted to the doorway. Above the hook the rope runs taut
    /// and diagonal from the winch down to that doorway. Below the hook it
    /// hangs straight down FROM THE DOORWAY, not from the middle of the
    /// shaft.
    ///
    /// The consequence is the whole point: everyone hanging below you is now
    /// dangling off the door you just hooked. Pinning the rope is not a
    /// private action, it relocates the entire crew.
    /// </summary>
    Vector3 RedirectedPoint(float depth)
    {
        if (hookDepth <= 0.01f)
            return hookPoint + Vector3.down * depth;

        if (depth <= hookDepth)
        {
            float t = depth / hookDepth;
            Vector3 p = Vector3.Lerp(AnchorPosition, hookPoint, t);

            // SAG. A rope pulled sideways between two points does not run in
            // a dead straight line - it hangs in a curve, and the further the
            // two ends are apart the deeper it dips. Without this the diagonal
            // reads as a steel cable, which is most of why the rope looked
            // wrong.
            //
            // sin gives zero at both ends and maximum in the middle, which is
            // close enough to a real catenary at this scale and far cheaper.
            float span = Vector3.Distance(
                new Vector3(AnchorPosition.x, 0f, AnchorPosition.z),
                new Vector3(hookPoint.x, 0f, hookPoint.z));

            p.y -= Mathf.Sin(t * Mathf.PI) * span * sagFactor;
            return p;
        }

        return hookPoint + Vector3.down * (depth - hookDepth);
    }

    /// <summary>Where the rope would be with no bend at all.</summary>
    public Vector3 StraightPointAtDepth(float depth)
    {
        return AnchorPosition + Vector3.down * Mathf.Clamp(depth, 0f, ropeLength);
    }

    /// <summary>
    /// The depth whose rope position is closest to a world point.
    ///
    /// This has to be a search, not arithmetic. When the rope is hanging
    /// straight you can work out the depth from the Y coordinate alone - but
    /// the moment somebody hooks it, the rope kinks, and a point 5m to the
    /// side at your height may be nowhere near the rope while a point above
    /// your head is right on it.
    ///
    /// Assuming Y was enough is why players could not clip back on near a
    /// pinned rope, and why cargo clipped to strange depths.
    /// </summary>
    public float NearestDepth(Vector3 worldPoint, int samples = 48)
    {
        float bestDepth = 0f;
        float bestSqr = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float d = ropeLength * i / samples;
            float sqr = (PointAtDepth(d) - worldPoint).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; bestDepth = d; }
        }
        return bestDepth;
    }

    /// <summary>Distance from a world point to the closest part of the rope.</summary>
    public float DistanceToRope(Vector3 worldPoint)
    {
        return Vector3.Distance(worldPoint, PointAtDepth(NearestDepth(worldPoint)));
    }

    float DepthFraction(float depth) =>
        ropeLength > 0.01f ? Mathf.Clamp01(depth / ropeLength) : 0f;

    /// <summary>
    /// How much of the total bend applies at this fraction down the rope.
    /// Squared rather than linear because a rope fixed at the top barely
    /// moves near the anchor and swings freely at its loose end.
    /// </summary>
    static float BendProfile(float t) => t * t;

    // --------------------------------------------------------------------
    // Things players report each physics step. All cleared every step, so
    // a constant load produces a constant bend instead of accumulating
    // forever.
    // --------------------------------------------------------------------

    /// <summary>
    /// A hanging player dragging the line sideways.
    ///
    /// SUMMED, not averaged - and that single choice is the co-op mechanic.
    /// Two players pulling opposite ways cancel to zero and the rope does not
    /// move at all. Four pulling together, in rhythm, pump it like a swing
    /// and can throw it across the shaft. Nobody scripted that; it is just
    /// what happens when several people share one object.
    /// </summary>
    public void AddPull(Vector3 pull, float depth)
    {
        Vector3 contribution = new Vector3(pull.x, 0f, pull.z) * BendProfile(DepthFraction(depth));

        // Debug only: pretend more people are pulling exactly as you are, so
        // you can feel a four-person crew before multiplayer exists.
        contribution *= 1 + debugExtraPullers;

        pullAccumulator += contribution;
    }

    /// <summary>
    /// Claim the rope and pin it to a point. Only one owner at a time - if
    /// someone else already has it, this does nothing and returns false.
    /// Called every physics step by whoever holds it, with blend rising from
    /// 0 to 1 as the winch reels in.
    /// </summary>
    public bool SetHook(object owner, Vector3 worldPoint, float depth, float blend)
    {
        // A rope mid-release is up for grabs - somebody can catch it again
        // before it finishes swinging back.
        if (hookActive && !hookReleasing && !ReferenceEquals(hookOwner, owner))
            return false;

        hookOwner = owner;
        hookActive = true;
        hookReleasing = false;
        hookDepth = Mathf.Clamp(depth, 0f, ropeLength);
        hookPoint = worldPoint;
        hookBlend = Mathf.Clamp01(blend);
        return true;
    }

    /// <summary>True if this owner could take the rope right now.</summary>
    public bool HookAvailableTo(object owner) =>
        !hookActive || hookReleasing || ReferenceEquals(hookOwner, owner);

    /// <summary>
    /// Let the rope go. It does NOT snap back - it swings back over about a
    /// second.
    ///
    /// Clearing the kink in a single frame teleported every clip point on the
    /// rope several metres sideways at once, and the tether constraint then
    /// whipped everyone hanging on it after the rope. Easing the blend out
    /// turns that into a controlled swing back into the shaft, which is a
    /// good moment rather than a bug - you ride the rope home with the loot.
    ///
    /// If you would rather NOT be taken along, cut your tether with F before
    /// releasing. That is a real choice, not an oversight.
    /// </summary>
    public void ClearHook(object owner)
    {
        if (!ReferenceEquals(hookOwner, owner)) return;
        hookOwner = null;
        hookReleasing = true;   // hookActive stays true until the blend runs out
    }

    /// <summary>Kilograms hanging on the winch. Players and cargo both.</summary>
    public void AddLoad(float kilograms) => loadAccumulator += kilograms;

    // --------------------------------------------------------------------
    // CARGO SLOTS
    //
    // Everything clipped to the rope registers here, so the next thing to be
    // clipped can find a gap instead of landing inside whatever is already
    // there. Without this, a whole run's loot stacked into a single point and
    // rendered as one flickering mess.
    //
    // It also makes the rope read correctly at a glance: a line of objects
    // hanging at intervals tells you how loaded you are without a single
    // number of UI.
    // --------------------------------------------------------------------

    readonly System.Collections.Generic.List<Carryable> attachedCargo =
        new System.Collections.Generic.List<Carryable>();

    public void RegisterCargo(Carryable c)
    {
        if (c != null && !attachedCargo.Contains(c)) attachedCargo.Add(c);
    }

    public void UnregisterCargo(Carryable c) => attachedCargo.Remove(c);

    /// <summary>
    /// The nearest depth to <paramref name="desired"/> with nothing already
    /// hanging within <paramref name="spacing"/> of it.
    ///
    /// Searches outward - one slot down, one slot up, two down, two up - so
    /// cargo lands as close as possible to where you clipped it rather than
    /// all sliding to the bottom.
    /// </summary>
    public float FindFreeDepth(float desired, float spacing)
    {
        desired = Mathf.Clamp(desired, 0f, ropeLength);
        spacing = Mathf.Max(0.4f, spacing);

        if (IsDepthFree(desired, spacing)) return desired;

        for (float step = spacing; step <= ropeLength; step += spacing)
        {
            float below = desired + step;
            if (below <= ropeLength && IsDepthFree(below, spacing)) return below;

            float above = desired - step;
            if (above >= 0f && IsDepthFree(above, spacing)) return above;
        }

        return desired;   // rope is completely full; stack and accept it
    }

    bool IsDepthFree(float depth, float spacing)
    {
        for (int i = attachedCargo.Count - 1; i >= 0; i--)
        {
            var c = attachedCargo[i];
            if (c == null) { attachedCargo.RemoveAt(i); continue; }   // destroyed
            if (Mathf.Abs(c.ropeDepth - depth) < spacing) return false;
        }
        return true;
    }

    /// <summary>Called by the shop between runs.</summary>
    public void ExtendRope(float extraMetres)
    {
        ropeLength = Mathf.Clamp(ropeLength + extraMetres, 0f, maxRopeLength);
    }

    void FixedUpdate()
    {
        CurrentLoad = loadAccumulator;
        UpdateOverload();

        // Ease a released hook back to straight instead of dropping it in one
        // frame. Everything clipped to the rope follows PointAtDepth, so an
        // instant change is an instant teleport for every player and every
        // piece of cargo on the line.
        if (hookReleasing)
        {
            hookBlend = Mathf.MoveTowards(hookBlend, 0f,
                Time.fixedDeltaTime / Mathf.Max(0.01f, hookReleaseTime));

            if (hookBlend <= 0.0001f)
            {
                hookActive = false;
                hookReleasing = false;
            }
        }

        // Soft target from hanging bodies. The hook is no longer part of this
        // spring at all - it kinks the rope's shape directly in PointAtDepth,
        // which is what makes it a redirect rather than a bend.
        Vector3 target = Vector3.ClampMagnitude(pullAccumulator * swayResponse, maxSway);

        // A damped spring chasing that target: pulled toward it, bled off by
        // whatever velocity we already have.
        Vector3 acceleration = (target - swayOffset) * swayStiffness
                             - swayVelocity * swayDamping;

        swayVelocity += acceleration * Time.fixedDeltaTime;
        swayOffset += swayVelocity * Time.fixedDeltaTime;
        swayOffset = Vector3.ClampMagnitude(swayOffset, maxSway);

        pullAccumulator = Vector3.zero;
        loadAccumulator = 0f;
    }

    /// <summary>
    /// The anchor gives way if you hold it over the limit.
    ///
    /// The grace period is the whole design of this. Instant failure would
    /// read as an unfair death; a second and a half of the winch screaming is
    /// long enough to hit G and ditch your pack, or to unclip something you
    /// just loaded. The load limit is not there to kill you, it is there to
    /// make you choose what to throw away, in a hurry, out loud.
    ///
    /// The timer only counts up, never down while you are still over. Get
    /// under the limit and it resets - so nibbling at the edge is survivable
    /// and sitting on it is not.
    /// </summary>
    void UpdateOverload()
    {
        if (Snapped) return;

        if (CurrentLoad > loadLimit)
        {
            overloadTimer += Time.fixedDeltaTime;
            if (overloadTimer >= overloadGrace) Snap();
        }
        else
        {
            overloadTimer = 0f;
        }
    }

    void Snap()
    {
        Snapped = true;
        Debug.Log("[Rope] ANCHOR TORE OUT");

        // The rope is simply gone. Everything clipped to it is in free fall,
        // which happens for free: PlayerTether stops constraining once the
        // rope reports Snapped, and cargo stops being driven.
        if (line != null) line.enabled = false;
        if (endStopper != null) endStopper.gameObject.SetActive(false);

        // Copy the list BEFORE releasing anything.
        //
        // UnclipFromRope calls back into UnregisterCargo, which removes from
        // this very list - modifying a collection while foreach is walking it
        // throws InvalidOperationException. Take a snapshot, clear, then
        // release from the snapshot.
        var releasing = attachedCargo.ToArray();
        attachedCargo.Clear();

        foreach (var c in releasing)
            if (c != null) c.UnclipFromRope();
    }

    void Reset()
    {
        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
    }

    // Drawn in LateUpdate at render rate, not FixedUpdate, or the rope would
    // stutter next to the smoothly interpolated player.
    void LateUpdate()
    {
        if (line == null) return;

        int count = Mathf.Max(2, visualSegments);
        line.useWorldSpace = true;
        line.positionCount = count;
        line.startWidth = visualWidth;
        line.endWidth = visualWidth;

        // Billboarding to the camera and tiling the texture along the length.
        // Without View alignment the rope is a flat ribbon that disappears
        // edge-on; without Tile a rope texture would stretch instead of
        // repeating. Neither costs anything and both are what makes a
        // LineRenderer read as a cylinder rather than a strip of paper.
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Tile;

        for (int i = 0; i < count; i++)
            line.SetPosition(i, PointAtDepth(ropeLength * i / (count - 1f)));

        UpdateEndStopper();
    }

    // A weighted knot on the end of the rope. Ropes have ends. Without one the
    // line simply stops in mid-air, which looks unfinished and - more
    // importantly - gives players no way to see where the rope runs out. That
    // matters, because running out of rope is the thing the shop exists to fix.
    void UpdateEndStopper()
    {
        if (!showEndStopper)
        {
            if (endStopper != null) endStopper.gameObject.SetActive(false);
            return;
        }

        if (endStopper == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "RopeEnd";
            Destroy(go.GetComponent<Collider>());   // must never push anyone

            if (line != null && line.sharedMaterial != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = line.sharedMaterial;

            endStopper = go.transform;
        }

        endStopper.gameObject.SetActive(true);
        endStopper.localScale = Vector3.one * endStopperSize;
        endStopper.position = PointAtDepth(ropeLength);
    }

    // The overload warning. Big, central, counting down.
    //
    // A number that is visibly running out is worth far more than a red bar:
    // it tells the crew exactly how long they have to argue about who drops
    // what, and it is something one player can shout at the others.
    void OnGUI()
    {
        // Don't draw gameplay chrome over the results/shop screen.
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        if (Snapped)
        {
            var dead = new GUIStyle(GUI.skin.label)
            { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            dead.normal.textColor = new Color(1f, 0.25f, 0.2f);
            GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 40f),
                      "THE ANCHOR TORE OUT", dead);
            return;
        }

        if (!Overloaded) return;

        float left = SecondsToFailure;

        // Flashes faster as it runs out.
        float blink = Mathf.PingPong(Time.time * (2f + 6f * FailureProgress), 1f);

        var warn = new GUIStyle(GUI.skin.label)
        { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        warn.normal.textColor = new Color(1f, 0.3f + blink * 0.3f, 0.2f);

        GUI.Label(new Rect(0f, Screen.height * 0.26f, Screen.width, 34f),
            $"ANCHOR FAILING   {left:0.0}s", warn);

        var sub = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        sub.normal.textColor = new Color(1f, 0.75f, 0.5f);

        GUI.Label(new Rect(0f, Screen.height * 0.26f + 36f, Screen.width, 24f),
            $"{CurrentLoad:0}kg on a {loadLimit:0}kg anchor  -  drop something NOW  (G ditches your pack)",
            sub);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Application.isPlaying && Overloaded ? Color.red : Color.yellow;
        Vector3 prev = PointAtDepth(0f);
        for (int i = 1; i <= 12; i++)
        {
            Vector3 next = PointAtDepth(ropeLength * i / 12f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        Gizmos.DrawWireSphere(PointAtDepth(ropeLength), 0.25f);
    }
}