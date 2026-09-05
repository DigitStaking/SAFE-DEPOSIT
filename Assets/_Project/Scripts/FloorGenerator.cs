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

        // ---- MAIN, THROUGH THE LANDING'S EXIT ----
        var main = mains != null && mains.Length > 0
            ? Attach(spawn, mains[layout.main], landing, level, "")
            : null;

        if (main != null) built.Add(main);
        if (main == null) return built;

        // ---- THE OPTIONAL PAIR ----
        //
        // Named exits, because this is the one module with two ways on and
        // guessing which is which would put the dead end where the side room
        // goes on some floors and not others.
        if (layout.hasSide && sides != null && sides.Length > 0)
        {
            var side = Attach(spawn, sides[0], main, level, "side");
            if (side != null) built.Add(side);
        }

        if (layout.hasBack && backs != null && backs.Length > 0)
        {
            var back = Attach(spawn, backs[0], main, level, "back");
            if (back != null) built.Add(back);
        }

        return built;
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
