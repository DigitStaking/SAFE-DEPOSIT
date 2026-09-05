using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places the rooms a FloorGraph asked for, by matching door to door.
///
/// ====================================================================
/// ORDER OF OPERATIONS, AND WHY IT IS THIS WAY ROUND
///
///   1. FloorGraph decides the tree      - pure numbers, no geometry
///   2. this file places it              - door socket onto door socket
///   3. FloorValidator checks it         - and rejects, it does not repair
///   4. on failure, a new seed and retry - never a patched-up floor
///
/// The previous generator did none of that. It grew geometry room by room,
/// and when something would not fit it SEALED THE DOOR - so a four-door
/// crossroads quietly became a corridor with two walls in it, and there was
/// no graph anywhere for anything to check against. Every seal was a failure
/// being hidden rather than retried.
///
/// Nothing here ever seals, blocks, closes or shortens. A room either lands
/// with all its doors satisfiable or it is destroyed and something else is
/// tried. If the whole floor cannot be placed, the floor is thrown away and
/// regenerated from a different seed.
///
/// THE CONNECTION
///
/// A door's +X points OUT of its room. To join child door C to parent door P,
/// C must sit exactly on P and face back along it:
///
///     want     = P.rotation * yaw180
///     childRot = want * inverse(C.localRotation)
///     childPos = P.position - childRot * C.localPosition
///
/// That is the entire placement maths, and it holds for any two rooms at any
/// rotation because neither needs to know the other's shape. Positioning by
/// pivots or bounds instead is what produces the gap you can fall through.
/// ====================================================================
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public class FloorGenerator : MonoBehaviour
{
    [Header("Rooms, by how many doors they have")]
    public GameObject[] oneDoor;
    public GameObject[] twoDoor;
    public GameObject[] threeDoor;
    public GameObject[] fourDoor;

    [Header("Size")]
    public int minRooms = 7;
    public int maxRooms = 14;
    public int minJunctions = 2;

    [Header("Where the floor starts")]
    [Tooltip("The lift's doorway, in the LEVEL's local space. The start " +
             "room's entrance door is placed here - it is the ONLY opening " +
             "to the outside on the whole floor.")]
    public Vector3 entranceLocal = new Vector3(7.5f, 0f, 0f);

    /// <summary>Everything one attempt needs, so Build stays a function and
    /// the editor preview and the runtime take the identical path.</summary>
    public class Catalogue
    {
        public GameObject[][] byDoorCount = new GameObject[5][];

        public HashSet<int> Available()
        {
            var set = new HashSet<int>();

            for (int d = 1; d <= 4; d++)
                if (byDoorCount[d] != null && byDoorCount[d].Length > 0) set.Add(d);

            return set;
        }
    }

    /// <summary>
    /// Build a whole floor, or return null.
    ///
    /// Null means "this seed does not work here" and the caller should try
    /// another. It never means a floor with something wrong in it - which is
    /// the distinction the old generator did not make.
    /// </summary>
    public static List<RoomModule> Build(Transform level, int runNumber, int floor,
                                         Catalogue cat, Vector3 entranceLocal,
                                         int minRooms, int maxRooms, int minJunctions,
                                         System.Func<GameObject, GameObject> spawn,
                                         out FloorGraph graph, out string failure)
    {
        graph = null;
        failure = null;

        var available = cat.Available();

        if (!available.Contains(1))
        {
            failure = "no 1-door room in the set - without a dead end no branch " +
                      "can ever be terminated, so every floor would leak doors";
            return null;
        }

        // Several attempts, each with its own seed. A rejection is cheap and a
        // wrong floor is not, so retrying is always the better trade.
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int seed = FloorLayout.SeedFor(runNumber, floor, attempt);
            var rng = new System.Random(seed);

            var g = FloorGraph.Build(rng, minRooms, maxRooms, minJunctions, available);

            var graphFaults = g.Problems(minRooms, minJunctions);
            if (graphFaults.Count > 0)
            {
                failure = "graph: " + graphFaults[0];
                continue;
            }

            var rooms = Place(level, g, cat, entranceLocal, rng, spawn, out string why);

            if (rooms != null)
            {
                graph = g;
                failure = null;
                return rooms;
            }

            failure = why;
        }

        return null;
    }

    // ------------------------------------------------------------------

    static List<RoomModule> Place(Transform level, FloorGraph g, Catalogue cat,
                                  Vector3 entranceLocal, System.Random rng,
                                  System.Func<GameObject, GameObject> spawn,
                                  out string failure)
    {
        failure = null;

        var built = new List<RoomModule>();
        var bounds = new List<Bounds>();
        var doorsOf = new Dictionary<int, List<RoomExit>>();

        // ---- THE START ROOM ----
        var rootPrefab = PickPrefab(cat, g.nodes[0].doors, rng);
        var root = Spawn(spawn, rootPrefab, level);

        if (root == null) { failure = "no prefab for the start room"; return null; }

        var rootDoors = new List<RoomExit>(RoomExit.DoorsUnder(root.transform));

        if (rootDoors.Count != g.nodes[0].doors)
        {
            failure = "start room prefab has the wrong number of doors";
            Cleanup(built);
            Discard(root.gameObject);
            return null;
        }

        // One of its doors becomes THE entrance - the single opening to the
        // outside on this floor - and is placed at the lift's doorway. The
        // rest owe children, which is exactly the graph's root degree.
        var entrance = rootDoors[rng.Next(rootDoors.Count)];
        entrance.isEntrance = true;

        // Placed so the entrance door lands on the lift's doorway, facing back
        // out of the floor.
        Quaternion want = Quaternion.Euler(0f, 180f, 0f);
        root.transform.localRotation =
            want * Quaternion.Inverse(entrance.transform.localRotation);
        root.transform.localPosition =
            entranceLocal - root.transform.localRotation * entrance.transform.localPosition;

        built.Add(root);
        bounds.Add(Footprint(root));
        doorsOf[0] = rootDoors;

        // ---- EVERY EDGE OF THE TREE, BREADTH FIRST ----
        var queue = new Queue<int>();
        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            int parentId = queue.Dequeue();
            var parentDoors = doorsOf[parentId];

            foreach (int childId in g.nodes[parentId].children)
            {
                RoomExit parentDoor = FirstFree(parentDoors);

                if (parentDoor == null)
                {
                    failure = $"room {parentId} ran out of doors - the graph and " +
                              "the prefab disagree about how many it has";
                    Cleanup(built);
                    return null;
                }

                var child = AttachChild(g.nodes[childId].doors, parentDoor, cat,
                                        level, bounds, rng, spawn,
                                        out List<RoomExit> childDoors);

                if (child == null)
                {
                    // No prefab, no door on it, and no orientation fits here
                    // without overlapping. That is a real failure and the
                    // floor is abandoned - NOT a door to be walled up.
                    failure = $"room {childId} ({g.nodes[childId].doors} doors) " +
                              $"would not fit onto room {parentId}";
                    Cleanup(built);
                    return null;
                }

                built.Add(child);
                bounds.Add(Footprint(child));
                doorsOf[childId] = childDoors;
                queue.Enqueue(childId);
            }
        }

        return built;
    }

    /// <summary>
    /// Try every room of the right door count, and every door on each, until
    /// one lands without overlapping.
    ///
    /// This is the backtracking the old generator did not have. Its only
    /// recovery from a bad fit was to seal a door; here a bad fit costs an
    /// Instantiate and a destroy, and the search moves on.
    /// </summary>
    static RoomModule AttachChild(int doorCount, RoomExit parentDoor, Catalogue cat,
                                  Transform level, List<Bounds> bounds,
                                  System.Random rng,
                                  System.Func<GameObject, GameObject> spawn,
                                  out List<RoomExit> childDoors)
    {
        childDoors = null;

        var options = cat.byDoorCount[doorCount];
        if (options == null || options.Length == 0) return null;

        // Shuffled, so a floor does not always reach for the same prefab and
        // so a retry genuinely tries something else.
        var order = new List<int>();
        for (int i = 0; i < options.Length; i++) order.Add(i);

        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int swap = order[i]; order[i] = order[j]; order[j] = swap;
        }

        foreach (int idx in order)
        {
            var room = Spawn(spawn, options[idx], level);
            if (room == null) continue;

            var doors = new List<RoomExit>(RoomExit.DoorsUnder(room.transform));

            if (doors.Count != doorCount)
            {
                // The prefab disagrees with the catalogue it was filed under.
                // Skipped rather than used: a room with the wrong number of
                // doors cannot satisfy the graph by definition.
                Discard(room.gameObject);
                continue;
            }

            foreach (var mine in doors)
            {
                Connect(room.transform, mine, parentDoor);

                Bounds b = Footprint(room);
                bool clash = false;

                foreach (var other in bounds)
                    if (other.Intersects(b)) { clash = true; break; }

                if (clash) continue;

                RoomExit.Join(parentDoor, mine);
                childDoors = doors;
                return room;
            }

            Discard(room.gameObject);
        }

        return null;
    }

    /// <summary>
    /// Put <paramref name="mine"/> exactly onto <paramref name="target"/>,
    /// facing back along it. The whole of the placement maths.
    /// </summary>
    static void Connect(Transform room, RoomExit mine, RoomExit target)
    {
        Quaternion want = target.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

        room.rotation = want * Quaternion.Inverse(mine.transform.localRotation);
        room.position = target.transform.position -
                        room.rotation * mine.transform.localPosition;
    }

    static RoomExit FirstFree(List<RoomExit> doors)
    {
        foreach (var d in doors)
            if (d != null && !d.connected && !d.isEntrance) return d;

        return null;
    }

    static GameObject PickPrefab(Catalogue cat, int doorCount, System.Random rng)
    {
        var options = cat.byDoorCount[doorCount];
        return options == null || options.Length == 0
            ? null
            : options[rng.Next(options.Length)];
    }

    static RoomModule Spawn(System.Func<GameObject, GameObject> spawn,
                            GameObject prefab, Transform level)
    {
        if (prefab == null || spawn == null) return null;

        var go = spawn(prefab);
        if (go == null) return null;

        // 'false' - do NOT keep world position. Letting Unity rewrite the
        // transform to preserve where the instance happened to appear is the
        // commonest cause of a room ending up somewhere else entirely.
        go.transform.SetParent(level, false);
        return go.GetComponent<RoomModule>();
    }

    /// <summary>
    /// The room's footprint, pulled in so two rooms sharing a doorway wall are
    /// not read as overlapping. Shared walls touch by design; only a real
    /// overlap counts.
    /// </summary>
    static Bounds Footprint(RoomModule room)
    {
        var b = new Bounds(room.transform.position, Vector3.one * 0.1f);
        bool any = false;

        foreach (var r in room.GetComponentsInChildren<MeshRenderer>())
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        b.Expand(new Vector3(-1.4f, 0f, -1.4f));
        return b;
    }

    static void Cleanup(List<RoomModule> built)
    {
        foreach (var r in built)
            if (r != null) Discard(r.gameObject);

        built.Clear();
    }

    /// <summary>Works in play mode and in the editor preview, which run the
    /// same Build for the same reason a preview must not lie.</summary>
    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }
}
