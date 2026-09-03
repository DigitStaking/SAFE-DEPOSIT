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
    public float stepLead = 0.13f;

    [Tooltip("How much of a forward stride this leg is willing to take " +
             "SIDEWAYS.\n\n" +
             "A person steps far less to the side than they do forward - the " +
             "hip opens maybe half as far as it swings - so a stride budget " +
             "that is the same in every direction throws the foot much too far " +
             "out on a strafe. This is the fix for that, and it is anatomy " +
             "rather than taste.\n\n" +
             "Applied to the lead, to how far the foot may reach, and to how " +
             "far it may drift before stepping, so all three stay consistent.")]
    [Range(0.2f, 1f)] public float lateralScale = 0.55f;

    [Header("Step timing")]
    [Tooltip("How fast the foot travels through the air, in metres per second. " +
             "THE MAIN SPEED KNOB - lower makes the whole gait more deliberate.")]
    public float footSwingSpeed = 3.4f;

    [Tooltip("Shortest a step may take. Stops a tiny correction becoming a " +
             "twitch.")]
    public float stepTimeMin = 0.22f;

    [Tooltip("Longest a step may take. Stops a huge recovery stride turning " +
             "into slow motion.")]
    public float stepTimeMax = 0.45f;

    [Tooltip("Smallest lift, in metres. A shuffling correction still clears " +
             "the floor by this much.")]
    public float stepArc = 0.07f;

    [Tooltip("How high the foot lifts as a FRACTION OF HOW FAR THAT STEP " +
             "TRAVELS. This is the fix for feet that skim the floor. " +
             "A fixed lift is the bug: 0.11m looks like a step when the foot " +
             "moves 0.3m and looks like a slide when it moves 1.5m, because " +
             "what the eye reads is not the height, it is the ANGLE the foot " +
             "leaves the ground at. A real step lifts 15-20% of its own " +
             "length, so a long stride lifts high and a short shuffle stays " +
             "low - both without being told which they are.")]
    [Range(0.05f, 0.4f)] public float arcPerLength = 0.2f;

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

    [Header("Turning on the spot")]
    [Tooltip("Degrees the body may turn under a planted foot before that foot " +
             "must step to catch up. THE FIX FOR FEET THAT SLIDE WHEN YOU " +
             "TURN THE CAMERA. " +
             "Turning in place moves the body not at all, so measured speed " +
             "stays zero and the distance test barely notices: a 90 degree " +
             "turn swings the foot target only about 18cm, under the standing " +
             "budget, so the feet stayed put and the body swivelled on top of " +
             "them. Rotation needed its own threshold because it is not " +
             "travel.")]
    public float turnStepDegrees = 32f;

    [Header("Limits - what stops the legs tangling")]
    [Tooltip("Closest this foot may come to the body centre line, in metres. " +
             "The legs CANNOT cross. Strafing right pulls the left foot to the " +
             "right, and without this it walks through the right leg.")]
    public float minSeparation = 0.07f;

    [Tooltip("How far past its stride budget a foot may be dragged before it " +
             "steps REGARDLESS of the alternation rules. 1 is the normal " +
             "threshold, so this is how much overrun is tolerated.\n\n" +
             "Every alternation rule can refuse a step, and all of them are " +
             "reasonable while a foot is merely late. None are reasonable once " +
             "the leg can no longer reach - refusing then does not delay the " +
             "step, it strands the foot and the IK stretches the limb to hold " +
             "it. A body standing with its legs splayed is two feet that both " +
             "politely waited for the other.")]
    public float strandedAt = 1.7f;

    [Tooltip("Furthest this foot may be planted from directly below the hips, " +
             "in metres. A leg has a length; without this a fast strafe throws " +
             "the target out past where any knee could follow, and step 3 would " +
             "have to stretch the limb to reach it.")]
    public float maxReach = 0.55f;

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

    /// <summary>The body yaw at the moment this foot was planted. A planted
    /// foot does not turn with the body, so the gap between this and the
    /// body's yaw now is how far the character has swivelled on top of it.</summary>
    float plantedYaw;

    /// <summary>Which way the foot itself points. Held while planted - a foot
    /// on the floor does not swivel - and carried round to the body's new yaw
    /// during a step, which is the only time a real foot changes direction.</summary>
    float footYaw;

    float stepFromYaw;

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
        footYaw = transform.eulerAngles.y;
        plantedYaw = footYaw;
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

            float budget = (strideBase + stridePerSpeed * Speed) *
                           (1f - Mathf.Clamp01(cut) * Injury);

            // ---- AND IT MAY NOT EXCEED WHAT THE LEG CAN REACH ----
            //
            // These were two independent numbers that contradicted each other.
            // At full speed the foot was allowed to fall 0.98m behind before
            // stepping, while the leg was only allowed to reach 0.55m - so
            // past walking pace the foot was ALWAYS dragged beyond reach, the
            // IK stretched the limb to hold onto it, and a stretched leg
            // dragging along the floor is exactly what sliding looks like.
            //
            // The reach is the physical fact and the budget is the preference,
            // so the budget yields.
            return Mathf.Min(budget, maxReach * 0.95f);
        }
    }

    /// <summary>How far past its budget this foot is, as a fraction. Public so
    /// the other leg can tell whose need is greater.</summary>
    public float Drift => DriftFraction(restCache);

    /// <summary>
    /// How long this step takes.
    ///
    /// DERIVED FROM THE DISTANCE, not from a speed guess. Subtracting a bit of
    /// time per metre-per-second was the wrong model: it made every step at a
    /// given speed take the same time regardless of how far it was going, so a
    /// long stride and a small correction moved the foot at wildly different
    /// speeds - and the long one was the fast one, which is backwards.
    ///
    /// A foot swings at roughly a constant rate, so time follows distance.
    /// </summary>
    float StepTime =>
        Mathf.Clamp(stepLength / Mathf.Max(0.1f, footSwingSpeed),
                    stepTimeMin, stepTimeMax);

    /// <summary>How far the current step is travelling. Set when it starts,
    /// so the arc and the duration are both sized to the real distance rather
    /// than to a guess about speed.</summary>
    float stepLength;

    /// <summary>How high the foot lifts. Proportional to the distance it is
    /// covering, so it reads as a step at every stride length. A heavy load
    /// flattens it toward a shuffle.</summary>
    float Arc => Mathf.Max(stepArc, stepLength * arcPerLength) *
                 (1f - loadArcFlatten * Load);

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
        // ---- WORKED OUT IN THE BODY'S OWN FRAME ----
        //
        // With a body that never turns, the body IS the fixed reference, and
        // forward and sideways are genuinely different directions to it - which
        // they have to be, because a leg does not reach equally in both.
        Vector3 lead = transform.InverseTransformDirection(Travel * stepLead);
        lead.y = 0f;

        // ---- A PERSON STEPS SHORT SIDEWAYS ----
        //
        // The hip swings much further forward than it opens outward, so a lead
        // that is the same length in every direction throws the foot far too
        // wide on a strafe. That was the splayed stance: a target a full
        // stride out to the side, which the IK then stretched the leg to reach.
        lead.x *= lateralScale;

        // A leg has a length, and that length is an ELLIPSE, not a circle. A
        // clamp to one radius allowed the same reach sideways as forward,
        // which for a hip roughly a metre up meant the leg had to span further
        // than a leg goes - so it splayed instead of stepping.
        lead = ClampToEllipse(lead, maxReach * lateralScale, maxReach);

        Vector3 local = new Vector3(width, 0f, stanceForward) + lead;

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

            // ---- LIFTS FAST, LANDS SOFT ----
            //
            // A plain sine peaks exactly halfway, which means the foot rises
            // and falls at the same rate - and that reads as a hop. A real
            // step snaps off the floor at toe-off and comes down shallow, so
            // the peak sits early. Skewing t before the sine does that in one
            // multiply, with nothing to misconfigure.
            place.y += Mathf.Sin(Mathf.Pow(t, 0.72f) * Mathf.PI) * Arc;

            footPosition = place;
            footYaw = Mathf.LerpAngle(stepFromYaw, transform.eulerAngles.y, Smooth(t));

            if (t >= 1f)
            {
                stepping = false;
                footPosition = stepTo;
                footYaw = transform.eulerAngles.y;
                plantedYaw = footYaw;
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
        float drift = DriftFraction(rest);

        // ---- TURNING COUNTS, AND IT DID NOT BEFORE ----
        //
        // Rotating on the spot moves the body not at all, so the measured
        // speed stays zero and the distance test barely reacts: a 90 degree
        // turn swings a foot target only about 18cm, under the standing
        // budget. So the feet stayed planted and the character swivelled on
        // top of them - which is the sliding reported when turning the camera.
        //
        // Rotation is a displacement the distance test cannot see, so it gets
        // its own threshold and is folded in as whichever need is greater.
        float turned = Mathf.Abs(Mathf.DeltaAngle(plantedYaw, transform.eulerAngles.y));
        drift = Mathf.Max(drift, turned / Mathf.Max(1f, turnStepDegrees));

        if (drift <= 1f) return;

        // ---- A STRANDED FOOT STEPS NO MATTER WHO ELSE IS STEPPING ----
        //
        // This is the bug in the splayed-stance screenshot, and it is separate
        // from the sideways reach being too long.
        //
        // Every rule below can REFUSE a step - the partner is mid-stride, the
        // foot landed a moment ago, the other foot needs it more. All of them
        // are reasonable while the foot is merely late. None of them are
        // reasonable once it has been left so far behind that the leg cannot
        // reach it any more, because then refusing does not delay a step, it
        // strands the foot and the IK stretches the limb to keep hold of it.
        //
        // A body standing with its legs splayed is that: two feet that both
        // politely waited for the other.
        bool stranded = drift > strandedAt;

        if (!stranded)
        {
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
        }

        stepping = true;
        stepAge = 0f;
        stepFrom = footPosition;
        stepTo = rest;

        // ---- THE STEP SIZES ITSELF ----
        //
        // Both the lift and the duration come from how far THIS step is
        // actually going, worked out once here rather than guessed from speed.
        // A long stride lifts high and takes a while; a small correction
        // barely leaves the floor and is over quickly. Neither needs a rule
        // about which it is.
        stepLength = Vector3.Distance(Flat(stepFrom), Flat(stepTo));
        stepTime = StepTime;

        // A step is the only moment a real foot changes which way it points,
        // so the turn is carried round during the swing rather than applied to
        // a foot that is standing on the floor.
        stepFromYaw = footYaw;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>
    /// Shrink a body-local offset until it fits inside an ellipse - sideways
    /// radius first, forward radius second.
    ///
    /// A circle was the wrong shape for every limit in this file. It let the
    /// foot reach as far to the side as it could forward, which no leg does,
    /// and it is why a strafe splayed the stance instead of stepping.
    /// </summary>
    static Vector3 ClampToEllipse(Vector3 local, float sideways, float forward)
    {
        sideways = Mathf.Max(0.01f, sideways);
        forward = Mathf.Max(0.01f, forward);

        float x = local.x / sideways;
        float z = local.z / forward;
        float over = x * x + z * z;

        if (over <= 1f) return local;

        float shrink = 1f / Mathf.Sqrt(over);
        return new Vector3(local.x * shrink, 0f, local.z * shrink);
    }

    /// <summary>
    /// How far past its budget this foot has drifted, as a fraction: under 1
    /// is fine, over 1 must step.
    ///
    /// Measured against the same ellipse for the same reason - a foot dragged
    /// 30cm sideways is in far more trouble than one dragged 30cm forward, and
    /// a single radius called them equally urgent.
    /// </summary>
    float DriftFraction(Vector3 rest)
    {
        Vector3 d = transform.InverseTransformDirection(footPosition - rest);

        float x = d.x / Mathf.Max(0.01f, Stride * lateralScale);
        float z = d.z / Mathf.Max(0.01f, Stride);

        return Mathf.Sqrt(x * x + z * z);
    }

    /// <summary>Ease in and out. A linear step starts and stops abruptly and
    /// looks mechanical; this is one line and fixes it.</summary>
    static float Smooth(float t) => t * t * (3f - 2f * t);

    // ---- what step 3 will read ------------------------------------------

    /// <summary>Where the IK goal should go, once there is one.</summary>
    public Vector3 FootPosition => footPosition;

    /// <summary>The slope under the planted foot, for tilting it in step 3.</summary>
    public Vector3 FootNormal => footNormal;

    /// <summary>
    /// Which way this foot points, in degrees.
    ///
    /// NOT the body's yaw. A planted foot does not swivel when you turn the
    /// camera - it stays where it was put until it steps - and using the live
    /// body yaw made every standing foot rotate under the character like it
    /// was on ice.
    /// </summary>
    public float FootYaw => footYaw;

    /// <summary>How far the body has turned since this foot was planted.</summary>
    public float TurnedSincePlanted =>
        Mathf.Abs(Mathf.DeltaAngle(plantedYaw, transform.eulerAngles.y));

    /// <summary>True while this foot is in the air.</summary>
    public bool IsStepping => stepping;

    // ---- what the on-screen readout reports -----------------------------

    /// <summary>
    /// The height of the floor this foot is working over.
    ///
    /// Taken from the target rather than from the body, so that a lift
    /// measured against it is a lift off THE GROUND UNDER THE FOOT and not off
    /// whatever height the player happens to be standing at. On a staircase
    /// those are different numbers, and the one that matters is this one.
    /// </summary>
    public float GroundHeight => restCache.y;

    /// <summary>How far the current or last step travelled, in metres.</summary>
    public float StepLength => stepLength;

    /// <summary>How high the current or last step lifts, in metres.</summary>
    public float StepLift => Arc;

    /// <summary>How long the current or last step takes, in seconds.</summary>
    public float StepSeconds => stepTime;

    /// <summary>The stride budget right now, in metres.</summary>
    public float StrideBudget => Stride;

    /// <summary>How loaded this body is, 0 to 1.</summary>
    public float LoadAmount => Load;

    /// <summary>How hurt this body is, 0 to 1.</summary>
    public float InjuryAmount => Injury;

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
