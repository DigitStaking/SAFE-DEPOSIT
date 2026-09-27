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

        CheckTheWayIn(rooms, bad);
        CheckItActuallyBranches(rooms, minJunctions, bad);
        CheckLocksAreSolvable(rooms, bad);

        return bad;
    }

    // ====================================================================
    // THE WAY OUT OF THE LIFT IS A HARD CONSTRAINT
    //
    // "No room overlaps the shaft" is not the same promise as "a player can
    // walk out of the car". The start room can satisfy every keep-out rule and
    // still stand a column, a partition or its own wall squarely in the
    // doorway - and the first thing anybody does on a floor is step forward.
    //
    // So this measures the corridor a player actually walks through: from just
    // past the doorway wall, straight into the room, at CHEST HEIGHT.
    //
    // The height band is the whole trick. Every room has a floor renderer and
    // a ceiling renderer whose bounds span the entire interior, so a test box
    // that reached the ground would report every room as blocked by its own
    // floor. Sampling from 0.4m to 2.0m clears the floor slab (top at y 0) and
    // the ceiling (bottom at y 4) and still catches anything a person would
    // walk into.
    // ====================================================================

    /// <summary>Depth of clear corridor required in front of the entrance.</summary>
    const float WayInDepth = 3f;

    /// <summary>Door is 2m wide; 1.8 leaves a little either side so a wall
    /// that merely touches the frame is not called an obstruction.</summary>
    const float WayInWidth = 1.8f;

    const float WayInLow = 0.4f;
    const float WayInHigh = 2f;

    static void CheckTheWayIn(List<RoomModule> rooms, List<string> bad)
    {
        RoomExit entrance = null;

        foreach (var room in rooms)
        {
            if (room == null) continue;
            foreach (var d in RoomExit.DoorsUnder(room.transform))
                if (d != null && d.isEntrance) { entrance = d; break; }
            if (entrance != null) break;
        }

        if (entrance == null) return;    // already reported as a fault above

        // The entrance faces OUT of the floor, so walking in is -Outward.
        Vector3 inward = -entrance.Outward;
        inward.y = 0f;
        if (inward.sqrMagnitude < 0.0001f) return;
        inward.Normalize();

        // Start past the doorway wall itself, so the wall the door is cut
        // through is not mistaken for something blocking the door.
        Vector3 from = entrance.Position + inward * 0.6f;
        Vector3 to = entrance.Position + inward * (0.6f + WayInDepth);

        var corridor = new Bounds((from + to) * 0.5f, Vector3.zero);
        corridor.Encapsulate(from);
        corridor.Encapsulate(to);

        // Widen across the door and set the height band.
        Vector3 across = Vector3.Cross(Vector3.up, inward).normalized * (WayInWidth * 0.5f);
        corridor.Encapsulate(from + across);
        corridor.Encapsulate(from - across);
        corridor.Encapsulate(to + across);
        corridor.Encapsulate(to - across);

        var size = corridor.size;
        size.y = WayInHigh - WayInLow;

        corridor = new Bounds(
            new Vector3(corridor.center.x, (WayInLow + WayInHigh) * 0.5f, corridor.center.z),
            size);

        int blockers = 0;

        foreach (var room in rooms)
        {
            if (room == null) continue;

            foreach (var r in room.GetComponentsInChildren<MeshRenderer>())
            {
                if (r == null) continue;

                // Floors and ceilings span the room and are excluded by the
                // height band, not by name - a rename cannot break this.
                if (r.bounds.max.y <= WayInLow || r.bounds.min.y >= WayInHigh) continue;

                if (r.bounds.Intersects(corridor)) blockers++;
            }
        }

        if (blockers > 0)
            bad.Add($"{blockers} piece(s) of geometry stand in the {WayInDepth}m " +
                    "of corridor in front of the lift doorway - a player would " +
                    "walk out of the car into a wall");
    }

    // ====================================================================
    // BRANCHING, MEASURED ON WHAT WAS BUILT
    //
    // Counting rooms with three or more DOORS says the prefab has three doors.
    // It does not say three rooms were attached to it. Those are the same
    // number only while placement never fails - which is exactly the case this
    // is here to catch.
    //
    // So this walks the real partner links and counts CONNECTED neighbours.
    // A floor where nothing exceeds two neighbours is a corridor made of
    // rooms, however many junction prefabs it contains.
    // ====================================================================

    static void CheckItActuallyBranches(List<RoomModule> rooms, int minJunctions,
                                        List<string> bad)
    {
        int realJunctions = 0;
        int widest = 0;

        foreach (var room in rooms)
        {
            if (room == null) continue;

            int neighbours = 0;

            foreach (var d in RoomExit.DoorsUnder(room.transform))
            {
                if (d == null || d.isEntrance) continue;
                if (d.partner == null) continue;

                var other = d.partner.GetComponentInParent<RoomModule>();
                if (other != null && other != room) neighbours++;
            }

            if (neighbours > widest) widest = neighbours;
            if (neighbours >= 3) realJunctions++;
        }

        if (widest <= 2)
            bad.Add("no room has more than two connected neighbours - this floor " +
                    "is a corridor made of rooms, whatever its door counts say");

        if (realJunctions < minJunctions)
            bad.Add($"only {realJunctions} room(s) actually have 3+ rooms attached " +
                    $"(wanted {minJunctions}) - door counts are not connections");
    }

    // ====================================================================
    // A LOCKED DOOR MUST NOT LOCK AWAY ITS OWN KEY
    //
    // The floor is a tree, so a locked connection puts the whole subtree
    // beyond it out of reach. If the key is in that subtree the round cannot
    // be finished, and nothing about the floor LOOKS wrong - every door
    // connects, nothing overlaps, every room is reachable on paper.
    //
    // FloorLocks already places the key outside the locked subtree. This
    // checks it independently, because a rule enforced only by the code that
    // implements it has exactly one witness.
    // ====================================================================

    static void CheckLocksAreSolvable(List<RoomModule> rooms, List<string> bad)
    {
        var doors = new List<RoomDoor>();

        foreach (var room in rooms)
        {
            if (room == null) continue;
            doors.AddRange(room.GetComponentsInChildren<RoomDoor>());
        }

        if (doors.Count == 0) return;      // a floor with no lock is fine

        // Which rooms can be reached from the entrance WITHOUT opening
        // anything - walked through the real door links, refusing to cross a
        // locked one.
        RoomModule start = null;

        foreach (var room in rooms)
        {
            if (room == null) continue;
            foreach (var d in RoomExit.DoorsUnder(room.transform))
                if (d != null && d.isEntrance) { start = room; break; }
            if (start != null) break;
        }

        if (start == null) return;         // already reported

        var reachable = new HashSet<RoomModule>();
        var stack = new Stack<RoomModule>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var room = stack.Pop();
            if (!reachable.Add(room)) continue;

            foreach (var d in RoomExit.DoorsUnder(room.transform))
            {
                if (d == null || d.partner == null) continue;
                if (Blocked(d, doors)) continue;

                var other = d.partner.GetComponentInParent<RoomModule>();
                if (other != null) stack.Push(other);
            }
        }

        foreach (var door in doors)
        {
            if (door == null || !door.IsLocked) continue;

            bool keyInReach = false;

            foreach (var key in Object.FindObjectsByType<DoorKey>(FindObjectsSortMode.None))
            {
                if (key == null || key.keyId != door.keyId) continue;

                var where = key.GetComponentInParent<RoomModule>();
                if (where != null && reachable.Contains(where)) { keyInReach = true; break; }
            }

            if (!keyInReach)
                bad.Add($"the key for door '{door.keyId}' is behind that same door - " +
                        "the floor has locked itself and cannot be finished");
        }
    }

    /// <summary>Is this doorway shut by a locked door?</summary>
    static bool Blocked(RoomExit door, List<RoomDoor> doors)
    {
        foreach (var d in doors)
        {
            if (d == null || !d.IsLocked) continue;
            if (d.sideA == door || d.sideB == door) return true;
        }

        return false;
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
