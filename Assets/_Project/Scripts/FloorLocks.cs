using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts a locked door on a finished floor, and a key somewhere you can reach
/// without it.
///
/// ====================================================================
/// THE INVARIANT THAT MATTERS
///
/// The floor is a tree rooted at the lift. Locking one connection puts the
/// ENTIRE SUBTREE beyond it behind that lock - so the key must live on the
/// entrance side, or the crew has been handed a floor that cannot be finished.
///
/// That is not a detail to be careful about. It is the whole job, and it is
/// the reason this is computed from the graph rather than by dropping a key
/// "somewhere sensible". A key placed by eye is right most of the time, and
/// the times it is wrong end a playtest twenty minutes in with no clue why.
///
/// FloorValidator checks it independently afterwards, because a rule enforced
/// only by the code that also implements it is a rule with one witness.
///
/// LAYERS ARE PART OF BEING CORRECT HERE
///
/// PlayerCarry.pickupMask is m_Bits 320 - layers 6 and 8, Player and Loot. A
/// key on the Default layer is a key nobody can pick up, and it looks
/// completely fine. The same class of mistake made every generated room
/// unjumpable until 9 Sep; see docs/KNOWN_ISSUES.md.
/// ====================================================================
///
/// Phase 5, Step 7. See PHASE5_SPEC.md.
/// </summary>
public static class FloorLocks
{
    const float DoorWidth = 2f;
    const float DoorHeight = 2.5f;

    /// <summary>Small enough to be one-handed and stowable - Carryable calls
    /// anything at or under 8kg Small.</summary>
    const float KeyMass = 1.5f;

    /// <summary>
    /// Lock one connection on this floor and place its key.
    ///
    /// Does nothing and reports false when the floor cannot support a lock -
    /// too few rooms, or nowhere safe to put the key. A floor without a locked
    /// door is a lesser floor; a floor locked against itself is a broken one.
    /// </summary>
    public static bool Install(List<RoomModule> rooms, System.Random rng,
                               System.Func<GameObject, GameObject> _ = null)
    {
        if (rooms == null || rooms.Count < 4) return false;

        var entrance = EntranceRoom(rooms);
        if (entrance == null) return false;

        // ---- WALK THE TREE FROM THE LIFT ----
        //
        // parentOf tells us which side of any connection is nearer the
        // entrance, which is the only thing that decides where a key may go.
        var parentOf = new Dictionary<RoomModule, RoomModule>();
        var order = new List<RoomModule>();

        var queue = new Queue<RoomModule>();
        queue.Enqueue(entrance);
        parentOf[entrance] = null;

        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            order.Add(room);

            foreach (var d in RoomExit.DoorsUnder(room.transform))
            {
                if (d == null || d.partner == null) continue;

                var next = d.partner.GetComponentInParent<RoomModule>();
                if (next == null || parentOf.ContainsKey(next)) continue;

                parentOf[next] = room;
                queue.Enqueue(next);
            }
        }

        // ---- PICK A CONNECTION WORTH LOCKING ----
        //
        // Never the entrance room's own way in: locking the first door means
        // the crew stands in one room holding nothing, looking at the rest of
        // the floor. A lock should cost you a branch, not the floor.
        var candidates = new List<RoomModule>();

        foreach (var room in order)
        {
            if (room == entrance) continue;
            if (parentOf[room] == entrance) continue;   // one room of breathing space

            candidates.Add(room);
        }

        if (candidates.Count == 0) return false;

        // Shuffled by the floor's own rng, so the same run and floor lock the
        // same door on every machine without any of it being sent.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (var locked in candidates)
        {
            var behind = Subtree(locked, parentOf, order);

            // Somewhere to put the key that is NOT behind the door.
            var socket = FreeLootSocket(order, behind, rng);
            if (socket == null) continue;

            var pair = ConnectionBetween(parentOf[locked], locked);
            if (pair.Item1 == null) continue;

            string id = $"key_{locked.GetInstanceID():X}";

            SpawnDoor(pair.Item1, pair.Item2, id);
            SpawnKey(socket, id);


            return true;
        }

        return false;
    }

    // ------------------------------------------------------------------

    static RoomModule EntranceRoom(List<RoomModule> rooms)
    {
        foreach (var room in rooms)
        {
            if (room == null) continue;

            foreach (var d in RoomExit.DoorsUnder(room.transform))
                if (d != null && d.isEntrance) return room;
        }

        return null;
    }

    /// <summary>Every room at or beyond <paramref name="root"/>, walking away
    /// from the entrance.</summary>
    static HashSet<RoomModule> Subtree(RoomModule root,
                                       Dictionary<RoomModule, RoomModule> parentOf,
                                       List<RoomModule> order)
    {
        var inside = new HashSet<RoomModule> { root };

        // order is breadth-first from the entrance, so a single forward pass
        // reaches every descendant after its parent.
        foreach (var room in order)
        {
            var parent = parentOf[room];
            if (parent != null && inside.Contains(parent)) inside.Add(room);
        }

        return inside;
    }

    /// <summary>The two doors joining these rooms.</summary>
    static (RoomExit, RoomExit) ConnectionBetween(RoomModule a, RoomModule b)
    {
        if (a == null || b == null) return (null, null);

        foreach (var d in RoomExit.DoorsUnder(a.transform))
        {
            if (d == null || d.partner == null) continue;
            if (d.partner.GetComponentInParent<RoomModule>() == b) return (d, d.partner);
        }

        return (null, null);
    }

    /// <summary>A loot socket in a room the crew can reach WITHOUT the key.</summary>
    static RoomSocket FreeLootSocket(List<RoomModule> order,
                                     HashSet<RoomModule> behindTheDoor,
                                     System.Random rng)
    {
        var options = new List<RoomSocket>();

        foreach (var room in order)
        {
            if (behindTheDoor.Contains(room)) continue;

            foreach (var s in room.Sockets(RoomSocket.Kind.Loot)) options.Add(s);
            foreach (var s in room.Sockets(RoomSocket.Kind.Lock)) options.Add(s);
        }

        return options.Count == 0 ? null : options[rng.Next(options.Count)];
    }

    // ------------------------------------------------------------------

    static GameObject SpawnDoor(RoomExit near, RoomExit far, string keyId)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "LockedDoor";

        // Parented to the room NEARER the entrance, so the door dies with the
        // floor it belongs to and there is exactly one of it - both rooms'
        // doors sit at the same point, and spawning per-exit would give a door
        // inside a door.
        go.transform.SetParent(near.transform, false);
        go.transform.localPosition = new Vector3(0f, DoorHeight * 0.5f, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(0.18f, DoorHeight - 0.05f, DoorWidth - 0.08f);

        int env = LayerMask.NameToLayer("Environment");
        if (env >= 0) go.layer = env;

        Paint(go, new Color(0.65f, 0.32f, 0.12f));   // visibly not a wall

        var door = go.AddComponent<RoomDoor>();
        door.keyId = keyId;
        door.sideA = near;
        door.sideB = far;

        return go;
    }

    static GameObject SpawnKey(RoomSocket at, string keyId)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Key";

        go.transform.SetParent(at.transform.parent, false);
        go.transform.position = at.Position + Vector3.up * 0.15f;
        go.transform.rotation = at.Rotation;
        go.transform.localScale = new Vector3(0.12f, 0.05f, 0.28f);

        // ---- THE LAYER IS NOT COSMETIC ----
        //
        // PlayerCarry.pickupMask is layers 6 and 8. A key on Default cannot be
        // picked up and gives no clue why.
        int loot = LayerMask.NameToLayer("Loot");
        if (loot >= 0) go.layer = loot;

        Paint(go, new Color(0.9f, 0.78f, 0.25f));

        var body = go.AddComponent<Rigidbody>();
        body.mass = KeyMass;                 // Small: one hand, jumpable, stowable

        go.AddComponent<Carryable>();
        go.AddComponent<DoorKey>().keyId = keyId;

        return go;
    }

    static void Paint(GameObject go, Color colour)
    {
        var r = go.GetComponent<MeshRenderer>();
        if (r == null) return;

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return;

        var m = new Material(sh);
        m.SetColor("_BaseColor", colour);
        r.sharedMaterial = m;
    }
}
