using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the life of a floor: one exists at a time, built when the lift
/// arrives and destroyed when it leaves.
///
/// ====================================================================
/// ONLY THE CURRENT FLOOR EXISTS
///
/// Twenty floors of eight to fourteen rooms is somewhere near two hundred
/// rooms, each with a dozen colliders and meshes. Building them all at Play is
/// most of a minute of hitching and a scene nobody can navigate in the editor,
/// to render one floor at a time.
///
/// So a floor is generated on arrival and destroyed on departure. Nothing is
/// hidden or pooled - hidden objects still cost colliders, physics and memory,
/// which is the entire thing this avoids.
///
/// THE SEED, AND WHY THERE IS NOT A NEW ONE
///
/// Campaign.RunNumber is already a host-owned NetworkVariable&lt;int&gt; that
/// every machine agrees on and that changes when a new run starts. That is
/// exactly the contract a run seed needs:
///
///     same run + same floor  -> the same layout, every time, on every machine
///     new run                -> new layouts
///
/// Leaving floor 3 and coming back to it rebuilds it identically, because
/// nothing is remembered - it is recomputed from (RunNumber, floor). And a
/// client joining on floor 12 gets the host's floor 12 without a byte of
/// geometry crossing the wire.
///
/// A separate RunSeed variable would be a second source of truth for "which
/// run is this". Phase 4 spent seven weeks undoing exactly that across 59
/// statics.
/// ====================================================================
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public class FloorDirector : MonoBehaviour
{
    public static FloorDirector Instance { get; private set; }

    [Header("Shape")]
    public int minRooms = 7;
    public int maxRooms = 14;
    public int minJunctions = 2;

    // ====================================================================
    // THE SHAFT'S GEOMETRY LIVES HERE, ONCE.
    //
    // These two numbers were duplicated in the editor preview, and the copies
    // drifted immediately: the preview passed Vector3.zero as the entrance, so
    // it planted every start room in the MIDDLE of the shaft and then
    // correctly rejected all ten floors with "the start room would sit inside
    // the shaft". The generator was fine. The preview was measuring a
    // different building.
    //
    // A preview that runs different numbers from the game is worse than no
    // preview, because it fails convincingly. So both now read these, and
    // ShaftKeepOutFor is the only implementation of the volume.
    // ====================================================================

    /// <summary>
    /// Where the lift's doorway is, in a LEVEL's local space.
    ///
    /// Grayboxbuilder puts the inner face of the shaft wall at x 7.5 with the
    /// opening centred on z 0. The start room's entrance door goes here.
    /// </summary>
    public static readonly Vector3 ShaftDoorwayLocal = new Vector3(7.5f, 0f, 0f);

    /// <summary>
    /// Half-extent of the volume no room may enter, in a LEVEL's local space.
    /// The shaft interior is 14 across, so 7.6 covers it and a little more - a
    /// room reaching inside is a player who cannot get out of the lift.
    /// </summary>
    public const float ShaftKeepOutHalf = 7.6f;

    /// <summary>
    /// The shaft, in WORLD space, built from the level's own frame.
    ///
    /// From the level's frame and not a fixed box, because Phase 1 rotates
    /// each level so its doorway faces a different way - a world-axis box
    /// would protect the wrong side of three floors in four.
    ///
    /// Eight corners through the transform, so a 90-degree rotation gives an
    /// exact box rather than an approximation.
    /// </summary>
    public static Bounds ShaftKeepOutFor(Transform level)
    {
        var b = new Bounds(level.TransformPoint(Vector3.up * 3f), Vector3.zero);

        for (int i = 0; i < 8; i++)
            b.Encapsulate(level.TransformPoint(new Vector3(
                (i & 1) == 0 ? -ShaftKeepOutHalf : ShaftKeepOutHalf,
                (i & 2) == 0 ? -2f : 8f,
                (i & 4) == 0 ? -ShaftKeepOutHalf : ShaftKeepOutHalf)));

        return b;
    }

    [Header("The lift's doorway, in LEVEL local space")]
    [Tooltip("Defaults to ShaftDoorwayLocal. Change it only for a shaft built " +
             "to different proportions - and change the preview with it.")]
    public Vector3 entranceLocal = ShaftDoorwayLocal;

    [Tooltip("Turn off the fixed graybox room on a level while a generated " +
             "floor is standing there. They occupy the same space, and the " +
             "generated floor is the one that replaced it.")]
    public bool hideFixedRoom = true;

    Transform shaft;
    FloorGenerator.Catalogue catalogue;

    // ====================================================================
    // EVERY UNLOCKED FLOOR IS ALIVE. NOT JUST THE ONE YOU ARE STANDING ON.
    //
    // This used to hold a single floor and destroy it the moment the lift
    // went somewhere else. That is indefensible in a four-player game: player
    // A is on floor 1, player B rides to floor 2, and floor 1 is deleted out
    // from under A.
    //
    // "Only the current floor exists" was the wrong optimisation. The right
    // one is "only floors that are actually unlocked exist" - twenty floors
    // of geometry is the thing worth avoiding, and a crew with 15m of cable
    // has three of them, not twenty.
    //
    // So geometry follows UNLOCK, not the lift:
    //
    //   LOCKED     -> nothing built
    //   UNLOCKED   -> built, and it stays built
    //   SEALED     -> destroyed, but only once nobody is standing on it
    //
    // Leaving a floor does nothing at all.
    // ====================================================================

    readonly Dictionary<int, GameObject> built = new Dictionary<int, GameObject>();

    // What the last reconcile ran against. Two ints, compared each frame, so
    // the work only happens when the progression actually moves - buying
    // cable, or a room being demolished.
    int lastReachable = -1;
    uint lastSealed = uint.MaxValue;
    bool reconciling;

    /// <summary>Floors that currently have geometry. For diagnostics.</summary>
    public int GeneratedCount => built.Count;

    // ====================================================================
    // THIS INSTALLS ITSELF. THERE IS NO SETUP STEP TO FORGET.
    //
    // It used to need a GameObject in the scene, and for a while it had none -
    // so Elevator called FloorDirector.Instance?.ShowFloor(), the null guard
    // swallowed it, and the entire floor system did nothing at all without one
    // error or warning anywhere. A required manual step that fails SILENTLY is
    // the worst kind.
    //
    // Same pattern AtmosphereBootstrap already uses for SceneAtmosphere:
    // RuntimeInitializeOnLoadMethod after the scene loads, make the object if
    // it is missing, DontDestroyOnLoad so a between-round scene reload does
    // not lose it. No new manager - this IS the floor lifecycle, it just stops
    // waiting to be placed.
    //
    // A hand-placed one still wins: Ensure only builds a copy when there is
    // none, so putting one in a scene to tune its fields keeps working.
    // ====================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // Domain reload can be off in the editor, and a stale Instance from
        // the previous Play session points at a destroyed object.
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ensure()
    {
        if (Instance != null) return;

        var existing = FindFirstObjectByType<FloorDirector>(FindObjectsInactive.Include);
        if (existing != null) { Instance = existing; return; }

        var go = new GameObject("FLOOR_DIRECTOR");
        DontDestroyOnLoad(go);
        go.AddComponent<FloorDirector>();
    }

    void Awake()
    {
        Instance = this;
        shaft = GameObject.Find("SHAFT")?.transform;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Why a floor may not be generated. Read from the state the game already
    /// keeps - NOT a second progression system.
    /// </summary>
    public enum Access
    {
        /// <summary>Reachable and standing. Generate it.</summary>
        Open,

        /// <summary>Past the cable. The lift will not travel here.</summary>
        BeyondCable,

        /// <summary>Demolished. Rubble, and nothing behind it.</summary>
        Sealed,

        /// <summary>Stripped bare and banked. Taped shut - there is nothing
        /// left to come back for, so it is never built again.</summary>
        Cleared
    }

    /// <summary>
    /// Whether floor <paramref name="floor"/> may be entered.
    ///
    /// ---- THE GAME ALREADY KNEW THIS. THIS ONLY ASKS. ----
    ///
    /// Both facts are existing, host-owned, replicated state:
    ///
    ///   Campaign.DeepestReachableFloor  = CableLength / FloorHeight
    ///   Campaign.DestroyedRooms         = the demolished set, sent as a bitmask
    ///
    /// ElevatorDashboard reads exactly these two to refuse a floor with
    /// "12 SEALED" or "12 BEYOND CABLE". Asking the same question here rather
    /// than inventing an unlock flag is the whole point: two sources of truth
    /// for "can we go there" is how a lift refuses a floor the generator has
    /// already built.
    /// </summary>
    public static Access AccessTo(int floor)
    {
        if (floor >= 1 && Campaign.DestroyedRooms.Contains(floor)) return Access.Sealed;

        // Checked after Sealed on purpose: a floor can be both finished AND
        // then demolished, and "it collapsed" is the more useful thing to say.
        if (floor >= 1 && Campaign.ClearedRooms.Contains(floor)) return Access.Cleared;
        if (floor >= 1 && floor > Campaign.DeepestReachableFloor) return Access.BeyondCable;

        return Access.Open;
    }

    // ------------------------------------------------------------------
    // THE RECONCILE LOOP
    // ------------------------------------------------------------------

    // ====================================================================
    // THE SCENE IS GONE, SO EVERYTHING I REMEMBER ABOUT IT IS A LIE
    //
    // This object survives the between-round reload on purpose
    // (DontDestroyOnLoad, above) - but the floors it built do not. The scene
    // that held them is destroyed, so every entry in `built` becomes a husk
    // and `shaft` points at nothing.
    //
    // That alone would be harmless. What was NOT harmless is lastReachable:
    // Update returns early unless the reachable floor or the sealed set has
    // CHANGED, and after a reload neither has. So round two began with the
    // director convinced it had already built everything, and built nothing.
    //
    // The symptom was exact: the floor you rode to appeared, because Elevator
    // calls ShowFloor directly and that checks for a real object rather than
    // a remembered one. Every floor you had unlocked but not yet visited
    // stayed empty, and the map showed nothing there. Reported after round 1
    // on 10 Sep: "floor 3 and 4 and 5 are not open there is no map there".
    //
    // Clear() already did the right thing and nothing ever called it. It is
    // safe here even though the objects are already destroyed - DestroyFloor
    // tests `container != null`, and Unity's overload reports a husk as null.
    // ====================================================================

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                       UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Clear();

        // Re-found on the next Generate. Cached from a scene that no longer
        // exists, so holding it would only look like it worked.
        shaft = null;
    }

    void Update()
    {
        // Two integer comparisons. Cheap enough to do every frame, and it
        // means nothing has to remember to TELL us that cable was bought or a
        // room was demolished - both are replicated state, so every machine
        // notices on its own and builds the same floors from the same seed.
        int reachable = Campaign.DeepestReachableFloor;
        uint sealed_ = Campaign.SealedMask();

        if (reachable == lastReachable && sealed_ == lastSealed) return;

        lastReachable = reachable;
        lastSealed = sealed_;

        if (!reconciling) StartCoroutine(Reconcile());
    }

    /// <summary>
    /// Make the geometry match the progression.
    ///
    /// One floor per frame. Unlocking floor 4 is one floor of work, but a
    /// fresh run needs three at once and a generous cable could want five -
    /// and fourteen rooms each, built in a single frame, is a visible hitch at
    /// exactly the moment the crew is looking at the shop.
    /// </summary>
    System.Collections.IEnumerator Reconcile()
    {
        reconciling = true;

        for (int floor = 1; floor <= Campaign.TotalFloors; floor++)
        {
            var access = AccessTo(floor);
            bool have = built.ContainsKey(floor) && built[floor] != null;

            if (access == Access.Open && !have)
            {
                Generate(floor);
                yield return null;          // one floor per frame
                continue;
            }

            if (access == Access.Sealed && have)
            {
                // ---- NEVER DELETE A FLOOR SOMEBODY IS STANDING ON ----
                //
                // A room seals on a timer, and the crew is not always out of
                // it - RunManager.SealRoomIndex exists precisely to work out
                // who got caught. Destroying the geometry underneath them
                // would drop them through the world instead.
                //
                // Left alone if anyone is there. The next reconcile tries
                // again, and by then the seal has resolved.
                if (AnyoneOn(floor)) continue;

                DestroyFloor(floor);
                yield return null;
            }

            // BeyondCable with geometry already built is left alone on
            // purpose. Cable only ever grows - nothing in Campaign reduces
            // CableLength - so this cannot normally happen; and if it ever
            // does, keeping a floor that somebody might be standing on is the
            // safe direction to be wrong in.
        }

        reconciling = false;
    }

    /// <summary>
    /// Make sure this floor exists. Called by the lift on arrival.
    ///
    /// It does NOT destroy anything and it does not touch other floors -
    /// arriving somewhere is not a reason to demolish where you came from.
    /// Normally the reconcile has already built this floor and there is
    /// nothing to do; this is the belt to that braces, for the case where a
    /// floor was unlocked and entered inside the same frame.
    /// </summary>
    public void ShowFloor(int floor)
    {
        if (built.ContainsKey(floor) && built[floor] != null) return;
        if (AccessTo(floor) != Access.Open) return;

        Generate(floor);
    }

    /// <summary>Build one floor. Returns false if it could not be placed.</summary>
    bool Generate(int floor)
    {
        if (shaft == null) shaft = GameObject.Find("SHAFT")?.transform;
        if (shaft == null) return false;

        var level = shaft.Find($"Level_{floor:00}");
        if (level == null) return false;

        if (catalogue == null) catalogue = LoadCatalogue();
        if (catalogue == null) return false;

        // The fixed graybox room and a generated floor occupy the same space.
        // Turned off rather than deleted, so nothing is lost if generation
        // fails and this has to fall back to the old floor.
        if (hideFixedRoom) SetFixedRoom(level, false);

        var container = new GameObject("GENERATED");
        container.transform.SetParent(level, false);

        var rooms = FloorGenerator.Build(
            container.transform, Mathf.Max(1, Campaign.RunNumber), floor,
            catalogue, entranceLocal, minRooms, maxRooms, minJunctions,
            Instantiate, out FloorGraph graph, out string failure,
            ShaftKeepOutFor(container.transform));

        if (rooms == null || rooms.Count == 0)
        {
            // Generation failed after every retry. The fixed room goes back on
            // rather than leaving the crew in an empty shaft - a floor that is
            // merely OLD beats a floor that is not there.
            Destroy(container);
            if (hideFixedRoom) SetFixedRoom(level, true);

            Debug.LogWarning($"[FloorDirector] floor {floor} would not generate " +
                             $"({failure}). Fell back to the fixed room.");
            return false;
        }

        // ====================================================================
        // ONE CALL, AND IT IS THE WHOLE OPTIMISATION
        //
        // A floor is about 250 objects, 170 colliders and 170 RENDERERS. The
        // renderers are what costs: three unlocked floors is ~500 draw calls
        // of static grey boxes, and a fully unlocked run is ~3,400.
        //
        // Objects and colliders are comparatively free here - these are static
        // colliders that never move, which PhysX sleeps on. Draw calls are not.
        //
        // StaticBatchingUtility.Combine merges the meshes under this floor into
        // one, so the floor draws in a handful of calls instead of a hundred
        // and seventy. It is the standard answer for procedurally generated
        // level geometry, it is one line, and it costs nothing at runtime
        // beyond the combine itself.
        //
        // It is safe here for one reason: NOTHING IN A FLOOR EVER MOVES. Rooms
        // are placed once and never again. The moment something in here needs
        // to move - a sliding door, a collapsing ceiling - it must be excluded
        // from the combine or it will not move on screen.
        //
        // Grayboxbuilder already marks its shaft static for the same reason.
        // The generated rooms simply never got the equivalent, because they
        // are built at runtime and cannot be flagged in the editor.
        //
        // The locked door is combined too, and that is fine: toggling a
        // renderer's enabled flag still works on batched geometry - it is
        // MOVING one that does not.
        // ---- THE FLOOR ONLY NOW HAS ANYWHERE TO PUT LOOT ----
        //
        // LootSpawner allots every floor its money at Start, but it cannot
        // place a crate until there are rooms with loot sockets in them - and
        // at Start there are none, because that runs before the first Update
        // and this generates in Update.
        //
        // NOT THIS FRAME, THOUGH. See StockWhenTheGhostsAreGone.
        StartCoroutine(StockWhenTheGhostsAreGone(level, floor));

        built[floor] = container;
        return true;
    }

    // ====================================================================
    // WAIT ONE FRAME BEFORE PUTTING LOOT ON A FLOOR
    //
    // FloorGenerator backtracks - up to 24 attempts - and discards the rooms
    // of a failed attempt with Object.Destroy, which does NOT destroy
    // anything now. It marks the object and Unity deletes it at the END OF
    // THE FRAME.
    //
    // So for the rest of this frame the floor contains ghosts: rooms that
    // were rejected, still in the hierarchy, still carrying loot sockets, and
    // still perfectly findable by GetComponentsInChildren. Stocking now can
    // stand a crate on a floor that is about to stop existing.
    //
    // That is exactly what happened on 10 Sep. One crate of nine fell 5.55m -
    // dead vertical, zero lateral, which is the signature of spawning into
    // thin air rather than rolling off an edge. It fell past floors 1 and 2
    // and came to rest on floor 3's ceiling slab, whose top face is at -10.5.
    //
    // One frame is all it takes: the deferred destroys flush, the ghosts are
    // gone, and SocketsUnder only sees rooms that actually survived.
    //
    // The floor can be torn down again before this resumes - a seal, or the
    // crew leaving - so everything is re-checked rather than assumed.
    // ====================================================================

    System.Collections.IEnumerator StockWhenTheGhostsAreGone(Transform level, int floor)
    {
        yield return null;

        if (level == null) yield break;                  // floor went away
        if (!built.TryGetValue(floor, out var still) || still == null) yield break;

        // Batched HERE rather than the moment the floor was built, for the
        // same reason the loot waits. StaticBatchingUtility.Combine bakes
        // whatever it finds into one mesh - and a frame ago that included
        // every room the generator had rejected. Their renderers die at the
        // end of the frame, but their vertices would have stayed in the
        // combined mesh for the life of the floor: never drawn, always
        // carried. Combining after the ghosts are gone bakes only the rooms
        // that survived.
        BatchFloorGeometry(still);

        var loot = SceneRefs.Loot;
        if (loot != null) loot.StockFloorIfPending(level, floor);
    }

    /// <summary>
    /// Destroy one floor's geometry and put its fixed room back.
    ///
    /// Destroyed, not hidden. Hidden objects still cost colliders, physics and
    /// memory, which is the entire reason for doing this at all. What survives
    /// is the floor NUMBER and the campaign's sealed set - a few bits - and
    /// the layout can always be recomputed from (RunNumber, floor).
    /// </summary>
    void DestroyFloor(int floor)
    {
        if (!built.TryGetValue(floor, out var container)) return;

        if (container != null)
        {
            var level = container.transform.parent;
            Destroy(container);

            // The fixed room comes back so the doorway still leads somewhere,
            // and RunManager's rubble is already in front of it.
            if (hideFixedRoom && level != null) SetFixedRoom(level, true);
        }

        built.Remove(floor);
    }

    /// <summary>Tear every generated floor down. Between runs, not during.</summary>
    public void Clear()
    {
        foreach (var floor in new List<int>(built.Keys)) DestroyFloor(floor);

        built.Clear();
        lastReachable = -1;
        lastSealed = uint.MaxValue;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Is anybody standing on this floor?
    ///
    /// Worked out from HEIGHT rather than from anything the player carries,
    /// because the levels are stacked at a fixed pitch and height is the one
    /// fact that is true on every machine without being sent. The lift owns
    /// those two numbers, so they are read from it rather than copied.
    /// </summary>
    bool AnyoneOn(int floor)
    {
        var lift = SceneRefs.Lift;
        if (lift == null) return false;             // no lift, no floors, no risk

        foreach (var p in PlayerRegistry.All)
        {
            if (p == null) continue;

            int on = Mathf.RoundToInt((lift.surfaceY - p.transform.position.y)
                                      / Mathf.Max(0.01f, lift.floorHeight));

            if (on == floor) return true;
        }

        return false;
    }

    /// <summary>
    /// Turn the fixed graybox ROOM on or off, leaving the shaft alone.
    ///
    /// ---- ONLY THE ROOM. NEVER THE SHAFT. ----
    ///
    /// This used to disable every child that was not the GENERATED container,
    /// on the reasoning that a name check is fragile. It is much worse than
    /// fragile here: a Level_NN holds the shaft walls AND the fixed room
    /// together -
    ///
    ///     Wall_West, Wall_North, Wall_South,          <- the shaft
    ///     Wall_East_Left, Wall_East_Right, Wall_East_Above   <- and its doorway
    ///     Room_Floor, Room_Ceiling, Room_BackWall,    <- the fixed room
    ///     Room_Wall_North, Room_Wall_South
    ///
    /// so "disable everything else" removed the shaft walls and the doorway
    /// frame at whichever floor you were standing on. The lift would have
    /// opened onto a hole.
    ///
    /// Grayboxbuilder names every room piece "Room_" and every shaft piece
    /// "Wall_", consistently, and it is the only thing that builds these - so
    /// the prefix is a contract with one author rather than a guess.
    /// </summary>
    // ====================================================================
    // COMBINE THE FLOOR, BUT NOT THE THINGS THAT MOVE
    //
    // Static batching bakes each mesh's vertices into one big mesh IN WORLD
    // SPACE. After that the transform no longer drives what you see: move the
    // object and the physics body walks away while its picture stays welded
    // where it was built.
    //
    // Which is exactly what the floor's KEY does. FloorLocks parents it inside
    // the room it spawned in, and it has a Rigidbody because you pick it up.
    // Combining it would give an invisible key on the floor that still had
    // collision - no error, no warning, and no way to guess the cause.
    //
    // So: anything with a Rigidbody steps outside for the length of the
    // combine and comes straight back. A Rigidbody is the honest test for
    // "this is meant to move" - it is what makes movement possible in the
    // first place - and there is normally exactly one of them per floor.
    //
    // The locked door has none, and needs none: it opens by switching its
    // renderer and collider off, and disabling a batched renderer works
    // perfectly. It is only MOVING that batching forbids.
    // ====================================================================

    static void BatchFloorGeometry(GameObject container)
    {
        var movers = container.GetComponentsInChildren<Rigidbody>(true);
        var homes = new Transform[movers.Length];

        for (int i = 0; i < movers.Length; i++)
        {
            homes[i] = movers[i].transform.parent;
            movers[i].transform.SetParent(null, true);   // keep world position
        }

        StaticBatchingUtility.Combine(container);

        // Re-parenting after the combine is safe: the batch was built from
        // what was present, and these were not. They keep their own meshes.
        for (int i = 0; i < movers.Length; i++)
            movers[i].transform.SetParent(homes[i], true);
    }

    void SetFixedRoom(Transform level, bool on)
    {
        foreach (Transform child in level)
        {
            if (child == null) continue;
            if (!child.name.StartsWith("Room_")) continue;

            child.gameObject.SetActive(on);
        }
    }

    /// <summary>
    /// The room set, from Resources so the runtime and the editor preview load
    /// the identical prefabs.
    ///
    /// Door counts are COUNTED off each prefab rather than read from its name
    /// or folder - a room filed as a 3-door that carries two would break the
    /// graph's degree invariant somewhere much further down.
    /// </summary>
    static FloorGenerator.Catalogue LoadCatalogue()
    {
        var all = Resources.LoadAll<GameObject>("Rooms");

        if (all == null || all.Length == 0)
        {
            Debug.LogError("[FloorDirector] no room prefabs in Resources/Rooms. " +
                           "Run Tools > Rooms > Build Room Set.");
            return null;
        }

        var buckets = new List<GameObject>[5];
        for (int i = 0; i < 5; i++) buckets[i] = new List<GameObject>();

        foreach (var go in all)
        {
            if (go == null || go.GetComponent<RoomModule>() == null) continue;

            int doors = RoomExit.DoorsUnder(go.transform).Length;
            if (doors >= 1 && doors <= 4) buckets[doors].Add(go);
        }

        if (buckets[1].Count == 0)
        {
            Debug.LogError("[FloorDirector] no 1-door room. Without a dead end no " +
                           "branch can terminate, so no floor can ever be valid.");
            return null;
        }

        var cat = new FloorGenerator.Catalogue();
        for (int d = 1; d <= 4; d++) cat.byDoorCount[d] = buckets[d].ToArray();

        return cat;
    }
}
