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

    [Header("The lift's doorway, in LEVEL local space")]
    [Tooltip("Grayboxbuilder puts the inner face of the shaft wall at x 7.5 " +
             "with the opening centred on z 0. The start room's entrance door " +
             "is placed here.")]
    public Vector3 entranceLocal = new Vector3(7.5f, 0f, 0f);

    [Tooltip("Half-extent of the volume no room may enter, in LEVEL local " +
             "space. The shaft interior is 14 across, so 7.6 covers it plus a " +
             "little - a room that reaches inside is a player who cannot get " +
             "out of the lift.")]
    public float shaftKeepOut = 7.6f;

    [Tooltip("Turn off the fixed graybox room on a level while a generated " +
             "floor is standing there. They occupy the same space, and the " +
             "generated floor is the one that replaced it.")]
    public bool hideFixedRoom = true;

    Transform shaft;
    GameObject current;
    int currentFloor = int.MinValue;

    FloorGenerator.Catalogue catalogue;

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
    /// Make sure the floor standing in the scene is the one for
    /// <paramref name="floor"/>. Cheap and idempotent - call it whenever the
    /// lift arrives; it does nothing if the right floor is already up.
    /// </summary>
    public void ShowFloor(int floor)
    {
        if (floor == currentFloor && current != null) return;

        Clear();

        if (shaft == null) shaft = GameObject.Find("SHAFT")?.transform;
        if (shaft == null) return;

        var level = shaft.Find($"Level_{floor:00}");
        if (level == null) return;

        if (catalogue == null) catalogue = LoadCatalogue();
        if (catalogue == null) return;

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
            KeepOut(container.transform));

        if (rooms == null || rooms.Count == 0)
        {
            // Generation failed after every retry. The fixed room goes back on
            // rather than leaving the player in an empty shaft - a floor that
            // is merely OLD beats a floor that is not there.
            Destroy(container);
            if (hideFixedRoom) SetFixedRoom(level, true);

            Debug.LogWarning($"[FloorDirector] floor {floor} would not generate " +
                             $"({failure}). Fell back to the fixed room.");
            return;
        }

        current = container;
        currentFloor = floor;
    }

    /// <summary>Tear the current floor down. Destroyed, not hidden - hidden
    /// objects still cost colliders, physics and memory.</summary>
    public void Clear()
    {
        if (current != null)
        {
            var level = current.transform.parent;
            Destroy(current);
            current = null;

            if (hideFixedRoom && level != null) SetFixedRoom(level, true);
        }

        currentFloor = int.MinValue;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// The volume no room may enter, in world space.
    ///
    /// Built from the LEVEL's frame, so it turns with the floor - Phase 1
    /// rotates each level so its doorway faces a different way, and a keep-out
    /// box that ignored that would protect the wrong side of three floors in
    /// four.
    /// </summary>
    Bounds KeepOut(Transform level)
    {
        var b = new Bounds(level.TransformPoint(Vector3.up * 3f), Vector3.zero);

        // Eight corners through the level's transform, so a 90-degree rotation
        // gives an exact box rather than an approximation.
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? -shaftKeepOut : shaftKeepOut,
                (i & 2) == 0 ? -2f : 8f,
                (i & 4) == 0 ? -shaftKeepOut : shaftKeepOut);

            b.Encapsulate(level.TransformPoint(corner));
        }

        return b;
    }

    /// <summary>
    /// Everything the level had before a floor was generated into it.
    ///
    /// Found by "not the GENERATED container" rather than by name, so it keeps
    /// working whatever Grayboxbuilder decides to call its pieces.
    /// </summary>
    void SetFixedRoom(Transform level, bool on)
    {
        foreach (Transform child in level)
        {
            if (child == null) continue;
            if (child.name == "GENERATED") continue;
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
