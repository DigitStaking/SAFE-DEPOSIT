using System.Collections.Generic;

/// <summary>
/// The floor as a GRAPH, decided before a single wall exists.
///
/// ====================================================================
/// TOPOLOGY FIRST. THIS IS THE WHOLE FIX.
///
/// The previous generator placed geometry and hoped. When a room would not
/// fit it sealed a door, which turned a four-door crossroads into a corridor
/// with two walls in it - and there was no graph anywhere, so nothing could
/// notice, check, or retry.
///
/// Here the shape is decided as pure numbers first: which room has how many
/// doors, and which door connects to which. Every node's degree EQUALS its
/// room's door count, by construction, so there is no such thing as a door
/// left over. Placement comes afterwards and either succeeds or backtracks;
/// it is never allowed to "fix" a problem by closing something.
///
/// THE TREE, AND WHY A TREE
///
/// A floor is a tree rooted at the entrance:
///
///   - the root's doors all lead to children (plus one Entrance door, which
///     is the only opening to the outside on the whole floor)
///   - every other room spends one door on its parent and the rest on
///     children
///   - a 1-door room therefore has no children AT ALL - it is a leaf, and
///     leaves are the only way a branch is allowed to end
///
/// So "no exits" is not a rule that has to be enforced afterwards. It falls
/// out of the construction: growth stops only by placing 1-door rooms, and a
/// 1-door room has nothing left to lead anywhere.
/// ====================================================================
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public class FloorGraph
{
    public class Node
    {
        /// <summary>How many doors this room must have. Degree in the graph
        /// equals this exactly - that is the invariant the whole file
        /// exists to hold.</summary>
        public int doors;

        public int parent = -1;
        public readonly List<int> children = new List<int>();

        public bool IsLeaf => doors == 1;
        public int Degree => (parent >= 0 ? 1 : 0) + children.Count;
    }

    public readonly List<Node> nodes = new List<Node>();
    public int Root => 0;

    public int Count => nodes.Count;

    /// <summary>Rooms with three or more doors - the junctions where a player
    /// actually has a choice. A floor with none of these is a corridor
    /// however many rooms it has.</summary>
    public int Junctions
    {
        get
        {
            int n = 0;
            foreach (var node in nodes) if (node.doors >= 3) n++;
            return n;
        }
    }

    public int Leaves
    {
        get
        {
            int n = 0;
            foreach (var node in nodes) if (node.IsLeaf) n++;
            return n;
        }
    }

    /// <summary>The deepest branch, for reporting. A tall thin graph is a
    /// corridor wearing a tree's clothes.</summary>
    public int Depth()
    {
        var depth = new int[nodes.Count];
        int deepest = 0;

        for (int i = 1; i < nodes.Count; i++)
        {
            depth[i] = depth[nodes[i].parent] + 1;
            if (depth[i] > deepest) deepest = depth[i];
        }

        return deepest;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Grow a floor.
    ///
    /// <paramref name="available"/> is which door counts there are rooms for -
    /// normally 1,2,3,4. A count with no room prefab behind it is never
    /// chosen, because a graph the geometry cannot express is worse than a
    /// smaller graph.
    /// </summary>
    public static FloorGraph Build(System.Random rng, int minRooms, int maxRooms,
                                   int minJunctions, HashSet<int> available)
    {
        // Tried a few times because the constraints (enough rooms, enough
        // junctions) are easier to satisfy by retrying than by steering. Each
        // attempt is a handful of integer operations.
        FloorGraph best = null;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            var g = Grow(rng, minRooms, maxRooms, available);

            if (g.Count >= minRooms && g.Junctions >= minJunctions && g.Leaves >= 2)
                return g;

            // Keep the least bad, so a pathological module set still produces
            // something rather than nothing.
            if (best == null || g.Count > best.Count) best = g;
        }

        return best;
    }

    static FloorGraph Grow(System.Random rng, int minRooms, int maxRooms,
                           HashSet<int> available)
    {
        var g = new FloorGraph();

        // ---- THE ROOT IS A JUNCTION ----
        //
        // Starting from a 1 or 2-door room guarantees a corridor for at least
        // the first two rooms, and the first thing a player sees is the thing
        // they judge the floor by. So the root is the widest room available.
        int rootDoors = Widest(available, 4);
        g.nodes.Add(new Node { doors = rootDoors });

        // Open slots: (node, how many children it still owes). The root owes
        // one child per door; every other room owes doors-1, having spent one
        // on its parent.
        var open = new List<int>();
        for (int i = 0; i < rootDoors; i++) open.Add(0);

        while (open.Count > 0)
        {
            // Which open slot to fill. Random rather than first-in, because
            // first-in is breadth-first is a fan, and last-in is depth-first
            // is a snake. Picking at random gives arms of different lengths,
            // which is what a maze looks like.
            int slot = rng.Next(open.Count);
            int parent = open[slot];
            open.RemoveAt(slot);

            // ---- WHEN TO STOP GROWING ----
            //
            // If finishing every remaining slot with leaves would already
            // reach the cap, then leaves are all we can afford. This is what
            // makes termination certain: a 1-door room adds no new slots, so
            // the open list can only shrink from here.
            bool mustClose = g.Count + open.Count + 1 >= maxRooms;

            int doors = mustClose ? 1 : PickDoors(rng, available, g.Count, minRooms);

            int id = g.Count;
            g.nodes.Add(new Node { doors = doors, parent = parent });
            g.nodes[parent].children.Add(id);

            // A 1-door room spends its only door on its parent and owes
            // nothing. Anything else owes doors-1 children.
            for (int i = 1; i < doors; i++) open.Add(id);
        }

        return g;
    }

    /// <summary>
    /// How many doors the next room has.
    ///
    /// Weighted toward continuing while the floor is still small, and toward
    /// closing once it is big enough - so a floor reaches its minimum size
    /// before it starts ending branches, without the size being forced by a
    /// hard rule that would make every floor the same size.
    /// </summary>
    static int PickDoors(System.Random rng, HashSet<int> available,
                         int placed, int minRooms)
    {
        bool small = placed < minRooms;

        // 1-door leaves are rare early and common late. 3s and 4s are what
        // make junctions, so they are worth more while there is room to grow.
        int[] pool = small
            ? new[] { 2, 2, 3, 3, 3, 4, 4 }
            : new[] { 1, 1, 1, 2, 2, 3, 4 };

        // Try the weighted pool, then fall back to anything the module set
        // actually has - never invent a door count with no room behind it.
        for (int i = 0; i < 12; i++)
        {
            int d = pool[rng.Next(pool.Length)];
            if (available.Contains(d)) return d;
        }

        return Widest(available, 1);
    }

    static int Widest(HashSet<int> available, int fallback)
    {
        for (int d = 4; d >= 1; d--)
            if (available.Contains(d)) return d;

        return fallback;
    }

    /// <summary>
    /// What is wrong with this graph, in plain sentences. Empty means it
    /// honours the contract.
    ///
    /// Checked here, on the numbers, BEFORE any geometry exists - so a floor
    /// that could never be right is rejected while rejecting it is free.
    /// </summary>
    public List<string> Problems(int minRooms, int minJunctions)
    {
        var bad = new List<string>();

        if (nodes.Count < minRooms)
            bad.Add($"only {nodes.Count} rooms, wanted at least {minRooms}");

        if (Junctions < minJunctions)
            bad.Add($"only {Junctions} junction(s) - a floor with no room " +
                    "offering a choice is a corridor however long it is");

        if (Leaves < 2)
            bad.Add($"only {Leaves} dead end(s) - a tree with one leaf is a line");

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];

            // THE invariant. Degree must equal door count, or some door has
            // nothing on the other side of it.
            if (n.Degree != n.doors)
                bad.Add($"room {i} has {n.doors} doors but {n.Degree} connections");

            if (i != Root && n.parent < 0)
                bad.Add($"room {i} is not attached to anything");

            if (n.IsLeaf && n.children.Count > 0)
                bad.Add($"room {i} is a 1-door dead end with {n.children.Count} children");
        }

        // Reachability, which for a tree means every node was reached by
        // walking down from the root - checked rather than assumed, because
        // "it is a tree by construction" is exactly the kind of claim that
        // stops being true after somebody edits Grow.
        var seen = new bool[nodes.Count];
        var stack = new Stack<int>();
        stack.Push(Root);

        while (stack.Count > 0)
        {
            int id = stack.Pop();
            if (seen[id]) continue;
            seen[id] = true;
            foreach (int c in nodes[id].children) stack.Push(c);
        }

        for (int i = 0; i < nodes.Count; i++)
            if (!seen[i]) bad.Add($"room {i} cannot be reached from the start");

        return bad;
    }
}
