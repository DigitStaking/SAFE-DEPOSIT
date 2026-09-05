using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates ten floors so you can look at them.
///
/// This exists because Step 6's done-when is not a property of the code. "Ten
/// generated floors a stranger can navigate without a map" and "you can tell
/// them apart from memory" are things a person decides by walking around, and
/// no assertion I can write stands in for that.
///
/// It calls FloorGenerator.Build - the same method the runtime will - rather
/// than a preview-shaped copy of it. A preview that runs different code from
/// the game is a preview that lies, and this project has already lost an
/// afternoon to a test path and a real path disagreeing.
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public static class FloorPreview
{
    const string RoomDir = "Assets/_Project/Prefabs/Rooms";
    const string PreviewRoot = "FLOOR_PREVIEW";

    [MenuItem("Tools/Rooms/Preview Ten Floors")]
    public static void PreviewTen()
    {
        var landings = Load("Room_Landing_Open", "Room_Landing_Pillared");
        var mains = Load("Room_Main_Hall", "Room_Main_Divided", "Room_Main_Columns");
        var sides = Load("Room_Side_Store");
        var backs = Load("Room_Back_DeadEnd");

        if (landings.Length == 0 || mains.Length == 0)
        {
            Debug.LogError($"No modules in {RoomDir}. " +
                           "Run Tools > Rooms > Build Six Modules first.");
            return;
        }

        Clear();

        var root = new GameObject(PreviewRoot);
        int run = Mathf.Max(1, Campaign.RunNumber);

        var report = new System.Text.StringBuilder();
        report.AppendLine($"TEN FLOORS, run {run}");
        report.AppendLine();

        for (int floor = 1; floor <= 10; floor++)
        {
            // Side by side rather than stacked, so all ten are visible at once
            // in the scene view. The real shaft puts them 5m apart vertically
            // and you can only ever see one.
            var level = new GameObject($"Preview_Level_{floor:00}");
            level.transform.SetParent(root.transform, false);
            level.transform.localPosition = new Vector3(0f, 0f, floor * 30f);

            var rooms = FloorGenerator.Build(
                level.transform, run, floor,
                landings, mains, sides, backs,
                new Vector3(0f, 0f, 0f),
                p => (GameObject)PrefabUtility.InstantiatePrefab(p));

            var names = new List<string>();
            foreach (var r in rooms) names.Add(r != null ? r.label : "?");

            report.AppendLine($"  {floor:00}  {string.Join("  >  ", names)}");
        }

        report.AppendLine();
        report.AppendLine("The done-when is not something code can check:");
        report.AppendLine("  can a stranger navigate these without a map, and");
        report.AppendLine("  can you tell them apart from memory?");
        report.AppendLine("Walk them. If two floors in a row feel the same, the");
        report.AppendLine("module set is too small - that is a Step 5 answer, not");
        report.AppendLine("a generator one.");

        Debug.Log(report.ToString());
        Selection.activeGameObject = root;
    }

    [MenuItem("Tools/Rooms/Clear Floor Preview")]
    public static void Clear()
    {
        var existing = GameObject.Find(PreviewRoot);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    static GameObject[] Load(params string[] names)
    {
        var found = new List<GameObject>();

        foreach (var n in names)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomDir}/{n}.prefab");
            if (go != null) found.Add(go);
        }

        return found.ToArray();
    }
}
