// PushLibraryWindow.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/PushLibraryWindow.cs
// Open with: SAFE DEPOSIT / Player / Push Library
//
// ========================================================================
// EVERY PUSH PROFILE, EDITED AND TESTED IN ONE PLACE.
//
//     Edit -> Live Preview -> Test -> Save -> Assign -> Reuse
//
// Built on the Grip Library's shape on purpose, because that workflow works
// and a second one that looks different would just be a second thing to learn:
//
//   * a project SCAN, not a registry, so it cannot fall out of date
//   * rows drawn with PropertyField over a SerializedObject, so the sliders,
//     ranges, tooltips and headers are the real ones off PushProfile and
//     adding a field to that class shows up here with no work
//   * a Live panel that repaints while playing
//   * Select / assign buttons that put you where you need to be
//
// ---- WHERE IT IS SIMPLER THAN THE GRIP LIBRARY, AND WHY ----
//
// There is no "Push to prefab" button here, and its absence is the feature.
//
// Grips needed one because their data lives on a scene object, and Unity
// discards play-mode changes on Stop - so a value found by feel had to be
// deliberately copied onto an asset before you stopped, or it was gone.
//
// A PushProfile IS an asset. Dragging its slider during play mode edits the
// FILE. There is nothing to push, nothing to remember to click before you
// stop, and no way to lose a tuning session to habit. Same reason
// FirstPersonViewmodelSettings ended that complaint for the viewmodel.
//
// ---- AND WHERE IT ADDS SOMETHING GRIPS DID NOT NEED ----
//
// TEST. A grip is on screen for as long as you hold the crate, so you can
// stare at it. A shove is over in half a second, and the old way to see one
// was to find something pushable, aim, and press G - which makes tuning a
// walk rather than an edit. The Test button fires the gesture where you stand,
// with the profile you are looking at, ignoring cooldown and targets.
// ========================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PushLibraryWindow : EditorWindow
{
    [MenuItem("SAFE DEPOSIT/Player/Push Library")]
    public static void Open()
    {
        var w = GetWindow<PushLibraryWindow>("Push Library");
        w.minSize = new Vector2(460f, 380f);
        w.Rescan();
    }

    class Row
    {
        public PushProfile profile;
        public SerializedObject so;
        public bool expanded;
    }

    readonly List<Row> rows = new List<Row>();
    Vector2 scroll;
    string filter = "";
    bool showLive = true;

    void OnEnable()
    {
        Rescan();
        EditorApplication.playModeStateChanged += _ => Repaint();
    }

    void OnInspectorUpdate()
    {
        // Ten times a second while playing, so the live panel tracks the shove
        // rather than waiting for the mouse to move over the window.
        if (Application.isPlaying) Repaint();
    }

    /// <summary>
    /// Find every PushProfile in the project.
    ///
    /// Scanned, not registered - a list somebody has to remember to add to is
    /// a list that will be wrong within a week. Same decision as the Grip
    /// Library, for the same reason.
    /// </summary>
    void Rescan()
    {
        rows.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:PushProfile"))
        {
            var p = AssetDatabase.LoadAssetAtPath<PushProfile>(
                        AssetDatabase.GUIDToAssetPath(guid));

            if (p != null)
                rows.Add(new Row { profile = p, so = new SerializedObject(p) });
        }

        rows.Sort((a, b) => string.Compare(a.profile.name, b.profile.name,
                                           System.StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                Rescan();

            if (GUILayout.Button("New Profile", EditorStyles.toolbarButton,
                                 GUILayout.Width(84f)))
                CreateProfile();

            filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField);

            showLive = GUILayout.Toggle(showLive, "Live",
                                        EditorStyles.toolbarButton, GUILayout.Width(46f));
        }

        if (showLive) DrawLivePanel();

        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No push profiles yet. Press New Profile to make one - it is a " +
                "normal asset and lands in Assets/_Project/Settings/PushProfiles.",
                MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (var row in rows)
        {
            if (row.profile == null) continue;

            if (!string.IsNullOrEmpty(filter) &&
                row.profile.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                row.profile.displayName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            DrawRow(row);
        }

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------
    // LIVE
    // ------------------------------------------------------------------

    void DrawLivePanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField(
                    "Press Play, then use Test on any row below to fire that " +
                    "gesture where you stand. Every value you change is saved " +
                    "as you change it - a profile is an asset, so Stop does not " +
                    "take it away.",
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            var push = FindLivePush();

            if (push == null)
            {
                EditorGUILayout.LabelField("No PlayerPush in the scene.",
                                           EditorStyles.miniLabel);
                return;
            }

            var active = push.ActiveProfile;

            EditorGUILayout.LabelField(
                "Swing: " + (push.PushProgress >= 0f
                             ? Mathf.RoundToInt(push.PushProgress * 100f) + "%"
                             : "idle") +
                "    Profile: " + (active != null ? active.name : "(default)"),
                EditorStyles.miniLabel);

            EditorGUILayout.LabelField(
                "Drag any slider below while a shove is in the air and it " +
                "changes on the hands the same frame - the profile is read " +
                "every frame, never cached at swing start.",
                EditorStyles.wordWrappedMiniLabel);
        }
    }

    static PlayerPush FindLivePush()
    {
        foreach (var p in Object.FindObjectsByType<PlayerPush>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (PlayerRegistry.IsLocalFor(p)) return p;

        return Object.FindFirstObjectByType<PlayerPush>();
    }

    // ------------------------------------------------------------------
    // ROWS
    // ------------------------------------------------------------------

    void DrawRow(Row row)
    {
        var p = row.profile;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                row.expanded = EditorGUILayout.Foldout(
                    row.expanded, p.name + "   -   " + p.displayName, true);

                GUILayout.FlexibleSpace();

                GUILayout.Label(p.TotalLength.ToString("0.00") + "s",
                                EditorStyles.miniLabel, GUILayout.Width(44f));

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Test", EditorStyles.miniButton,
                                         GUILayout.Width(46f)))
                        Test(p);
                }

                if (GUILayout.Button("Assign", EditorStyles.miniButton,
                                     GUILayout.Width(52f)))
                    AssignToSelection(p);

                if (GUILayout.Button("Select", EditorStyles.miniButton,
                                     GUILayout.Width(52f)))
                {
                    Selection.activeObject = p;
                    EditorGUIUtility.PingObject(p);
                }
            }

            if (!row.expanded) return;

            if (row.so == null) row.so = new SerializedObject(p);

            row.so.Update();

            EditorGUI.indentLevel++;

            // The Inspector's own drawing code, not an imitation of it.
            foreach (string f in new[] { "displayName", "notes", "left", "right",
                                         "spread", "palmRotation",
                                         "duration", "hold", "returnTime" })
            {
                var prop = row.so.FindProperty(f);
                if (prop != null) EditorGUILayout.PropertyField(prop, true);
            }

            EditorGUI.indentLevel--;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mirror left onto right"))
                {
                    Undo.RecordObject(p, "Mirror push hand");
                    p.left.MirrorInto(p.right);
                    EditorUtility.SetDirty(p);
                    row.so = new SerializedObject(p);
                }

                if (GUILayout.Button("Restore recommended"))
                {
                    Undo.RecordObject(p, "Restore push profile");
                    p.RestoreRecommended();
                    EditorUtility.SetDirty(p);
                    row.so = new SerializedObject(p);
                }
            }

            // A ScriptableObject is an asset, so this write IS the save. There
            // is no prefab dance and nothing to remember to press before Stop.
            if (row.so.ApplyModifiedProperties())
                AssetDatabase.SaveAssetIfDirty(p);
        }
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Fire this gesture where the player is standing.
    ///
    /// Goes through PlayerPush.TestSwing rather than simulating anything here,
    /// so what you are watching is the real code path with the real timing -
    /// a preview that runs different code from the game is a preview that
    /// eventually lies.
    /// </summary>
    static void Test(PushProfile p)
    {
        var push = FindLivePush();

        if (push == null)
        {
            Debug.LogWarning("[Push] No PlayerPush in the scene to test with.");
            return;
        }

        push.TestSwing(p);
    }

    /// <summary>
    /// Put this profile on whatever is selected, adding Pushable if needed.
    ///
    /// Works on prefab assets and on scene objects, because both are things
    /// you might have selected when you decide a door should shove like this.
    /// </summary>
    static void AssignToSelection(PushProfile p)
    {
        var targets = Selection.gameObjects;

        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Nothing selected",
                "Select the object or prefab that should use '" + p.displayName +
                "', then press Assign again.",
                "OK");
            return;
        }

        int n = 0;

        foreach (var go in targets)
        {
            var push = go.GetComponent<Pushable>();

            if (push == null)
                push = Undo.AddComponent<Pushable>(go);

            Undo.RecordObject(push, "Assign push profile");
            push.profile = p;

            EditorUtility.SetDirty(push);

            // A prefab ASSET has no scene to carry the change, so it has to be
            // written back explicitly.
            if (PrefabUtility.IsPartOfPrefabAsset(go))
                AssetDatabase.SaveAssetIfDirty(go);

            n++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Push] '" + p.displayName + "' assigned to " + n + " object(s).");
    }

    void CreateProfile()
    {
        const string dir = "Assets/_Project/Settings/PushProfiles";

        if (!AssetDatabase.IsValidFolder(dir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");

            AssetDatabase.CreateFolder("Assets/_Project/Settings", "PushProfiles");
        }

        var p = ScriptableObject.CreateInstance<PushProfile>();
        p.displayName = "New Push";

        string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/PushProfile.asset");

        AssetDatabase.CreateAsset(p, path);
        AssetDatabase.SaveAssets();

        Rescan();

        Selection.activeObject = p;
        EditorGUIUtility.PingObject(p);
    }
}
