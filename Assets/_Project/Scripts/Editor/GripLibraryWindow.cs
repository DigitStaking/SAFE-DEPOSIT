// GripLibraryWindow.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/GripLibraryWindow.cs
// Open with: SAFE DEPOSIT / Player / Grip Library
//
// ========================================================================
// TUNE IT WHILE PLAYING, THEN KEEP IT.
//
// "i can just click play test and see what the best position and change
//  parametre in inspector and later i can push this parametres directly to
//  library, with all grip details"
//
// That is the right workflow and it was broken in the middle. Unity throws
// away every play-mode change on Stop, so the only way to keep a value you
// found by feel was to write it on paper, stop, and type it in again. Nobody
// does that more than twice, which is how the numbers in the Inspector drifted
// so far from anything deliberate.
//
// So this window has a PUSH. While the game is running it can read what the
// hands are actually doing and write it into the prefab - which survives Stop,
// because a prefab is an asset and not a scene object.
//
//     press Play  ->  pick something up  ->  drag values in the Inspector
//                 ->  Push, here         ->  press Stop, and it is still there
//
// TWO SEPARATE PUSHES, because they are two separate scopes and confusing them
// is how you overwrite twenty items to fix one:
//
//   Push this ITEM's grip        writes the two hand points and ten finger
//                                curls into THAT loot prefab. Affects one item.
//
//   Push the CHARACTER settings  writes the eighteen PlayerCarryArms values
//                                into Player.prefab. Affects everything you
//                                pick up.
//
// ---- WHY IT LOOKS LIKE THE INSPECTOR ----
//
// Because it IS the Inspector's drawing code. Every row draws its fields with
// PropertyField over a SerializedObject, so the sliders, the ranges, the
// tooltips and the headers are the real ones off the class - not a
// hand-rebuilt imitation that drifts the first time a field is added.
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
        w.minSize = new Vector2(460f, 360f);
        w.Rescan();
    }

    const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

    class Row
    {
        public GameObject prefab;
        public Carryable item;
        public SerializedObject so;
        public bool expanded;
    }

    readonly List<Row> rows = new List<Row>();
    Vector2 scroll;
    string filter = "";
    bool onlyAuto = false;
    bool showLive = true;

    void OnEnable()
    {
        Rescan();

        // Play mode changes what this window can do, and a window that only
        // repaints when the mouse moves over it will show a stale "nothing
        // held" for as long as you are looking at it.
        EditorApplication.playModeStateChanged += _ => Repaint();
    }

    void OnInspectorUpdate()
    {
        // Ten times a second while playing, so the live panel tracks what is
        // actually in the hands without needing the mouse.
        if (Application.isPlaying) Repaint();
    }

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

            rows.Add(new Row { prefab = go, item = c, so = new SerializedObject(c) });
        }

        rows.Sort((a, b) => string.Compare(a.prefab.name, b.prefab.name,
                                           System.StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                Rescan();

            filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField);

            onlyAuto = GUILayout.Toggle(onlyAuto, "Only Auto",
                                        EditorStyles.toolbarButton, GUILayout.Width(74f));

            showLive = GUILayout.Toggle(showLive, "Live",
                                        EditorStyles.toolbarButton, GUILayout.Width(46f));
        }

        if (showLive) DrawLivePanel();

        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No prefabs with a Carryable component found. Press Rescan.",
                MessageType.Info);
            return;
        }

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

    // ------------------------------------------------------------------
    // THE LIVE PANEL - the half that makes play-mode tuning worth doing
    // ------------------------------------------------------------------

    void DrawLivePanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField(
                    "Press Play, pick something up, tune PlayerCarryArms in the " +
                    "Inspector, then push the result down here before you Stop.",
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            var arms = FindLiveArms();

            if (arms == null)
            {
                EditorGUILayout.LabelField("No PlayerCarryArms in the scene.",
                                           EditorStyles.miniLabel);
                return;
            }

            // ---- the character's own settings, which apply to everything ----
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Character settings", GUILayout.Width(130f));

                if (GUILayout.Button("Push to Player prefab"))
                    PushCharacterSettings(arms);
            }

            var item = arms.LiveItem;

            if (item == null)
            {
                EditorGUILayout.LabelField("Holding: nothing", EditorStyles.miniLabel);
                return;
            }

            var row = RowFor(item);

            EditorGUILayout.LabelField(
                "Holding: " + item.name + "   (" + item.Weight + ", " +
                (item.HasCustomGrip ? "Custom" : "Auto") + ")",
                EditorStyles.miniLabel);

            if (!arms.HasLiveGrips)
            {
                EditorGUILayout.LabelField("Hands are not placed yet.", EditorStyles.miniLabel);
                return;
            }

            if (row == null)
            {
                EditorGUILayout.HelpBox(
                    "This object could not be matched back to a prefab, so there " +
                    "is nowhere to save it. Spawned from code with no prefab " +
                    "link, most likely.",
                    MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("This item's grip", GUILayout.Width(130f));

                if (GUILayout.Button("Push into " + row.prefab.name))
                    PushItemGrip(arms, item, row);
            }

            EditorGUILayout.LabelField(
                "Pushing converts where the hands ACTUALLY are into the item's " +
                "own space and sets it to Custom - so it keeps exactly what you " +
                "are looking at, including the finger curls.",
                EditorStyles.wordWrappedMiniLabel);
        }
    }

    /// <summary>
    /// The local player's carry arms, whatever they are called this session.
    ///
    /// Searched rather than cached: entering play mode destroys and rebuilds
    /// everything, and a cached reference across that boundary is a stale
    /// pointer that reports "no player" forever.
    /// </summary>
    static PlayerCarryArms FindLiveArms()
    {
        foreach (var a in Object.FindObjectsByType<PlayerCarryArms>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var motor = a.GetComponentInParent<PlayerMotor>();
            if (motor != null && PlayerRegistry.IsLocalFor(motor)) return a;
        }

        // No local player identified - the first one is still more useful than
        // refusing to show anything, and in single-player testing it is the
        // right one anyway.
        return Object.FindFirstObjectByType<PlayerCarryArms>();
    }

    /// <summary>
    /// Match a scene object back to the prefab it came from.
    ///
    /// Tries the real prefab link first. Falls back to matching by name with
    /// "(Clone)" stripped, because loot instantiated at runtime does not always
    /// carry a link back to its source, and a workflow that silently refuses to
    /// save is worse than one that matches by name.
    /// </summary>
    Row RowFor(Carryable item)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);

        if (source != null)
            foreach (var r in rows)
                if (r.prefab == source) return r;

        string name = item.name.Replace("(Clone)", "").Trim();

        foreach (var r in rows)
            if (string.Equals(r.prefab.name, name,
                              System.StringComparison.OrdinalIgnoreCase)) return r;

        return null;
    }

    // ------------------------------------------------------------------
    // THE TWO PUSHES
    // ------------------------------------------------------------------

    /// <summary>
    /// Write the live hand placement into the item's prefab.
    ///
    /// Takes the WORLD points the component computed this frame and converts
    /// them through the item's own transform, so whatever produced them -
    /// measurement, an offset you dragged, a palm angle you nudged - is baked
    /// in as the item's Custom grip. What you were looking at is what gets
    /// saved.
    /// </summary>
    static void PushItemGrip(PlayerCarryArms arms, Carryable live, Row row)
    {
        var target = row.item;

        Undo.RecordObject(target, "Push grip to library");

        Transform t = live.transform;

        target.gripMode = Carryable.GripMode.Custom;

        target.leftGrip.used = arms.LiveLeftUsed;
        target.rightGrip.used = arms.LiveRightUsed;

        if (arms.LiveLeftUsed)
        {
            target.leftGrip.localPosition = t.InverseTransformPoint(arms.LiveLeftPosition);
            target.leftGrip.localEuler =
                (Quaternion.Inverse(t.rotation) * arms.LiveLeftRotation).eulerAngles;
        }

        if (arms.LiveRightUsed)
        {
            target.rightGrip.localPosition = t.InverseTransformPoint(arms.LiveRightPosition);
            target.rightGrip.localEuler =
                (Quaternion.Inverse(t.rotation) * arms.LiveRightRotation).eulerAngles;
        }

        // Fingers come from the character's current values, because that is
        // what you were watching close. If the item already had its own, they
        // are what the hands were using and this is a no-op.
        if (!live.HasCustomGrip)
        {
            Copy(target.leftGrip, arms);
            Copy(target.rightGrip, arms);
        }

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(row.prefab);

        row.so = new SerializedObject(target);   // the old one now shows stale values

        Debug.Log("[Grip] Pushed live grip into " + row.prefab.name +
                  " and set it to Custom. It survives Stop.");
    }

    static void Copy(Carryable.HandGrip g, PlayerCarryArms arms)
    {
        g.thumb = arms.thumbCurl;
        g.index = arms.indexCurl;
        g.middle = arms.middleCurl;
        g.ring = arms.ringCurl;
        g.little = arms.littleCurl;
    }

    /// <summary>
    /// Write the live character settings onto the Player prefab.
    ///
    /// Opened through PrefabUtility rather than edited in the scene, because
    /// the thing in the scene is an instance that stops existing the moment you
    /// press Stop - which is the entire problem this window exists to solve.
    /// </summary>
    static void PushCharacterSettings(PlayerCarryArms live)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        if (root == null)
        {
            Debug.LogError("[Grip] Could not open " + PlayerPrefabPath);
            return;
        }

        try
        {
            var target = root.GetComponentInChildren<PlayerCarryArms>(true);

            if (target == null)
            {
                Debug.LogError("[Grip] No PlayerCarryArms on the Player prefab - run " +
                               "Repair Player Prefab Components first.");
                return;
            }

            target.CopySettingsFrom(live);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Grip] Pushed PlayerCarryArms settings onto Player.prefab. " +
                      "They survive Stop.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------
    // ROWS - drawn with the Inspector's own code, not an imitation of it
    // ------------------------------------------------------------------

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
                                GUILayout.Width(58f));

                GUILayout.Label(item.HasCustomGrip ? "Custom" : "Auto",
                                EditorStyles.miniBoldLabel, GUILayout.Width(52f));

                if (item.HasCustomGrip &&
                    GUILayout.Button("Reseed", EditorStyles.miniButton, GUILayout.Width(56f)))
                {
                    Undo.RecordObject(item, "Reseed grip");
                    item.SeedGripsFromBounds(0.78f, 0.85f, 0.55f, 0.06f);
                    Save(row);
                }

                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    Selection.activeObject = row.prefab;
                    EditorGUIUtility.PingObject(row.prefab);
                }
            }

            if (!row.expanded) return;

            if (row.so == null) row.so = new SerializedObject(item);

            row.so.Update();

            EditorGUI.indentLevel++;

            // PropertyField, not hand-drawn fields: the sliders, ranges,
            // tooltips and headers are the real ones off Carryable, so adding
            // a field to the class shows up here with no work.
            Field(row.so, "gripMode");

            if (item.HasCustomGrip)
            {
                if (Implausible(item.leftGrip) || Implausible(item.rightGrip))
                    EditorGUILayout.HelpBox(
                        "These grip points are more than a metre from the item's " +
                        "origin - almost certainly seeded before the scale bug " +
                        "was fixed. Press Reseed.",
                        MessageType.Warning);

                Field(row.so, "leftGrip");
                Field(row.so, "rightGrip");
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Measured from bounds: " + item.WorldBounds.size.ToString("F2"),
                    EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;

            if (row.so.ApplyModifiedProperties())
                AssetDatabase.SaveAssetIfDirty(row.prefab);
        }
    }

    static void Field(SerializedObject so, string path)
    {
        var p = so.FindProperty(path);
        if (p != null) EditorGUILayout.PropertyField(p, true);
    }

    static void Save(Row row)
    {
        EditorUtility.SetDirty(row.item);
        AssetDatabase.SaveAssetIfDirty(row.prefab);
        row.so = new SerializedObject(row.item);
    }

    /// <summary>A hand more than a metre from the item's own origin is not a
    /// grip on it, whatever the numbers say.</summary>
    static bool Implausible(Carryable.HandGrip g) =>
        g.used && g.localPosition.magnitude > 1f;
}
