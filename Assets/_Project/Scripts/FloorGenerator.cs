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

        // ---- THE SPINE ----
        //
        // Each main hangs off the previous room's onward exit - the landing's
        // only one, then each main's "back" door. That is what makes a floor
        // long or short: the same attach step, repeated as many times as the
        // layout asked for.
        //
        // A room that has no free exit ends the spine early. That is a
        // shorter floor and not a failure: every module must be walkable with
        // its exits unused, so the floor simply stops here.
        RoomModule current = landing;

        for (int i = 0; i < layout.mains.Length; i++)
        {
            if (mains == null || mains.Length == 0) break;

            string onward = current == landing ? "" : "back";
            var main = Attach(spawn, mains[layout.mains[i]], current, level, onward);
            if (main == null) break;

            built.Add(main);

            // A side room off this one, if the layout said so. It branches and
            // does not continue the spine, which is what makes it optional in
            // the sense that matters: you can walk past it.
            if (layout.sideAt[i] && sides != null && sides.Length > 0)
            {
                var side = Attach(spawn, sides[0], main, level, "side");
                if (side != null) built.Add(side);
            }

            current = main;
        }

        // ---- THE DEAD END ----
        if (layout.hasBack && backs != null && backs.Length > 0 && current != landing)
        {
            var back = Attach(spawn, backs[0], current, level, "back");
            if (back != null) built.Add(back);
        }

        SealUnusedExits(built);
        return built;
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

    /// <summary>
    /// Put <paramref name="prefab"/> on a free exit of <paramref name="from"/>.
    ///
    /// Returns null when there is no such exit, which the caller treats as
    /// "this floor does not have that room" rather than as an error. A module
    /// must be walkable with its exits unused, so a floor that ran out of
    /// doors is a shorter floor and not a broken one.
    /// </summary>
    static RoomModule Attach(System.Func<GameObject, GameObject> spawn,
                             GameObject prefab, RoomModule from,
                             Transform level, string label)
    {
        RoomExit exit = null;

        foreach (var e in RoomExit.ExitsUnder(from.transform))
        {
            if (e == null || e.used) continue;
            if (!string.IsNullOrEmpty(label) && e.label != label) continue;
            exit = e;
            break;
        }

        if (exit == null) return null;

        var room = Place(spawn, prefab, level);
        if (room == null) return null;

        // The whole of the placement maths. See the header.
        room.transform.SetPositionAndRotation(exit.Position, exit.Rotation);
        exit.used = true;

        return room;
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
