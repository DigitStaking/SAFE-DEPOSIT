// ProceduralLegs.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ProceduralLegs.cs
// Goes on: the Player root, next to PlayerMotor.
//
// ========================================================================
// PHASE 5 - LEGS THAT DECIDE WHERE TO STAND.
//
// STEP 1 OF SIX: THE STEPPING DECISION, ONE FOOT, NO IK YET.
//
// Nothing in this file touches the skeleton. It draws a gizmo. That is
// deliberate - the hard part of procedural locomotion is not the IK, it is
// deciding WHEN a foot has been dragged far enough to move and WHERE it
// should land, and that decision is far easier to judge as a ball rolling
// around the floor than as a leg bending inside a character.
//
// If the ball steps convincingly, the leg will. If it does not, no amount of
// IK will rescue it.
//
// ------------------------------------------------------------------------
// WHY THIS REPLACES THE BLEND TREE RATHER THAN TUNING IT
//
// Four independent things were wrong with clip-based walking here, and only
// the last is beyond tuning:
//
//   1. Foot sliding. The Walking clip is authored at roughly 1.5 m/s and
//      moveSpeed is 4.5. The feet cycle at a third of the rate the body
//      travels.
//   2. There is no Run clip. AnimatorBuilder guards for one that was never
//      downloaded, so the tree tops out at a walk.
//   3. Diagonals cross-fade two clips whose feet land at different moments,
//      so the average lands between both - in the air.
//   4. Every clip assumes a flat plane. This building is rubble and slopes.
//
// A procedural foot answers all four at once, because it is not playing
// anything. It looks at the floor that is actually there.
//
// ------------------------------------------------------------------------
// SPEED IS MEASURED, NEVER ASSUMED. THIS IS THE WHOLE DESIGN.
//
// Stride length and step duration come from HOW FAST THIS BODY IS ACTUALLY
// TRAVELLING, sampled from its own position. Not from moveSpeed, not from
// input, not from the Rigidbody.
//
// That one choice is what makes every existing speed rule work for free, and
// there are more of them than there look:
//
//   injury      healthy 1.00, hurt 0.78, critical 0.52, downed 0.00
//   carrying    small 1.00, heavy 0.70, massive 0.45
//   dashboard   externalSpeedLock 0 while you are at the panel
//
// They MULTIPLY. A critical player carrying a safe moves at 0.52 x 0.45 =
// 23% of top speed - 1.05 m/s against 4.5. That is a 4.3x range, hopeless for
// one walk clip and free here: the body is slower, so the measurement is
// smaller, so the steps are shorter and closer together. Nobody had to tell
// the legs that the player was hurt.
//
// It also covers cases nobody wrote a rule for. Walking up a slope, being
// shoved by a teammate, riding a lift that stops sharply - all of them are
// motion, and all of them produce steps, because the only question this asks
// is whether the ground has moved under the foot.
//
// This is the project's own ask-don't-cache rule applied to walking. A cached
// idea of how fast you are supposed to be going is wrong from the moment you
// pick something up. A measured one cannot be.
//
// LOAD AND INJURY ALSO CHANGE THE SHAPE, NOT ONLY THE RATE
//
// Speed alone would make a hurt player look like a healthy one filmed in slow
// motion. So two style inputs sit on top, and they are small on purpose: a
// heavy load widens the stance and flattens the step into a shuffle, and an
// injury shortens the stride further than the speed already did. The limp
// itself - one leg worse than the other - needs two feet and arrives in step 2.
//
// WHY TEAMMATES WILL WORK WITH NO NETWORK CODE AT ALL
//
// Measuring position delta rather than rb.linearVelocity is not a detail, it
// is what makes remote bodies work. A remote Rigidbody is not moving under its
// own power - NetworkTransform writes its transform directly - so its velocity
// reads near zero, which is exactly the bug that had teammates sliding around
// in a permanent idle before OwnerNetworkAnimator existed.
//
// Position is correct on every machine, because position is the one thing that
// has always been replicated. So this runs identically on your body and on
// everybody else's, with no owner gate and no new bytes on the wire. Each
// machine raycasts its own floor, so a teammate's feet follow the rubble that
// machine can see.
// ========================================================================

using UnityEngine;

[DefaultExecutionOrder(25)]
public class ProceduralLegs : MonoBehaviour
{
    public enum Side { Left = -1, Right = 1 }

    [Header("Which leg")]
    [Tooltip("Which side of the body this foot belongs to. Decides which way " +
             "the stance offset points and, from step 2, which foot leads when " +
             "you strafe - stepping right should start with the right foot.")]
    public Side side = Side.Right;

    [Header("Stance")]
    [Tooltip("How far to the side of the body centre line this foot rests, in " +
             "metres. Half the gap between the feet. Always positive - the " +
             "side field above decides the direction.")]
    public float stanceWidth = 0.13f;

    [Tooltip("Forward offset of the resting foot from the body centre. Zero is " +
             "directly below the hips.")]
    public float stanceForward = 0f;

    [Header("Stride - scaled by measured speed")]
    [Tooltip("How far the foot may be dragged from where it wants to be before " +
             "it steps, while standing still. Small: a stationary character " +
             "should correct a badly placed foot quickly.")]
    public float strideBase = 0.22f;

    [Tooltip("Extra allowed drag per metre-per-second of travel. This is what " +
             "makes a run take long strides and a hurt shuffle take short ones, " +
             "and it needs no knowledge of either - only of speed.")]
    public float stridePerSpeed = 0.17f;

    [Tooltip("Seconds of travel the foot aims AHEAD of the body when it steps. " +
             "Stepping to where you are rather than where you are going is how " +
             "a character ends up walking on its heels. This is also what lets " +
             "one piece of code walk backwards and sideways with no extra " +
             "cases: the lead follows the velocity, whatever way it points.")]
    public float stepLead = 0.17f;

    [Header("Step timing")]
    [Tooltip("Seconds a step takes at a standstill. Lower is snappier.")]
    public float stepTimeBase = 0.32f;

    [Tooltip("Seconds shaved off the step time per metre-per-second of travel. " +
             "Faster travel means quicker steps as well as longer ones.")]
    public float stepTimePerSpeed = 0.035f;

    [Tooltip("Floor on step time. Below this, steps read as a twitch rather " +
             "than a stride.")]
    public float stepTimeMin = 0.13f;

    [Tooltip("How high the foot lifts at the middle of a step.")]
    public float stepArc = 0.11f;

    [Header("Load and injury shape the gait")]
    [Tooltip("How much a heavy load widens the stance, in metres at maximum " +
             "load. Carrying a safe makes you plant your feet further apart.")]
    public float loadStanceWiden = 0.07f;

    [Tooltip("How much of the step arc a maximum load removes, 0 to 1. Heavy " +
             "carrying is a shuffle - the feet barely leave the floor.")]
    [Range(0f, 1f)] public float loadArcFlatten = 0.55f;

    [Tooltip("How much of the stride an injury removes at its worst, 0 to 1. " +
             "This is ON TOP of being slower, which already shortens it, and it " +
             "is what separates hurt from strolling.")]
    [Range(0f, 1f)] public float injuryStrideCut = 0.3f;

    [Header("Alternation - what stops both feet leaving the floor")]
    [Tooltip("Seconds this foot must wait after landing before it may step " +
             "again. Without it a foot that lands slightly short steps again " +
             "immediately, which reads as a stutter rather than a stride.")]
    public float minStepGap = 0.08f;

    [Tooltip("This leg is the bad one. When hurt it takes a shorter, quicker " +
             "step than its partner, which is what a limp actually is - an " +
             "UNEVEN gait, not a slow one. Tick this on ONE leg only.")]
    public bool limpsWhenHurt = false;

    [Tooltip("Extra stride this leg loses at full injury, on top of the cut " +
             "both legs already take. 0 to 1.")]
    [Range(0f, 1f)] public float limpExtraCut = 0.35f;

    [Header("Limits - what stops the legs tangling")]
    [Tooltip("Closest this foot may come to the body centre line, in metres. " +
             "The legs CANNOT cross. Strafing right pulls the left foot to the " +
             "right, and without this it walks through the right leg.")]
    public float minSeparation = 0.07f;

    [Tooltip("Furthest this foot may be planted from directly below the hips, " +
             "in metres. A leg has a length; without this a fast strafe throws " +
             "the target out past where any knee could follow, and step 3 would " +
             "have to stretch the limb to reach it.")]
    public float maxReach = 0.62f;

    [Header("Ground")]
    [Tooltip("What counts as floor. Copied from PlayerMotor on Awake so the " +
             "feet and the ground check can never disagree about what a floor is.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Metres above the target the probe starts. Must clear the tallest " +
             "step or piece of rubble a foot should be able to climb onto.")]
    public float probeUp = 0.8f;

    [Tooltip("Metres below the target the probe reaches. Must clear the deepest " +
             "hole a foot should still find a floor in.")]
    public float probeDown = 1.6f;

    [Header("Debug - step 1 has no IK, only this")]
    [Tooltip("Draw the foot, its target and its probe in the SCENE view. " +
             "Gizmos never render in the Game view, which is why the markers " +
             "below exist as well.")]
    public bool drawGizmos = true;

    [Tooltip("Spawn real spheres for the foot and its target, so they are " +
             "visible in the GAME view while you play.\n\n" +
             "Gizmos were the obvious choice and the wrong one here: they draw " +
             "only in the Scene view, and this game is played in a pitch-dark " +
             "building. A debug visual you have to stop and go looking for is a " +
             "debug visual that does not get looked at.\n\n" +
             "These are unlit, so the dark cannot hide them, and they cast no " +
             "shadow and carry no collider - nothing can bump into them, pick " +
             "them up or scan them for a price.")]
    public bool showRuntimeMarkers = true;

    [Tooltip("Diameter of the debug spheres, in metres.")]
    public float markerSize = 0.085f;

    // ---- WHAT THIS BODY IS ACTUALLY DOING -------------------------------

    /// <summary>Horizontal speed in metres per second, MEASURED from position.
    /// Every gait number below is derived from this one.</summary>
    public float Speed { get; private set; }

    /// <summary>Horizontal travel, in metres per second. Zero while standing.</summary>
    public Vector3 Travel { get; private set; }

    Vector3 lastSamplePosition;
    bool sampled;

    // ---- THE FOOT -------------------------------------------------------

    /// <summary>Where the foot is right now. Between steps this does not move,
    /// which is the entire point: a planted foot stays on the floor while the
    /// body travels over it, and that is what makes sliding impossible.</summary>
    Vector3 footPosition;

    /// <summary>The ground normal under the planted foot. Unused until step 3
    /// tilts the foot to match the slope; stored now because the raycast that
    /// knows it already happens here.</summary>
    Vector3 footNormal = Vector3.up;

    bool stepping;
    float stepAge;
    float stepTime;
    Vector3 stepFrom;
    Vector3 stepTo;
    bool probeHit;

    PlayerCarry carry;
    PlayerHealth health;

    /// <summary>The other leg on this body, found rather than wired.
    /// Two components that have to agree are two chances to forget one.</summary>
    ProceduralLegs partner;

    /// <summary>When this foot last touched down. Guards minStepGap.</summary>
    float lastLanded = -999f;

    void Awake()
    {
        carry = GetComponent<PlayerCarry>();
        health = GetComponent<PlayerHealth>();

        foreach (var leg in GetComponents<ProceduralLegs>())
            if (leg != this) { partner = leg; break; }

        var motor = GetComponent<PlayerMotor>();
        if (motor != null) groundMask = motor.groundMask;
    }

    void OnEnable()
    {
        // Plant where we are, not at the origin. A foot starting at (0,0,0)
        // takes one enormous step across the level on the first frame - the
        // same class of bug as the elevator rider whose lastCarPosition
        // defaulted to zero and fired them out of the lift.
        sampled = false;
        stepping = false;
        Speed = 0f;
        Travel = Vector3.zero;
        footPosition = Rest();
    }

    // --------------------------------------------------------------------
    // MEASUREMENT
    // --------------------------------------------------------------------

    /// <summary>
    /// Update, not FixedUpdate, and it matters twice.
    ///
    /// Feet are a VISUAL system: they should resolve at render rate, or they
    /// inherit the physics step's stutter on every machine whose framerate is
    /// not a multiple of it.
    ///
    /// And a remote body only moves in Update - NetworkTransform interpolates
    /// there. Sampling in FixedUpdate would read a teammate's position twice
    /// between two arrivals and conclude, half the time, that they had stopped.
    /// </summary>
    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Measure(dt);
        Advance(dt);
        ShowMarkers();
    }

    // --------------------------------------------------------------------
    // SOMETHING YOU CAN ACTUALLY SEE WHILE PLAYING
    // --------------------------------------------------------------------

    Transform footMarker;
    Transform restMarker;
    Material footMaterial;

    void ShowMarkers()
    {
        if (!showRuntimeMarkers)
        {
            if (footMarker != null) ClearMarkers();
            return;
        }

        if (footMarker == null)
        {
            footMarker = MakeMarker("~legFoot", Color.green, markerSize);
            footMaterial = footMarker.GetComponent<Renderer>().material;
        }

        if (restMarker == null)
            restMarker = MakeMarker("~legRest", new Color(0.25f, 0.7f, 1f),
                                    markerSize * 0.6f);

        footMarker.position = footPosition;
        restMarker.position = restCache;

        // Yellow in the air, green planted - the same code the gizmo uses, so
        // the two views cannot disagree about what the foot is doing.
        Tint(footMaterial, stepping ? Color.yellow : Color.green);
    }

    /// <summary>
    /// A sphere that survives a dark room: unlit so no lamp is needed, no
    /// shadow so it cannot be mistaken for geometry, and NO COLLIDER - which
    /// matters more than it sounds. A collider here would be something the
    /// player could walk into, the pickup ray could hit, and the price scanner
    /// could try to value.
    /// </summary>
    Transform MakeMarker(string name, Color colour, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.hideFlags = HideFlags.DontSave;

        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        go.transform.localScale = Vector3.one * size;

        var renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var material = new Material(shader);
        renderer.material = material;
        Tint(material, colour);

        return go.transform;
    }

    /// <summary>
    /// URP's Unlit uses _BaseColor and the old built-in shaders use _Color.
    /// Setting only one leaves the sphere magenta or white depending on which
    /// pipeline answered Shader.Find, so set whichever is actually there.
    /// </summary>
    static void Tint(Material material, Color colour)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
    }

    void ClearMarkers()
    {
        if (footMarker != null) Destroy(footMarker.gameObject);
        if (restMarker != null) Destroy(restMarker.gameObject);

        if (footMaterial != null) Destroy(footMaterial);

        footMarker = null;
        restMarker = null;
        footMaterial = null;
    }

    void OnDisable() => ClearMarkers();

    void Measure(float dt)
    {
        Vector3 here = transform.position;

        if (!sampled)
        {
            lastSamplePosition = here;
            sampled = true;
            return;
        }

        Vector3 moved = here - lastSamplePosition;
        lastSamplePosition = here;

        // Horizontal only. Riding the lift down is not walking and a fall is
        // not a sprint - both are large vertical deltas that would otherwise
        // read as speed and set the feet running in mid-air.
        moved.y = 0f;

        Vector3 perSecond = moved / dt;

        // Smoothed, because a single frame's delta is noisy enough to flicker
        // the stride length visibly. Fast on the way up and the way down: this
        // is not a meter that needs to look calm, it needs to be right.
        Speed = Mathf.Lerp(Speed, perSecond.magnitude, 1f - Mathf.Exp(-12f * dt));

        Travel = perSecond.sqrMagnitude > 0.0001f ? perSecond : Vector3.zero;
    }

    // --------------------------------------------------------------------
    // THE GAIT, DERIVED
    // --------------------------------------------------------------------

    /// <summary>
    /// 0 empty-handed, 1 at maximum load.
    ///
    /// READ LIVE from PlayerCarry rather than stored, for the reason this
    /// project keeps rediscovering: a cached answer about what you are holding
    /// is wrong from the moment you put it down.
    /// </summary>
    float Load
    {
        get
        {
            if (carry == null || !carry.IsCarrying) return 0f;

            // SpeedMultiplier: 1 small, 0.7 heavy, 0.45 massive.
            return Mathf.InverseLerp(1f, 0.45f, carry.SpeedMultiplier);
        }
    }

    /// <summary>0 healthy, 1 at critical. Downed is excluded - a downed player
    /// is not walking badly, they are not walking.</summary>
    float Injury
    {
        get
        {
            if (health == null || health.IsDowned) return 0f;

            // SpeedFactor: 1 healthy, 0.78 hurt, 0.52 critical.
            return Mathf.InverseLerp(1f, 0.52f, health.SpeedFactor);
        }
    }

    /// <summary>
    /// How far the foot may drift before it must step.
    ///
    /// The limp lives here rather than in a clip. A limp is not slowness - a
    /// slow walk is still even - it is one leg taking a SHORTER step than the
    /// other, so the body lurches once per cycle. Cutting the stride on one
    /// side and letting the other side stay full produces that for free,
    /// because the good leg then has to cover the distance the bad one did not.
    /// </summary>
    float Stride
    {
        get
        {
            float cut = injuryStrideCut;
            if (limpsWhenHurt) cut += limpExtraCut;

            return (strideBase + stridePerSpeed * Speed) *
                   (1f - Mathf.Clamp01(cut) * Injury);
        }
    }

    /// <summary>How far this foot currently is from where it wants to be.
    /// Public so the other leg can tell whose need is greater.</summary>
    public float Drift => Vector3.Distance(Flat(footPosition), Flat(restCache));

    /// <summary>How long a step takes, in seconds.</summary>
    float StepTime =>
        Mathf.Max(stepTimeMin, stepTimeBase - stepTimePerSpeed * Speed);

    /// <summary>How high the foot lifts. A heavy load flattens it to a shuffle.</summary>
    float Arc => stepArc * (1f - loadArcFlatten * Load);

    /// <summary>
    /// Where this foot would ideally be standing right now: beside the body,
    /// led along the direction of travel, dropped onto whatever floor is
    /// actually there.
    /// </summary>
    Vector3 Rest()
    {
        float width = (stanceWidth + loadStanceWiden * Load) * (int)side;

        // ---- THE LEAD IS WHY THERE ARE NO DIRECTIONAL CASES ----
        //
        // The foot aims at where the body WILL be, and Travel is a WORLD
        // vector that already points wherever the player is actually going -
        // forward, backward, sideways, diagonal, or being shoved by somebody
        // else. It is never rotated into the body's frame, and that is the
        // point:
        //
        //   THE BODY DOES NOT TURN. THE FEET DO ALL OF IT.
        //
        // PlayerMotor welds the body to the camera, so pressing S walks you
        // backwards while still facing forward and pressing A strafes you left
        // while still facing forward. The chest cannot show which way you are
        // going, so the feet have to - and because the lead follows a world
        // velocity rather than an input axis, all 360 degrees are one case.
        // There is no "if walking left" in this file and there never needs to
        // be, which is the whole reason this replaces five clips with none.
        Vector3 offset = transform.right * width
                       + transform.forward * stanceForward
                       + Travel * stepLead;

        // ---- CLAMPED IN THE BODY'S OWN FRAME ----
        //
        // With a body that never turns, the body IS the fixed reference, and
        // two things have to be true in it no matter which way you travel.
        Vector3 local = transform.InverseTransformDirection(offset);
        local.y = 0f;

        // A leg has a length. A fast strafe throws the raw target far past
        // where any knee could follow, and step 3 would have to stretch the
        // limb to reach it.
        local = Vector3.ClampMagnitude(local, maxReach);

        // ---- AND THE LEGS MUST NOT CROSS ----
        //
        // This is the case that only exists because the body is fixed. Strafe
        // right and the lead pulls BOTH feet right - including the left one,
        // which would walk straight through the right leg.
        //
        // Holding each foot on its own side turns that into what a person
        // actually does side-stepping: the leading foot steps out, the
        // trailing foot closes up behind it. The gait falls out of the
        // constraint rather than being animated.
        if (side == Side.Right) local.x = Mathf.Max(local.x, minSeparation);
        else                    local.x = Mathf.Min(local.x, -minSeparation);

        return Ground(transform.position + transform.TransformDirection(local));
    }

    /// <summary>
    /// Drop a point onto the floor beneath it. Returns the point unchanged when
    /// there is no floor - stepping over a stairwell should not snap the foot
    /// to the bottom of the shaft.
    /// </summary>
    Vector3 Ground(Vector3 point)
    {
        Vector3 from = point + Vector3.up * probeUp;

        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit,
                            probeUp + probeDown, groundMask,
                            QueryTriggerInteraction.Ignore))
        {
            probeHit = true;
            footNormal = hit.normal;
            return hit.point;
        }

        probeHit = false;
        return point;
    }

    // --------------------------------------------------------------------
    // STEPPING
    // --------------------------------------------------------------------

    /// <summary>
    /// The last target worked out this frame.
    ///
    /// Rest() casts a ray, and the gizmo, the marker and the step logic all
    /// want the answer. Asking three times gave three raycasts per foot per
    /// frame and, worse, three chances for the debug view to disagree with
    /// what the foot was actually doing.
    /// </summary>
    Vector3 restCache;

    void Advance(float dt)
    {
        Vector3 rest = Rest();
        restCache = rest;

        if (stepping)
        {
            stepAge += dt;
            float t = stepTime <= 0f ? 1f : Mathf.Clamp01(stepAge / stepTime);

            // ---- THE TARGET KEEPS MOVING MID-STEP ----
            //
            // stepTo is re-aimed every frame rather than fixed when the step
            // began. A step lasts a third of a second and the body travels
            // during it, so committing to the old target lands every foot a
            // third of a second behind, permanently - which reads as wading.
            stepTo = rest;

            Vector3 place = Vector3.Lerp(stepFrom, stepTo, Smooth(t));

            // A sine arch: zero at both ends, highest in the middle. Cheaper
            // and steadier than a curve asset, and nothing to misconfigure.
            place.y += Mathf.Sin(t * Mathf.PI) * Arc;

            footPosition = place;

            if (t >= 1f)
            {
                stepping = false;
                footPosition = stepTo;
                lastLanded = Time.time;
            }

            return;
        }

        // ---- PLANTED. THE BODY MOVES, THE FOOT DOES NOT. ----
        //
        // This is the half that stops sliding, and it is the half that is
        // impossible with a clip: the foot holds a fixed WORLD position while
        // the character walks over it, so it cannot skate no matter how fast
        // the body is travelling.
        float drift = Vector3.Distance(Flat(footPosition), Flat(rest));

        if (drift <= Stride) return;

        // ---- ONE FOOT ON THE FLOOR AT A TIME ----
        //
        // Both feet drift at the same rate and cross the same threshold on
        // the same frame, so without this they step together - and a body
        // whose feet leave the ground simultaneously is not walking, it is
        // hopping. This is the whole of step 2, and it is a single check.
        if (partner != null && partner.stepping) return;

        // Landed a moment ago. A foot that lands slightly short would step
        // again immediately, which reads as a stutter rather than a stride.
        if (Time.time - lastLanded < minStepGap) return;

        // ---- AND WHOEVER NEEDS IT MORE GOES FIRST ----
        //
        // This is what makes the LEADING foot lead, and it needs no rule about
        // which direction you are travelling.
        //
        // Strafe right and the crossover clamp holds the left foot near the
        // centre line while the right foot's target runs away to the right. So
        // the right foot is dragged further, so the right foot steps - which
        // is exactly what a person does side-stepping right. Reverse it and
        // the left leads instead. The gait falls out of the geometry.
        //
        // Strictly greater, so two feet at identical drift can never both
        // yield and deadlock.
        if (partner != null && !partner.stepping && partner.Drift > drift + 0.001f)
            return;

        stepping = true;
        stepAge = 0f;
        stepTime = StepTime;
        stepFrom = footPosition;
        stepTo = rest;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>Ease in and out. A linear step starts and stops abruptly and
    /// looks mechanical; this is one line and fixes it.</summary>
    static float Smooth(float t) => t * t * (3f - 2f * t);

    // ---- what step 3 will read ------------------------------------------

    /// <summary>Where the IK goal should go, once there is one.</summary>
    public Vector3 FootPosition => footPosition;

    /// <summary>The slope under the planted foot, for tilting it in step 3.</summary>
    public Vector3 FootNormal => footNormal;

    /// <summary>True while this foot is in the air.</summary>
    public bool IsStepping => stepping;

    // --------------------------------------------------------------------
    // THE ONLY OUTPUT STEP 1 HAS
    // --------------------------------------------------------------------

    void OnDrawGizmos()
    {
        if (!drawGizmos || !Application.isPlaying) return;

        Vector3 rest = restCache;

        // Where the foot wants to be. Blue when the probe found floor, orange
        // when it did not - so a foot hunting for ground that is not there is
        // obvious rather than mysterious.
        Gizmos.color = probeHit ? new Color(0.3f, 0.8f, 1f) : new Color(1f, 0.35f, 0.2f);
        Gizmos.DrawWireSphere(rest, 0.05f);

        // The stride budget. Cross this line and the foot must move.
        Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
        DrawCircle(Flat(footPosition) + Vector3.up * (footPosition.y + 0.02f), Stride);

        // The foot itself: yellow in the air, green planted.
        Gizmos.color = stepping ? Color.yellow : Color.green;
        Gizmos.DrawSphere(footPosition, 0.06f);

        // Its probe.
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawLine(rest + Vector3.up * probeUp, rest - Vector3.up * probeDown);
    }

    static void DrawCircle(Vector3 centre, float radius)
    {
        const int steps = 28;
        Vector3 prev = centre + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 2f;
            Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f,
                                                Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
