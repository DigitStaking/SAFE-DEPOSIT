using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arranges modules into a floor: landing, then main, then the optional side
/// and back rooms hanging off it.
///
/// ====================================================================
/// IT WALKS EXITS. IT DOES NOT COMPUTE POSITIONS.
///
/// Every module is authored with its origin at its own doorway, +X into the
/// room, and an exit is that same frame pointing outward. So attaching B to
/// an exit of A is:
///
///     b.SetPositionAndRotation(exit.Position, exit.Rotation)
///
/// and that is the entire placement maths in this file. No offsets, no per-
/// module table of which room fits which, no arithmetic that has to be kept
/// in step with the prefabs. Change a module's shape in RoomModuleBuilder and
/// this keeps working, because it never knew the shape.
///
/// That is what the frame decision in Step 5 bought, and it is why the frame
/// was worth being fussy about.
///
/// THE DOOR SIDE IS NOT DECIDED HERE.
///
/// Phase 1 already rotates each Level_NN so its doorway faces a different
/// way, and PHASE5_SPEC says the generator must respect that rather than
/// re-decide it. Modules are parented to the level and placed in LOCAL space,
/// so the rotation is inherited and this file never learns which way the
/// floor faces. Two systems deciding one thing is the split this project
/// keeps having to undo.
/// ====================================================================
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public class FloorGenerator : MonoBehaviour
{
    [Header("Modules")]
    public GameObject[] landings;
    public GameObject[] mains;
    public GameObject[] sides;
    public GameObject[] backs;

    [Header("Where the landing attaches")]
    [Tooltip("The level's own doorway, in the LEVEL's local space. " +
             "Grayboxbuilder puts the inner face of the shaft wall at x 7.5 " +
             "with the opening centred on z 0, so this is that spot - the " +
             "landing's origin goes here and everything else follows from its " +
             "exits.")]
    public Vector3 doorwayLocal = new Vector3(7.5f, 0f, 0f);

    /// <summary>
    /// Build one floor under <paramref name="level"/>, and return the rooms
    /// in the order a player walks them.
    ///
    /// Static and given everything it needs, so the editor preview and the
    /// runtime build take the same path. A preview that runs different code
    /// from the game is a preview that lies.
    /// </summary>
    public static List<RoomModule> Build(Transform level, int runNumber, int floor,
                                         GameObject[] landings, GameObject[] mains,
                                         GameObject[] sides, GameObject[] backs,
                                         Vector3 doorwayLocal,
                                         System.Func<GameObject, GameObject> spawn)
    {
        var built = new List<RoomModule>();
        if (level == null || landings == null || landings.Length == 0) return built;

        var layout = FloorLayout.For(runNumber, floor,
                                     landings.Length,
                                     mains != null ? mains.Length : 0);

        var rng = new System.Random(layout.seed);
        var taken = new List<Bounds>();

        // ---- THE LANDING, AT THE LEVEL'S OWN DOORWAY ----
        //
        // Local position and identity local rotation: the level is already
        // turned to face whichever way Phase 1 decided, and inheriting that is
        // how this respects it without knowing it.
        var landing = Place(spawn, landings[layout.landing], level);
        if (landing == null) return built;

        landing.transform.localPosition = doorwayLocal;
        landing.transform.localRotation = Quaternion.identity;
        built.Add(landing);
        taken.Add(FootprintOf(landing));

        // ================================================================
        // GROW A TREE, NOT A LINE
        //
        // The first version walked a spine - landing, main, main, main, with
        // side rooms hanging off - so every floor was a corridor and "which
        // way do we go" had the same answer on every floor of the building.
        //
        // A main offers left, right and straight on. Spending the budget
        // across those makes a floor branch, and branching is the whole reason
        // a crew splits up - which is the reason they have to talk to each
        // other, which is what the voice work in Phase 4 was for. A corridor
        // needs no radio.
        //
        // Breadth-first on purpose: depth-first spends the whole budget down
        // one arm and produces a long thin floor with a stub on it, which is a
        // corridor again with extra steps.
        // ================================================================

        var frontier = new Queue<RoomModule>();
        frontier.Enqueue(landing);

        while (frontier.Count > 0 && built.Count < layout.roomBudget)
        {
            var room = frontier.Dequeue();

            foreach (var exit in RoomExit.ExitsUnder(room.transform))
            {
                if (built.Count >= layout.roomBudget) break;
                if (exit == null || exit.used) continue;

                // Not every door leads somewhere. A floor where every opening
                // is filled is as predictable as one where none are - and an
                // unused door is sealed below, so it reads as a wall rather
                // than as a promise nobody kept.
                if (rng.NextDouble() < 0.2) continue;

                // A main CONTINUES the tree; a side or a back room ends it.
                // Leaves are what stop a floor being an endless branch, and
                // they are where the survivor and the best loot live.
                bool branch = rng.NextDouble() < 0.55
                              && mains != null && mains.Length > 0;

                GameObject prefab = branch
                    ? mains[room == landing ? layout.firstMain : rng.Next(mains.Length)]
                    : PickLeaf(rng, sides, backs);

                if (prefab == null) continue;

                var placed = TryAttach(spawn, prefab, exit, level, taken);
                if (placed == null) continue;

                built.Add(placed);
                if (branch) frontier.Enqueue(placed);
            }
        }

        SealUnusedExits(built);
        return built;
    }

    static GameObject PickLeaf(System.Random rng, GameObject[] sides, GameObject[] backs)
    {
        bool hasSide = sides != null && sides.Length > 0;
        bool hasBack = backs != null && backs.Length > 0;

        if (hasSide && hasBack)
            return rng.NextDouble() < 0.5 ? sides[rng.Next(sides.Length)]
                                          : backs[rng.Next(backs.Length)];

        if (hasSide) return sides[rng.Next(sides.Length)];
        if (hasBack) return backs[rng.Next(backs.Length)];
        return null;
    }

    /// <summary>
    /// Put a room on an exit, unless it would land on top of one already
    /// there.
    ///
    /// ---- BRANCHING NEEDS THIS AND A SPINE DID NOT ----
    ///
    /// A line of rooms cannot collide with itself. A tree can: two arms that
    /// turn toward each other meet, and the result is two rooms in the same
    /// space with their walls interleaved - which looks like a rendering bug
    /// and plays like a trap.
    ///
    /// So the room is placed, measured, and removed again if it overlaps.
    /// Placing first is not laziness: a module's footprint depends on its
    /// rotation, and the honest way to know where it lands is to put it there.
    /// The exit is left unused and gets sealed, so a rejected branch becomes a
    /// wall rather than a hole.
    /// </summary>
    static RoomModule TryAttach(System.Func<GameObject, GameObject> spawn,
                                GameObject prefab, RoomExit exit,
                                Transform level, List<Bounds> taken)
    {
        var room = Place(spawn, prefab, level);
        if (room == null) return null;

        // The whole of the placement maths. See the header.
        room.transform.SetPositionAndRotation(exit.Position, exit.Rotation);

        Bounds b = FootprintOf(room);

        foreach (var other in taken)
        {
            if (!other.Intersects(b)) continue;

            Discard(room.gameObject);
            return null;
        }

        taken.Add(b);
        exit.used = true;
        return room;
    }

    /// <summary>
    /// The room's footprint, pulled in so two rooms sharing a wall are not
    /// read as overlapping.
    ///
    /// Renderer bounds rather than colliders: they are already world space,
    /// and a module's walls are exactly what defines where it is. The inset is
    /// a little more than a wall thickness, because shared walls touch by
    /// design and only a real overlap should count.
    /// </summary>
    static Bounds FootprintOf(RoomModule room)
    {
        var renderers = room.GetComponentsInChildren<MeshRenderer>();

        var b = new Bounds(room.transform.position, Vector3.one * 0.1f);
        bool any = false;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        b.Expand(new Vector3(-1.2f, 0f, -1.2f));
        return b;
    }

    /// <summary>Works in play mode and in the editor preview, which run the
    /// same Build for the same reason a preview must not lie.</summary>
    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }

    // ====================================================================
    // A DOOR THAT LEADS NOWHERE IS WORSE THAN NO DOOR
    //
    // Every module is authored with openings for every exit it declares, and
    // the generator only fills some of them - the spine ends somewhere, and a
    // main whose side room was not rolled still has a side doorway cut into
    // its wall. Left alone, those are holes onto the skybox: a floor that ends
    // in a doorway reads as unfinished, and worse, it reads as a route. A
    // player walks to it, finds nothing, and stops trusting doors.
    //
    // A dead end has to LOOK like a dead end. So every exit nothing was
    // attached to gets its opening filled back in, which also makes Step 5's
    // "walkable with every exit sealed" rule automatic instead of a thing the
    // human has to remember.
    //
    // Filled rather than never cut, deliberately: the module cannot know
    // which of its doors a floor will use, so cutting them all and closing the
    // spares is the only order that lets one prefab serve both.
    // ====================================================================

    const float DoorWidth = 2f;
    const float DoorHeight = 2.5f;
    const float WallThick = 0.5f;

    static void SealUnusedExits(List<RoomModule> rooms)
    {
        foreach (var room in rooms)
        {
            if (room == null) continue;

            foreach (var exit in RoomExit.ExitsUnder(room.transform))
            {
                if (exit == null || exit.used) continue;

                var plug = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plug.name = "Sealed";
                plug.transform.SetParent(exit.transform, false);

                // The exit's own frame: +X points out through the opening, so
                // the plug sits half a wall along it and fills the 2 x 2.5
                // hole exactly.
                plug.transform.localPosition =
                    new Vector3(WallThick * 0.5f, DoorHeight * 0.5f, 0f);
                plug.transform.localRotation = Quaternion.identity;
                plug.transform.localScale =
                    new Vector3(WallThick, DoorHeight, DoorWidth);

                // Matched to whatever the room is made of, so a sealed door
                // does not announce itself as a different kind of object.
                var source = room.GetComponentInChildren<MeshRenderer>();
                if (source != null)
                    plug.GetComponent<MeshRenderer>().sharedMaterial = source.sharedMaterial;
            }
        }
    }

    static RoomModule Place(System.Func<GameObject, GameObject> spawn,
                            GameObject prefab, Transform level)
    {
        if (prefab == null || spawn == null) return null;

        var go = spawn(prefab);
        if (go == null) return null;

        // Parented WITHOUT keeping world position - the caller sets local or
        // world coordinates immediately afterwards, and letting Unity rewrite
        // them to preserve where the instance happened to appear is the
        // single most common cause of a room ending up somewhere else.
        go.transform.SetParent(level, false);

        return go.GetComponent<RoomModule>();
    }
}
