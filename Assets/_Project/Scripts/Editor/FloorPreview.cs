using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates floors so you can look at them, and validates every one.
///
/// It calls FloorGenerator.Build - the same method the runtime will - rather
/// than a preview-shaped copy. A preview that runs different code from the
/// game is a preview that lies.
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public static class FloorPreview
{
    const string RoomDir = "Assets/_Project/Resources/Rooms";
    const string PreviewRoot = "FLOOR_PREVIEW";

    const int MinRooms = 7;
    const int MaxRooms = 14;
    const int MinJunctions = 2;
    const int MinDeadEnds = 2;

    // The same volume FloorDirector protects at runtime. The preview has to
    // test what the game tests, or it is checking a different generator.
    const float ShaftKeepOut = 7.6f;

    /// <summary>The shaft, in world space, built from the level's own frame so
    /// it turns with the floor.</summary>
    static Bounds KeepOut(Transform level)
    {
        var b = new Bounds(level.TransformPoint(Vector3.up * 3f), Vector3.zero);

        for (int i = 0; i < 8; i++)
            b.Encapsulate(level.TransformPoint(new Vector3(
                (i & 1) == 0 ? -ShaftKeepOut : ShaftKeepOut,
                (i & 2) == 0 ? -2f : 8f,
                (i & 4) == 0 ? -ShaftKeepOut : ShaftKeepOut)));

        return b;
    }

    [MenuItem("Tools/Rooms/Preview Ten Floors")]
    public static void PreviewTen()
    {
        var cat = LoadCatalogue(out string catReport);

        if (cat == null)
        {
            Debug.LogError(catReport + "\nRun Tools > Rooms > Build Room Set first.");
            return;
        }

        Clear();

        var root = new GameObject(PreviewRoot);
        int run = Mathf.Max(1, Campaign.RunNumber);

        var sb = new StringBuilder();
        sb.AppendLine($"TEN FLOORS, run {run}");
        sb.AppendLine(catReport);
        sb.AppendLine();

        int failed = 0, invalid = 0;

        for (int floor = 1; floor <= 10; floor++)
        {
            var level = new GameObject($"Preview_Level_{floor:00}");
            level.transform.SetParent(root.transform, false);

            // Far enough apart that a big floor cannot reach its neighbour and
            // make two valid floors look like one broken one.
            level.transform.localPosition = new Vector3(0f, 0f, floor * 140f);

            var rooms = FloorGenerator.Build(
                level.transform, run, floor, cat, Vector3.zero,
                MinRooms, MaxRooms, MinJunctions,
                p => (GameObject)PrefabUtility.InstantiatePrefab(p),
                out FloorGraph graph, out string failure, KeepOut(level.transform));

            if (rooms == null)
            {
                failed++;
                sb.AppendLine($"  {floor:00}  FAILED - {failure}");
                continue;
            }

            var faults = FloorValidator.Check(rooms, MinRooms, MinJunctions,
                                              MinDeadEnds, KeepOut(level.transform));

            int dead = 0, junc = 0;
            foreach (var r in rooms)
            {
                int n = r.DoorCount;
                if (n == 1) dead++;
                if (n >= 3) junc++;
            }

            sb.AppendLine($"  {floor:00}  {rooms.Count} rooms  " +
                          $"{junc} junction(s)  {dead} dead end(s)  " +
                          $"depth {graph.Depth()}  " +
                          (faults.Count == 0 ? "VALID" : $"{faults.Count} FAULT(S)"));

            foreach (var f in faults)
            {
                invalid++;
                sb.AppendLine($"        ! {f}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(failed == 0 && invalid == 0
            ? "All ten generated and all ten passed validation."
            : $"{failed} floor(s) could not be generated, {invalid} fault(s) found.");

        sb.AppendLine();
        sb.AppendLine("What code cannot check, and is on you:");
        sb.AppendLine("  can a stranger navigate these without a map?");
        sb.AppendLine("  do they feel like different places?");

        if (failed > 0 || invalid > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());

        Selection.activeGameObject = root;
    }

    /// <summary>
    /// Twenty seeds, no geometry kept - the fast check that the RULES hold, as
    /// opposed to the slow one where you look at it.
    /// </summary>
    [MenuItem("Tools/Rooms/Validate Twenty Seeds")]
    public static void ValidateMany()
    {
        var cat = LoadCatalogue(out string catReport);
        if (cat == null) { Debug.LogError(catReport); return; }

        Clear();
        var root = new GameObject(PreviewRoot);

        var sb = new StringBuilder();
        sb.AppendLine("TWENTY SEEDS");
        sb.AppendLine(catReport);
        sb.AppendLine();

        int ok = 0, bad = 0;
        var tally = new Dictionary<string, int>();

        // Which door counts actually turned up. A run of twenty seeds that
        // never places a 4-door room has not tested the 4-door room, however
        // green the report looks.
        var typesSeen = new int[5];

        for (int seed = 1; seed <= 20; seed++)
        {
            var level = new GameObject($"S{seed}");
            level.transform.SetParent(root.transform, false);
            level.transform.localPosition = new Vector3(0f, 0f, seed * 140f);

            var rooms = FloorGenerator.Build(
                level.transform, seed, seed, cat, Vector3.zero,
                MinRooms, MaxRooms, MinJunctions,
                p => (GameObject)PrefabUtility.InstantiatePrefab(p),
                out FloorGraph graph, out string failure, KeepOut(level.transform));

            if (rooms == null)
            {
                bad++;
                Count(tally, "could not generate: " + failure);
                continue;
            }

            var faults = FloorValidator.Check(rooms, MinRooms, MinJunctions,
                                              MinDeadEnds, KeepOut(level.transform));

            foreach (var r in rooms)
            {
                int n = r.DoorCount;
                if (n >= 1 && n <= 4) typesSeen[n]++;
            }

            if (faults.Count == 0) ok++;
            else { bad++; foreach (var f in faults) Count(tally, f); }
        }

        sb.AppendLine($"  {ok} valid, {bad} not.");
        sb.AppendLine();
        sb.AppendLine("  room types placed across all seeds:");

        bool allTypes = true;
        for (int d = 1; d <= 4; d++)
        {
            sb.AppendLine($"    {d}-door x{typesSeen[d]}");
            if (typesSeen[d] == 0) allTypes = false;
        }

        if (!allTypes)
            sb.AppendLine("  ! a door count never appeared - that type is UNTESTED " +
                          "however clean the rest of this looks");

        if (tally.Count > 0)
        {
            sb.AppendLine();
            foreach (var kv in tally) sb.AppendLine($"  x{kv.Value}  {kv.Key}");
        }

        Object.DestroyImmediate(root);

        if (bad > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/Rooms/Clear Floor Preview")]
    public static void Clear()
    {
        var existing = GameObject.Find(PreviewRoot);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Sort every room prefab by how many doors it actually has.
    ///
    /// Counted from the prefab rather than read off a name or a folder, so a
    /// room filed as a 3-door that carries two is caught here instead of
    /// breaking the graph's degree invariant somewhere further down.
    /// </summary>
    static FloorGenerator.Catalogue LoadCatalogue(out string report)
    {
        var buckets = new List<GameObject>[5];
        for (int i = 0; i < 5; i++) buckets[i] = new List<GameObject>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            int doors = RoomExit.DoorsUnder(go.transform).Length;
            if (doors >= 1 && doors <= 4) buckets[doors].Add(go);
        }

        var sb = new StringBuilder("  rooms: ");
        for (int d = 1; d <= 4; d++) sb.Append($"{d}-door x{buckets[d].Count}  ");

        report = sb.ToString();

        if (buckets[1].Count == 0)
        {
            report = "No 1-door room found. Without a dead end no branch can be " +
                     "terminated, so no floor can ever be valid.\n" + report;
            return null;
        }

        var cat = new FloorGenerator.Catalogue();
        for (int d = 1; d <= 4; d++) cat.byDoorCount[d] = buckets[d].ToArray();

        return cat;
    }

    static void Count(Dictionary<string, int> tally, string key)
    {
        tally.TryGetValue(key, out int n);
        tally[key] = n + 1;
    }
}
