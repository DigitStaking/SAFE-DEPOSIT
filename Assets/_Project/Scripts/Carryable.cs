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

    /// <summary>
    /// World bounds of everything this object draws.
    ///
    /// Exists so hands can be placed ON the actual object rather than at a
    /// hand-authored offset. A grab animation is authored for ONE size, so a
    /// can and a filing cabinet would get the same hand separation and the
    /// hands would float inside the small one and clip through the big one.
    /// Measured bounds give every item the right grip for free.
    ///
    /// Renderers rather than colliders, because what the player sees is what
    /// their hands should be touching - a collider is often a rough box around
    /// a more interesting shape.
    /// </summary>
    public Bounds WorldBounds
    {
        get
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>();

            var found = new Bounds(transform.position, Vector3.one * 0.2f);
            bool any = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled) continue;

                if (!any) { found = renderers[i].bounds; any = true; }
                else found.Encapsulate(renderers[i].bounds);
            }

            return found;
        }
    }

    // ====================================================================
    // HOW THIS PARTICULAR THING IS HELD.
    //
    // "each box has her own dimension and way to grab and each item too, so
    //  can i add like list of items and how i can grab each and save the data"
    //
    // Yes, and it belongs HERE rather than in a separate table. This component
    // is already on every loot prefab, so the grip is saved with the item it
    // describes: no lookup key to keep in sync, no list to remember to add a
    // new prop to, and duplicating a prefab duplicates its grip for free.
    //
    // TWO LEVELS, and it matters which one you reach for:
    //
    //   Auto     measured from the object's bounds. Costs nothing, right for
    //            most crates, and is what every item gets until you say
    //            otherwise. A new prop dropped in the level is already
    //            grippable.
    //
    //   Custom   two points you place by hand, one per hand, stored in the
    //            ITEM'S OWN SPACE so they rotate and move with it. For the
    //            things Auto cannot know about - a briefcase handle, a
    //            cabinet's recessed lip, a body carried under the arms.
    //
    // Start on Auto. Switch a specific item to Custom when Auto is wrong for
    // it. Seed the Custom points from the measured ones first (there is a
    // button) so you are adjusting a decent guess rather than dragging two
    // hands out of the origin.
    // ====================================================================

    public enum GripMode
    {
        Auto,      // measured from bounds every frame
        Custom     // the two points below, in this object's local space
    }

    /// <summary>One hand's placement, in the ITEM'S local space so it follows
    /// the object however it is turned.</summary>
    [System.Serializable]
    public class HandGrip
    {
        [Tooltip("Where this hand sits, in the ITEM'S local space.")]
        public Vector3 localPosition;

        [Tooltip("Which way the palm faces, in the ITEM'S local space, in " +
                 "degrees. Drag the rotation handle in the Scene view rather " +
                 "than typing numbers - hand axes are not intuitive.")]
        public Vector3 localEuler;

        [Tooltip("Use this hand at all. Off means the arm is left to the " +
                 "animation - which is what you want for a one-handed carry " +
                 "like a briefcase or a pistol.")]
        public bool used = true;

        // ---- FINGERS ----
        //
        // "we don't grab boxes like that you grab them with fingers"
        //
        // Placing the hand somewhere is only half of a grip. An open flat
        // hand parked against a crate still reads as pushing it, not holding
        // it - the fingers have to close around something.
        //
        // One number per finger, 0 straight and 1 fully curled, because the
        // difference between grips is mostly WHICH fingers close:
        //
        //   crate lip       all four fingers hooked, thumb flat on the face
        //   briefcase       all five closed hard around a handle
        //   large panel     everything half-closed, more of a clamp
        //
        // Curl is applied to the real finger bones after the animation, so it
        // costs no clips and works on any humanoid rig with fingers mapped.

        [Tooltip("An extra twist for the PALM, in degrees, applied on top of " +
                 "the rotation above. " +
                 "Separate because they answer different questions: Rotation " +
                 "aims the hand at the object, Palm Rotation rolls the palm " +
                 "against it. Rolling with the Rotation field means re-aiming " +
                 "the hand every time you want a different wrist angle.")]
        public Vector3 palmRotation;

        // ---- ELBOW: ONE ANGLE, AROUND THE ARM ----
        //
        // "i need just rotation for the elbow, rotation in one axe the axe
        //  where is the direction of arm"
        //
        // Right, and it is not a simplification - it is the actual shape of
        // the problem. With the shoulder fixed and the hand already placed by
        // the grip, the elbow has exactly ONE degree of freedom left: it swings
        // around the line from shoulder to hand, like a hinge on a door whose
        // top and bottom are pinned. Everything else is decided by the two
        // ends and the length of the arm.
        //
        // So a 3D point was the wrong control. It offered three numbers for a
        // one-number problem, and two of them did nothing except move the
        // target somewhere unreachable - which is what a hint of
        // (237, 50, 74) is: a point 250 metres away, in a direction so far off
        // that nudging it changes nothing you can see.
        //
        // One angle, in degrees. 0 leaves the arm where the animation had it;
        // positive and negative swing the elbow out and in.
        //
        // Still driven through AvatarIKHint - the elbow channel of the SAME
        // humanoid solver that places the hand, in the same OnAnimatorIK pass.
        // No second arm solver, and the hand does not move: only the bend
        // between shoulder and hand changes, which is why this is safe to add
        // to grips that are already tuned.

        [Header("Elbow")]
        [Tooltip("Steer this arm's elbow. Off leaves the bend to the solver's " +
                 "own default, which is fine for most things.")]
        public bool useElbowHint = false;

        [Tooltip("Where the elbow sits, in degrees around the shoulder-to-hand " +
                 "axis, which is the only direction it can move once the " +
                 "hand is placed. The hand, its rotation and the item all " +
                 "stay exactly where they are. " +
                 "0 = down, 90 = out away from the body, 180 = up, " +
                 "-90 = in toward the ribs. " +
                 "BODY-RELATIVE, so the same number means the same thing " +
                 "on both arms and a symmetric grip is the same value " +
                 "twice. Out for a wide box, in for a radio.")]
        [Range(-180f, 180f)] public float elbowAngle = 0f;

        [Tooltip("How strongly the elbow is steered, 0 to 1. Part-way nudges " +
                 "the bend rather than dictating it.")]
        [Range(0f, 1f)] public float elbowWeight = 1f;

        [Header("Fingers - 0 straight, 1 fully curled")]
        [Range(0f, 1f)] public float thumb = 0.35f;
        [Range(0f, 1f)] public float index = 0.8f;
        [Range(0f, 1f)] public float middle = 0.85f;
        [Range(0f, 1f)] public float ring = 0.85f;
        [Range(0f, 1f)] public float little = 0.8f;
    }

    // ====================================================================
    // ONE SET OF NUMBERS, ONE PLACE THAT MEASURES WITH THEM.
    //
    // These five used to live only on PlayerCarryArms, which made them
    // CHARACTER-wide: every Auto item in the game shared one width and one
    // height, and a single item could not be nudged without moving all of
    // them. "each box has her own dimension" is not solvable from the
    // character.
    //
    // So they are a struct, the character holds the default, and any item may
    // override it. Not duplicated - OVERRIDDEN, which is the difference
    // between one source of truth with an exception, and two sources of truth
    // that disagree next month.
    // ====================================================================

    [System.Serializable]
    public struct GripMeasure
    {
        [Tooltip("How far out toward the item's edges the hands sit, 0 to 1.")]
        [Range(0.3f, 1.2f)] public float width;

        [Tooltip("WHERE ON THE ITEM'S SIDE the fingers grip: 0 the bottom " +
                 "edge, 1 the top. Near the top, because a crate HANGS from " +
                 "fingers hooked over its sides.")]
        [Range(0f, 1f)] public float heightOnBox;

        [Tooltip("How far INTO the side face the hands sit, in metres.")]
        public float inset;

        [Tooltip("How far toward the player, from the item's centre, in metres.")]
        public float toward;

        [Tooltip("Widest the hands are ever placed apart, in metres.")]
        public float maxWidth;

        /// <summary>The values known to work, and the ONLY place they are
        /// written down. Two editor tools used to hardcode the same four
        /// numbers, which made three sources of truth for one decision.</summary>
        public static GripMeasure Default => new GripMeasure
        {
            width = 0.85f,
            heightOnBox = 0.78f,
            inset = 0.02f,
            toward = 0.06f,

            // ---- 0.30, NOT 0.55, AND THE ARITHMETIC SAYS WHY ----
            //
            // 0.55 each side puts the hands 1.10m apart. With shoulders half a
            // shoulder-width out (0.18m) and the item 0.44m in front, that asks
            // for a shoulder-to-hand distance of
            //
            //     sqrt(0.37^2 + 0.44^2) = 0.57m
            //
            // and this rig's arm reaches 0.49m. Every Auto grip on a wide item
            // was therefore asking for something 8cm out of reach - so the arms
            // locked out straight, which also left the elbows with no bend for
            // any hint to steer.
            //
            // 0.30 keeps a wide crate within reach on THIS character. It is a
            // per-character number, not a universal one: a taller model wants
            // more, and any single item can override it.
            maxWidth = 0.30f
        };
    }

    [Header("Grip - how the hands take hold of THIS item")]
    [Tooltip("Auto measures this object's bounds. Custom uses the two points " +
             "below, placed by hand and saved with this prefab.")]
    public GripMode gripMode = GripMode.Auto;

    public HandGrip leftGrip = new HandGrip();
    public HandGrip rightGrip = new HandGrip();

    [Tooltip("Use THIS item's own measurements below instead of the " +
             "character's. " +
             "For when Auto is nearly right and just needs the hands a little " +
             "higher or wider on this one object - which is most cases, and " +
             "far less work than placing two points by hand.")]
    public bool overrideMeasure = false;

    public GripMeasure measure = GripMeasure.Default;

    // ====================================================================
    // WHERE THE ITEM ITSELF SITS.
    //
    // On top of PlayerCarry's HoldAnchor, which already decides a sensible
    // height and distance from the item's WEIGHT CLASS. These are the
    // per-item correction to that: a flashlight wants to sit forward and
    // tilted, a radio closer in and turned, a crate exactly where the weight
    // class put it.
    //
    // In the BODY'S space, not the world's and not the camera's - X is your
    // right, Z is your forward - so the offset means the same thing whichever
    // way you are facing, and every viewer computes the same answer without
    // anything being sent over the network.
    // ====================================================================

    [Header("Held item transform - offsets from the hold anchor")]
    [Tooltip("Nudge the item itself, in metres, in the BODY'S space: X right, " +
             "Y up, Z forward. Zero leaves it where its weight class puts it.")]
    public Vector3 itemPositionOffset;

    [Tooltip("Turn the item, in degrees, relative to the way the body faces. " +
             "For anything whose model was not authored pointing forward.")]
    public Vector3 itemRotationOffset;

    [Tooltip("Draw the grip points on this item in the Scene view even when " +
             "it is not selected.")]
    public bool alwaysDrawGrips = false;

    // ====================================================================
    // WHICH PREFAB THIS CAME FROM.
    //
    // Stamped by LootSpawner the moment it instantiates one. Before this, the
    // Grip Library matched a held object back to its prefab by NAME with
    // "(Clone)" stripped - because runtime Object.Instantiate produces a plain
    // clone with no prefab connection for PrefabUtility to find.
    //
    // Name matching works right up until two prefabs share a name, or one is
    // renamed, and then it silently saves your tuning onto the wrong item. A
    // direct reference cannot be wrong.
    // ====================================================================

    [Tooltip("The prefab this instance was spawned from. Set automatically at " +
             "spawn - leave it alone. It is what lets the Grip Library save " +
             "your tuning back to the right asset.")]
    public GameObject sourcePrefab;

    public bool HasCustomGrip => gripMode == GripMode.Custom;

    /// <summary>The measurements for this item: its own if it has been given
    /// any, the character's otherwise.</summary>
    public GripMeasure MeasureOr(GripMeasure characterDefault) =>
        overrideMeasure ? measure : characterDefault;

    /// <summary>This item's grip points in WORLD space. Only meaningful in
    /// Custom mode.</summary>
    public void WorldGrips(out Vector3 lPos, out Quaternion lRot,
                           out Vector3 rPos, out Quaternion rRot)
    {
        lPos = transform.TransformPoint(leftGrip.localPosition);
        rPos = transform.TransformPoint(rightGrip.localPosition);
        lRot = transform.rotation * Quaternion.Euler(leftGrip.localEuler)
                                  * Quaternion.Euler(leftGrip.palmRotation);
        rRot = transform.rotation * Quaternion.Euler(rightGrip.localEuler)
                                  * Quaternion.Euler(rightGrip.palmRotation);
    }

    /// <summary>How far this hand's elbow should swing around the arm, and how
    /// strongly. False means leave the elbow alone entirely.</summary>
    public bool ElbowSwing(bool leftHand, out float degrees, out float weight)
    {
        HandGrip g = leftHand ? leftGrip : rightGrip;

        degrees = g.elbowAngle;
        weight = g.elbowWeight;

        return g.used && g.useElbowHint && weight > 0.001f;
    }

    // ====================================================================
    // THE MEASUREMENT, WRITTEN ONCE.
    //
    // PlayerCarryArms.Grips() and SeedGripsFromBounds each used to carry their
    // own copy of this arithmetic. They agreed on the day they were written
    // and had already started to drift: the seed clamped a minimum half-width,
    // the runtime one subtracted the inset, and neither knew about the other.
    //
    // A grip that PREVIEWS differently from how it PLAYS is the worst kind of
    // authoring bug, because the tool tells you it is fine. One function now,
    // called by both.
    // ====================================================================

    /// <summary>
    /// Where the two hands go on this object, measured from its bounds.
    ///
    /// WORLD SPACE throughout, so every number is real metres. The caller
    /// supplies the axes because they differ by caller: the player's right and
    /// forward at runtime, the item's own in the prefab view where there is no
    /// player to ask.
    /// </summary>
    public void MeasuredGrips(Vector3 side, Vector3 toward, GripMeasure m,
                              out Vector3 left, out Vector3 right)
    {
        Bounds b = WorldBounds;

        if (side.sqrMagnitude < 1e-8f) side = Vector3.right;
        if (toward.sqrMagnitude < 1e-8f) toward = Vector3.back;

        side.Normalize();
        toward.Normalize();

        float half = Mathf.Max(b.extents.x, b.extents.z) * m.width;
        half = Mathf.Min(half, m.maxWidth);
        half = Mathf.Max(0.05f, half - m.inset);

        float y = Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(m.heightOnBox));

        Vector3 centre = new Vector3(b.center.x, y, b.center.z) + toward * m.toward;

        left = centre - side * half;
        right = centre + side * half;
    }

    /// <summary>
    /// Fill the two Custom points from the measured bounds, so switching an
    /// item to Custom starts from the Auto answer rather than from nothing.
    ///
    /// Uses the ITEM'S OWN axes, because there is no player involved when you
    /// press the button in the prefab view. If left and right come out swapped
    /// for a model, swap the two X values - which way round they land depends
    /// on how that model was authored and there is nothing to detect it from.
    /// </summary>
    public void SeedGripsFromBounds(GripMeasure m)
    {
        MeasuredGrips(transform.right, -transform.forward, m,
                      out Vector3 lw, out Vector3 rw);

        leftGrip.localPosition = transform.InverseTransformPoint(lw);
        rightGrip.localPosition = transform.InverseTransformPoint(rw);

        // Palms facing each other across the object: the left hand looks
        // right, the right hand looks left. Whatever the hand bone's own axes
        // turn out to be, this at least starts them MIRRORED rather than
        // identical - two identical rotations is what makes one hand look
        // right and the other inside out.
        leftGrip.localEuler = new Vector3(0f, 90f, 0f);
        rightGrip.localEuler = new Vector3(0f, -90f, 0f);

        leftGrip.used = rightGrip.used = true;
    }

    /// <summary>Seed with this item's own measurements if it has them, the
    /// known-good defaults otherwise.</summary>
    public void SeedGripsFromBounds() =>
        SeedGripsFromBounds(overrideMeasure ? measure : GripMeasure.Default);

    /// <summary>
    /// Copy another Carryable's grip configuration onto this one, verbatim.
    ///
    /// This is what the Grip Library's save uses: the FIELDS as they stand,
    /// never the world points they happened to produce this frame. Saving
    /// computed points is how the character's palm angle got baked into an
    /// item and then applied a second time on the next pickup, rotating the
    /// saved grip another 90 degrees on every press of the button.
    /// </summary>
    public void CopyGripFrom(Carryable from)
    {
        if (from == null) return;

        gripMode = from.gripMode;
        leftGrip = Clone(from.leftGrip);
        rightGrip = Clone(from.rightGrip);
        overrideMeasure = from.overrideMeasure;
        measure = from.measure;

        itemPositionOffset = from.itemPositionOffset;
        itemRotationOffset = from.itemRotationOffset;
    }

    /// <summary>A real copy. HandGrip is a class, so assigning it would leave
    /// a prefab asset sharing one object with a scene instance that is about
    /// to be destroyed.</summary>
    static HandGrip Clone(HandGrip g) => new HandGrip
    {
        localPosition = g.localPosition,
        localEuler = g.localEuler,
        used = g.used,
        palmRotation = g.palmRotation,
        useElbowHint = g.useElbowHint,
        elbowAngle = g.elbowAngle,
        elbowWeight = g.elbowWeight,
        thumb = g.thumb,
        index = g.index,
        middle = g.middle,
        ring = g.ring,
        little = g.little
    };

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