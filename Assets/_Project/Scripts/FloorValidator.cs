using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Checks a placed floor against the rules, and REJECTS - it never repairs.
///
/// ====================================================================
/// WHY REJECTING IS THE POINT
///
/// The old generator's only response to a problem was to hide it: a door that
/// could not be connected got a wall built in front of it, and the floor
/// shipped looking fine. Nothing was ever reported because nothing was ever
/// checked - there was no model of what "correct" meant.
///
/// So this returns faults, and the generator throws the floor away and tries
/// another seed. A floor is either right or it does not exist. Repairing one
/// is how you get a level that passes every check and still has a door into
/// the skybox, because the repair is applied after the check that would have
/// caught it.
/// ====================================================================
///
/// Editor and development use. Nothing here draws to the game HUD.
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public static class FloorValidator
{
    /// <summary>Everything wrong with this floor. Empty means it is valid.</summary>
    public static List<string> Check(List<RoomModule> rooms, int minRooms,
                                     int minJunctions, int minDeadEnds,
                                     Bounds keepOut = default)
    {
        var bad = new List<string>();

        if (rooms == null || rooms.Count == 0)
        {
            bad.Add("no floor was generated at all");
            return bad;
        }

        if (rooms.Count < minRooms)
            bad.Add($"only {rooms.Count} rooms, wanted at least {minRooms}");

        // ---- DOORS ----
        int entrances = 0, unconnected = 0, oneSided = 0, misaligned = 0;
        int deadEnds = 0, junctions = 0;

        foreach (var room in rooms)
        {
            if (room == null) { bad.Add("a null room in the list"); continue; }

            var doors = RoomExit.DoorsUnder(room.transform);

            if (doors.Length == 1) deadEnds++;
            if (doors.Length >= 3) junctions++;

            foreach (var d in doors)
            {
                if (d == null) continue;

                if (d.isEntrance) { entrances++; continue; }

                // THE rule. A normal door with nothing on the other side is
                // exactly what this whole rewrite exists to make impossible.
                if (!d.connected || d.partner == null) { unconnected++; continue; }

                // Mutual, or it is half a link - which looks connected from
                // one room and is a hole from the other.
                if (d.partner.partner != d) { oneSided++; continue; }

                // The two doors must actually meet. A pair that is merely
                // NEAR is the gap you fall through.
                float gap = Vector3.Distance(d.Position, d.partner.Position);
                float facing = Vector3.Dot(d.Outward, d.partner.Outward);

                // Opposite means dot near -1. Anything else and the rooms are
                // joined at an angle, which leaves a wedge of nothing.
                if (gap > 0.05f || facing > -0.98f) misaligned++;
            }
        }

        if (entrances != 1)
            bad.Add($"{entrances} entrance(s) - a floor must have exactly one, " +
                    "and it is the only opening to the outside");

        if (unconnected > 0)
            bad.Add($"{unconnected} door(s) connect to nothing - a door is a " +
                    "connection, not a decoration");

        if (oneSided > 0)
            bad.Add($"{oneSided} one-sided connection(s) - a link claimed from " +
                    "one room and not the other");

        if (misaligned > 0)
            bad.Add($"{misaligned} door pair(s) do not meet cleanly - a gap or " +
                    "an angle between them is somewhere to fall through");

        if (deadEnds < minDeadEnds)
            bad.Add($"only {deadEnds} dead-end room(s), wanted {minDeadEnds} - " +
                    "branches have to END somewhere and a 1-door room is the " +
                    "only honest way to do it");

        if (junctions < minJunctions)
            bad.Add($"only {junctions} junction(s) - without a room that offers " +
                    "a choice the floor is a corridor however long it is");

        // ---- OVERLAPS ----
        var boxes = new List<Bounds>();
        foreach (var room in rooms)
        {
            if (room == null) continue;
            boxes.Add(Footprint(room));
        }

        int overlaps = 0;
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
                if (boxes[i].Intersects(boxes[j])) overlaps++;

        if (overlaps > 0)
            bad.Add($"{overlaps} pair(s) of rooms overlap");

        // ---- THE LIFT MUST BE POSSIBLE TO LEAVE ----
        //
        // Rooms not overlapping EACH OTHER says nothing about the shaft. A
        // branch that curls back and lands in front of the doors passes every
        // other check on this list and still leaves the player walking out of
        // the car into a wall.
        if (keepOut.extents.sqrMagnitude > 0f)
        {
            int intruding = 0;

            foreach (var box in boxes)
                if (keepOut.Intersects(box)) intruding++;

            if (intruding > 0)
                bad.Add($"{intruding} room(s) reach into the shaft or block the " +
                        "lift doorway");
        }

        // ---- REACHABILITY ----
        //
        // Walked through the DOORS rather than trusted from the graph,
        // because the graph is a claim about what was asked for and this is a
        // check on what was actually built.
        var seen = new HashSet<RoomModule>();
        var stack = new Stack<RoomModule>();

        RoomModule start = null;
        foreach (var room in rooms)
        {
            if (room == null) continue;
            foreach (var d in RoomExit.DoorsUnder(room.transform))
                if (d != null && d.isEntrance) { start = room; break; }
            if (start != null) break;
        }

        if (start == null) bad.Add("no room carries the entrance door");
        else
        {
            stack.Push(start);

            while (stack.Count > 0)
            {
                var room = stack.Pop();
                if (!seen.Add(room)) continue;

                foreach (var d in RoomExit.DoorsUnder(room.transform))
                {
                    if (d == null || d.partner == null) continue;

                    var other = d.partner.GetComponentInParent<RoomModule>();
                    if (other != null && !seen.Contains(other)) stack.Push(other);
                }
            }

            int stranded = 0;
            foreach (var room in rooms)
                if (room != null && !seen.Contains(room)) stranded++;

            if (stranded > 0)
                bad.Add($"{stranded} room(s) cannot be walked to from the entrance");
        }

        return bad;
    }

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
}
