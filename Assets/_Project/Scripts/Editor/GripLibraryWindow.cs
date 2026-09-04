// GripLibraryWindow.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/GripLibraryWindow.cs
// Open with: SAFE DEPOSIT / Player / Grip Library
//
// ========================================================================
// EVERY CARRYABLE ITEM, AND HOW EACH ONE IS HELD.
//
// "can i add like list of items and how i can grab each and save the data"
//
// This is the list. What it is NOT is a separate database - and that
// distinction is the whole design.
//
// A grip table keyed by item name is the obvious thing to build and it rots
// immediately: rename a prefab and the row orphans, add a prop and nothing
// reminds you the table needs a row, delete an item and a dead entry sits
// there forever. Worse, an item's grip would then live somewhere other than
// the item, so duplicating a prefab to make a variant would silently give you
// something with no grip at all.
//
// So the DATA lives on each Carryable, saved in its own prefab, and this
// window is only a VIEW over it. It cannot go out of date because there is
// nothing to keep in sync - it re-scans the project and shows what is
// actually there. Duplicating a prefab duplicates its grip for free, deleting
// one deletes its grip, and renaming is a non-event.
//
// What the window is genuinely for is the thing the Inspector cannot do:
// seeing all of them at once. Which items are still on Auto, which have been
// authored, and whether the crate and the toolbox ended up with wildly
// different finger curls for no reason.
// ========================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GripLibraryWindow : EditorWindow
{
    [MenuItem("SAFE DEPOSIT/Player/Grip Library")]
    public static void Open()
    {
        var w = GetWindow<GripLibraryWindow>("Grip Library");
        w.minSize = new Vector2(420f, 300f);
        w.Rescan();
    }

    class Row
    {
        public GameObject prefab;
        public Carryable item;
        public string path;
        public bool expanded;
    }

    readonly List<Row> rows = new List<Row>();
    Vector2 scroll;
    string filter = "";
    bool onlyAuto = false;

    void OnEnable() => Rescan();

    /// <summary>
    /// Find every prefab in the project with a Carryable on it.
    ///
    /// Scanned rather than registered, because a registry is a list somebody
    /// has to remember to add to, and that list will be wrong within a week.
    /// </summary>
    void Rescan()
    {
        rows.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            var c = go.GetComponent<Carryable>();
            if (c == null) continue;

            rows.Add(new Row { prefab = go, item = c, path = path });
        }

        rows.Sort((a, b) => string.Compare(a.prefab.name, b.prefab.name,
                                           System.StringComparison.OrdinalIgnoreCase));
    }

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                Rescan();

            filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField);

            onlyAuto = GUILayout.Toggle(onlyAuto, "Only Auto",
                                        EditorStyles.toolbarButton, GUILayout.Width(80f));
        }

        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No prefabs with a Carryable component found. If you expected " +
                "some, press Rescan - the list is built when the window opens.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(
            "Auto items are measured from their bounds and need no setup. " +
            "Switch one to Custom only when Auto is wrong for it.",
            EditorStyles.wordWrappedMiniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (var row in rows)
        {
            if (row.item == null) continue;

            if (!string.IsNullOrEmpty(filter) &&
                row.prefab.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (onlyAuto && row.item.HasCustomGrip) continue;

            DrawRow(row);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawRow(Row row)
    {
        var item = row.item;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                row.expanded = EditorGUILayout.Foldout(row.expanded, row.prefab.name, true);

                GUILayout.FlexibleSpace();

                GUILayout.Label(item.Weight.ToString(), EditorStyles.miniLabel,
                                GUILayout.Width(60f));

                GUILayout.Label(item.HasCustomGrip ? "Custom" : "Auto",
                                EditorStyles.miniBoldLabel, GUILayout.Width(55f));

                if (item.HasCustomGrip &&
                    GUILayout.Button("Reseed", EditorStyles.miniButton, GUILayout.Width(58f)))
                {
                    Undo.RecordObject(item, "Reseed grip");
                    item.SeedGripsFromBounds(0.78f, 0.85f, 0.55f, 0.06f);
                    EditorUtility.SetDirty(item);
                    AssetDatabase.SaveAssetIfDirty(row.prefab);
                }

                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(55f)))
                {
                    Selection.activeObject = row.prefab;
                    EditorGUIUtility.PingObject(row.prefab);
                }
            }

            if (!row.expanded) return;

            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();

            var mode = (Carryable.GripMode)EditorGUILayout.EnumPopup("Grip Mode", item.gripMode);

            if (mode != item.gripMode)
            {
                item.gripMode = mode;

                // Switching to Custom with two hands at the origin is useless
                // and looks broken, so seed it here the same way the Inspector
                // button does.
                if (mode == Carryable.GripMode.Custom &&
                    item.leftGrip.localPosition == Vector3.zero &&
                    item.rightGrip.localPosition == Vector3.zero)
                    item.SeedGripsFromBounds(0.78f, 0.85f, 0.55f, 0.06f);
            }

            if (item.HasCustomGrip)
            {
                // The first version of SeedGripsFromBounds did its arithmetic
                // in local space, so on any scaled prefab it produced hand
                // positions metres away from the object. Those are still sitting
                // in whatever was seeded before the fix, and a stray 4.3 in a
                // column of decimals is easy to read straight past.
                if (Implausible(item.leftGrip) || Implausible(item.rightGrip))
                    EditorGUILayout.HelpBox(
                        "These grip points are further than a metre from the " +
                        "item's origin, which almost certainly means they were " +
                        "seeded before the scale bug was fixed. Press Reseed.",
                        MessageType.Warning);

                Hand("Left", item.leftGrip);
                Hand("Right", item.rightGrip);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Measured from bounds: " + item.WorldBounds.size.ToString("F2"),
                    EditorStyles.miniLabel);
            }

            if (EditorGUI.EndChangeCheck())
            {
                // The prefab ASSET is being edited directly, so it has to be
                // marked dirty by hand - there is no scene to carry the change.
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(row.prefab);
            }

            EditorGUI.indentLevel--;
        }
    }

    /// <summary>A hand more than a metre from the item's own origin is not a
    /// grip on it, whatever the numbers say.</summary>
    static bool Implausible(Carryable.HandGrip g) =>
        g.used && g.localPosition.magnitude > 1f;

    static void Hand(string label, Carryable.HandGrip g)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        g.used = EditorGUILayout.Toggle("Used", g.used);

        if (g.used)
        {
            g.localPosition = EditorGUILayout.Vector3Field("Position", g.localPosition);
            g.localEuler = EditorGUILayout.Vector3Field("Palm", g.localEuler);

            g.thumb = EditorGUILayout.Slider("Thumb", g.thumb, 0f, 1f);
            g.index = EditorGUILayout.Slider("Index", g.index, 0f, 1f);
            g.middle = EditorGUILayout.Slider("Middle", g.middle, 0f, 1f);
            g.ring = EditorGUILayout.Slider("Ring", g.ring, 0f, 1f);
            g.little = EditorGUILayout.Slider("Little", g.little, 0f, 1f);
        }

        EditorGUI.indentLevel--;
    }
}
