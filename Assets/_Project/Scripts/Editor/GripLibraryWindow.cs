// GripLibraryWindow.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/GripLibraryWindow.cs
// Open with: SAFE DEPOSIT / Player / Grip Library
//
// ========================================================================
// AN AUTHORING TOOL. NOT PART OF THE GAME.
//
// Nothing in here runs in a build. It edits data that lives on Carryable, and
// PlayerCarryArms reads that data whether this window has ever been opened or
// not. Worth stating at the top, because a tool that quietly becomes a second
// gameplay system is exactly the thing this project has been pulling apart.
//
//     PlayerCarry      what am I holding
//     Carryable        how is THIS item held
//     PlayerCarryArms  where do my hands go
//     HandFingerCurl   how closed are my fingers
//
// One question, one owner. This window only edits the middle one.
//
// ---- THE WORKFLOW ----
//
//     select item -> Play -> grab it -> adjust -> see it change
//                 -> Save to prefab -> Stop -> still saved
//
// ---- WHY THE LIVE PANEL EDITS THE CLONE, NOT THE PREFAB ----
//
// This is the part that did not work before and the reason is worth writing
// down, because it looked like it worked.
//
// LootSpawner creates items with runtime Object.Instantiate, which produces a
// plain clone carrying its OWN copy of the grip data. PlayerCarryArms reads
// that clone. So editing the prefab row while playing changed the asset and
// moved nothing on screen - a live preview that previewed nothing, which is
// worse than no preview because you trust it.
//
// So while playing, the panel binds to the HELD CLONE. Every field is live,
// because PlayerCarryArms re-reads it every frame. Save then copies clone ->
// prefab.
//
// ---- AND WHY IT SAVES FIELDS, NOT COMPUTED POINTS ----
//
// The old save wrote the world positions the hands were actually at. Those
// already had the character's leftHandOffset added and leftPalmEuler
// multiplied in, so saving baked them into the item - and the next pickup
// applied them AGAIN. Every press of the button rotated that item's saved
// palms another 90 degrees.
//
// CopyGripFrom copies the fields as they stand. Saving twice does nothing the
// second time, which is what "save" should mean.
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
        w.minSize = new Vector2(470f, 380f);
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

    // Bound to the HELD CLONE while playing. Rebuilt whenever what is in the
    // hands changes, because a SerializedObject pointing at a destroyed clone
    // throws the moment it is drawn.
    SerializedObject liveSo;
    Carryable liveBound;

    Vector2 scroll;
    string filter = "";
    bool onlyAuto = false;
    bool showLive = true;

    void OnEnable()
    {
        Rescan();
        EditorApplication.playModeStateChanged += _ => { liveSo = null; liveBound = null; Repaint(); };
    }

    void OnInspectorUpdate()
    {
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

        EditorGUILayout.LabelField(
            "Saved grips, straight off each prefab. Auto items are measured " +
            "from their own bounds and need no setup.",
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

    // ------------------------------------------------------------------
    // LIVE - the held item itself, editable, with a save
    // ------------------------------------------------------------------

    void DrawLivePanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField(
                    "Press Play and pick something up. Its grip appears here, " +
                    "fully editable, and every change shows on the hands the " +
                    "same frame. Save to prefab when it looks right.",
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

            var item = arms.LiveItem;

            if (item == null)
            {
                EditorGUILayout.LabelField("Holding: nothing", EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Character defaults", GUILayout.Width(120f));
                    if (GUILayout.Button("Save to Player prefab"))
                        PushCharacterSettings(arms);
                }

                return;
            }

            // Rebound whenever the held item changes. A SerializedObject
            // pointing at a dropped clone throws when drawn.
            if (liveBound != item)
            {
                liveBound = item;
                liveSo = new SerializedObject(item);
            }

            GameObject source = SourcePrefabOf(item);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "Holding: " + item.name + "  (" + item.Weight + ")",
                    EditorStyles.miniBoldLabel);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(source == null))
                {
                    var was = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);

                    if (GUILayout.Button(source != null
                                             ? "Save to " + source.name
                                             : "Save to prefab",
                                         GUILayout.Width(170f)))
                        SaveToPrefab(item, source);

                    GUI.backgroundColor = was;
                }
            }

            if (source == null)
                EditorGUILayout.HelpBox(
                    "This object does not know which prefab it came from, so " +
                    "there is nowhere to save it. LootSpawner stamps that on " +
                    "spawn - an item placed in the scene by hand will not have " +
                    "it, and can be edited directly in its own Inspector " +
                    "instead.",
                    MessageType.Warning);

            WarnIfImplausible(item);

            // ---- WHY AN ELBOW ANGLE CAN DO NOTHING ----
            //
            // An arm stretched to within 3% of its length has no bend left,
            // and an elbow with no bend sits ON the shoulder-to-hand line
            // where nothing can steer it. The cause is always a hand target
            // too far away - a GRIP problem - so it is reported here rather
            // than compensated for in the solver, which would hide it.
            if (arms.LeftArmStraight || arms.RightArmStraight)
                EditorGUILayout.HelpBox(
                    (arms.LeftArmStraight && arms.RightArmStraight ? "BOTH arms are"
                     : arms.LeftArmStraight ? "The LEFT arm is" : "The RIGHT arm is") +
                    " stretched almost straight, so the elbow has no bend left " +
                    "and no Elbow Angle can move it. Fix the GRIP first - the " +
                    "hand target is further away than the arm is long.",
                    MessageType.Warning);

            liveSo.Update();

            EditorGUI.indentLevel++;
            DrawGripFields(liveSo);
            EditorGUI.indentLevel--;

            // No SetDirty and no asset save: this is a scene clone. The point
            // is that PlayerCarryArms re-reads it every frame, so the change
            // is on the hands immediately - and the explicit Save is what
            // makes it outlive Stop.
            liveSo.ApplyModifiedProperties();

            // Draw the solve in the SCENE VIEW - never in the game. This is
            // what makes "is the hint on the right side of the arm" a thing
            // you look at rather than argue about.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();

                bool grips = GUILayout.Toggle(arms.drawGrips, "Show hand gizmos",
                                              EditorStyles.miniButton);
                bool elbows = GUILayout.Toggle(arms.drawElbows, "Show elbow gizmos",
                                               EditorStyles.miniButton);

                if (EditorGUI.EndChangeCheck())
                {
                    arms.drawGrips = grips;
                    arms.drawElbows = elbows;
                    SceneView.RepaintAll();
                }
            }

            if (arms.drawElbows)
                EditorGUILayout.LabelField(
                    "Scene view: blue = shoulder, yellow = hand goal, magenta = " +
                    "elbow hint, and the line between shoulder and hand is the " +
                    "axis the angle sweeps around.",
                    EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seed from bounds"))
                {
                    item.gripMode = Carryable.GripMode.Custom;
                    item.SeedGripsFromBounds();
                    liveSo = new SerializedObject(item);
                }

                if (GUILayout.Button("Mirror left onto right"))
                {
                    Mirror(item.leftGrip, item.rightGrip);
                    liveSo = new SerializedObject(item);
                }
            }

            EditorGUILayout.LabelField(
                "Editing the item in your hands. Changes are live but belong " +
                "to this one spawned copy - Save writes them to the prefab so " +
                "every future one starts here.",
                EditorStyles.wordWrappedMiniLabel);
        }
    }

    static PlayerCarryArms FindLiveArms()
    {
        foreach (var a in Object.FindObjectsByType<PlayerCarryArms>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var motor = a.GetComponentInParent<PlayerMotor>();
            if (motor != null && PlayerRegistry.IsLocalFor(motor)) return a;
        }

        return Object.FindFirstObjectByType<PlayerCarryArms>();
    }

    /// <summary>
    /// Which prefab this instance came from.
    ///
    /// The stamp LootSpawner writes at spawn is the answer, and it is exact.
    /// PrefabUtility is tried as a fallback for objects placed in a scene by
    /// hand, which ARE real prefab instances.
    ///
    /// Matching by name is deliberately gone: it worked until two prefabs
    /// shared a name or one was renamed, and then it saved your tuning onto
    /// the wrong asset without saying so.
    /// </summary>
    static GameObject SourcePrefabOf(Carryable item)
    {
        if (item.sourcePrefab != null) return item.sourcePrefab;

        var src = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
        return src as GameObject;
    }

    // ------------------------------------------------------------------
    // SAVE
    // ------------------------------------------------------------------

    /// <summary>
    /// Copy the held clone's grip onto its prefab, verbatim.
    ///
    /// CopyGripFrom takes the FIELDS, not the world points the hands happened
    /// to be at - so saving twice does nothing the second time. The old
    /// version saved computed points that already had the character's palm
    /// angle folded in, and the next pickup folded it in again.
    /// </summary>
    void SaveToPrefab(Carryable live, GameObject prefab)
    {
        if (prefab == null) return;

        var target = prefab.GetComponent<Carryable>();

        if (target == null)
        {
            Debug.LogError("[Grip] " + prefab.name + " has no Carryable to save onto.");
            return;
        }

        Undo.RecordObject(target, "Save grip to prefab");

        target.CopyGripFrom(live);

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(prefab);

        foreach (var r in rows)
            if (r.prefab == prefab) r.so = new SerializedObject(target);

        Debug.Log("[Grip] Saved " + live.name + "'s grip to " + prefab.name +
                  " (" + target.gripMode + "). It survives Stop.");
    }

    /// <summary>
    /// Write the live character defaults onto the Player prefab.
    ///
    /// Still needs the LoadPrefabContents dance, unlike an item, because this
    /// component lives on a scene instance that stops existing on Stop.
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

            Debug.Log("[Grip] Character defaults saved to Player.prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------
    // ROWS
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
                    item.SeedGripsFromBounds();
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

            WarnIfImplausible(item);

            DrawGripFields(row.so);

            EditorGUI.indentLevel--;

            if (row.so.ApplyModifiedProperties())
                AssetDatabase.SaveAssetIfDirty(row.prefab);
        }
    }

    // ------------------------------------------------------------------
    // THE FIELDS, IN SECTIONS
    //
    // Three groups, because they answer three different questions and mixing
    // them into one flat list is how you end up scrolling past the one you
    // meant to change:
    //
    //     HAND GRIP           where the hands are and what the fingers do
    //     ELBOW / ARM         how the arm BENDS to get there
    //     HELD ITEM TRANSFORM where the object itself sits
    //
    // Drawn once and shared by the live clone and the prefab rows, so the two
    // can never show a different set of controls - and drawn with
    // PropertyField, so the sliders, ranges, tooltips and headers are the real
    // ones off Carryable and a new field appears here with no work.
    // ------------------------------------------------------------------

    static readonly Dictionary<string, bool> folded = new Dictionary<string, bool>();

    static bool Section(string title, bool openByDefault = true)
    {
        if (!folded.TryGetValue(title, out bool open)) open = openByDefault;

        EditorGUILayout.Space(2f);
        open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
        folded[title] = open;

        return open;
    }

    static void DrawGripFields(SerializedObject so)
    {
        Field(so, "gripMode");

        bool custom = so.FindProperty("gripMode") != null &&
                      so.FindProperty("gripMode").enumValueIndex ==
                      (int)Carryable.GripMode.Custom;

        // ---- HAND GRIP ----
        if (Section("HAND GRIP"))
        {
            EditorGUI.indentLevel++;

            if (custom)
            {
                Hand(so, "leftGrip", "Left Hand");
                Hand(so, "rightGrip", "Right Hand");
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Auto: the hands are measured from this item's bounds. " +
                    "Switch Grip Mode to Custom to place them by hand, or tick " +
                    "Override Measure below to keep Auto but tune its numbers " +
                    "for this item only.",
                    MessageType.None);
            }

            Field(so, "overrideMeasure");

            var over = so.FindProperty("overrideMeasure");
            if (over != null && over.boolValue) Field(so, "measure");

            EditorGUI.indentLevel--;
        }

        // ---- ELBOW ----
        //
        // Inside the hand foldouts in the Inspector, because they belong to a
        // hand - but called out as its own section here, because "the arm
        // bends wrong" is a different thought from "the hand is in the wrong
        // place" and you go looking for it separately.
        if (Section("ELBOW / ARM POSITION  (parked)", false))
        {
            EditorGUI.indentLevel++;

            if (!custom)
            {
                EditorGUILayout.HelpBox(
                    "Elbow hints live on each hand's grip, which Auto mode " +
                    "does not use. Switch Grip Mode to Custom to steer the " +
                    "elbows.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "PARKED - come back to this once the hands are right.\n\n" +
                    "It is off on every item and changes nothing while Use " +
                    "Elbow Hint is unticked. The controls are left here rather " +
                    "than removed so the work is not lost.",
                    MessageType.Info);

                EditorGUILayout.LabelField(
                    "The elbow swings around the shoulder-to-hand axis, which " +
                    "is the only direction it can move once the hand is " +
                    "placed. The hand, its rotation and the item all stay " +
                    "exactly where they are.\n\n" +
                    "0 = down    90 = out    180 = up    -90 = in\n\n" +
                    "Body-relative, so the same number means the same thing on " +
                    "both arms: a symmetric grip is the same value twice.",
                    EditorStyles.wordWrappedMiniLabel);

                Elbow(so, "leftGrip", "Left Elbow");
                Elbow(so, "rightGrip", "Right Elbow");
            }

            EditorGUI.indentLevel--;
        }

        // ---- HELD ITEM TRANSFORM ----
        if (Section("HELD ITEM TRANSFORM"))
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(
                "Offsets from the hold anchor, in the BODY'S space. Zero " +
                "leaves the item where its weight class puts it.",
                EditorStyles.wordWrappedMiniLabel);

            Field(so, "itemPositionOffset");
            Field(so, "itemRotationOffset");

            EditorGUI.indentLevel--;
        }
    }

    // ------------------------------------------------------------------
    // ONE HAND, SPLIT BETWEEN TWO SECTIONS
    //
    // "why there is two function in grip library with same name elbow"
    //
    // Because HAND GRIP used to draw each HandGrip whole with PropertyField,
    // and a HandGrip CONTAINS its elbow fields - so they appeared there, and
    // then again in the ELBOW section drawing the same three properties. Two
    // controls, one value, and no way to tell from looking which one was real.
    // They both were.
    //
    // So the hand is drawn field by field now, and the split is explicit:
    // everything except the elbow here, the elbow only in its own section.
    // Slightly more code, and it is the only way the section layout can mean
    // anything.
    // ------------------------------------------------------------------

    static readonly string[] HandFields =
    {
        "used", "localPosition", "localEuler", "palmRotation",
        "thumb", "index", "middle", "ring", "little"
    };

    static void Hand(SerializedObject so, string handProperty, string label)
    {
        var hand = so.FindProperty(handProperty);
        if (hand == null) return;

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        foreach (string f in HandFields)
        {
            var prop = hand.FindPropertyRelative(f);
            if (prop != null) EditorGUILayout.PropertyField(prop, true);
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>The elbow, and ONLY the elbow. Its own section, because "the
    /// arm bends wrong" is a different thought from "the hand is in the wrong
    /// place" and you go looking for it separately.</summary>
    static void Elbow(SerializedObject so, string handProperty, string label)
    {
        var hand = so.FindProperty(handProperty);
        if (hand == null) return;

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        var use = hand.FindPropertyRelative("useElbowHint");
        if (use != null) EditorGUILayout.PropertyField(use);

        if (use == null || use.boolValue)
        {
            var angle = hand.FindPropertyRelative("elbowAngle");
            var w = hand.FindPropertyRelative("elbowWeight");

            if (angle != null) EditorGUILayout.PropertyField(angle);
            if (w != null) EditorGUILayout.PropertyField(w);
        }

        EditorGUI.indentLevel--;
    }

    static void Field(SerializedObject so, string path)
    {
        var p = so.FindProperty(path);
        if (p != null) EditorGUILayout.PropertyField(p, true);
    }

    static void Mirror(Carryable.HandGrip from, Carryable.HandGrip to)
    {
        to.localPosition = new Vector3(-from.localPosition.x,
                                        from.localPosition.y,
                                        from.localPosition.z);

        to.localEuler = new Vector3(from.localEuler.x,
                                    -from.localEuler.y,
                                    -from.localEuler.z);

        to.palmRotation = new Vector3(from.palmRotation.x,
                                      -from.palmRotation.y,
                                      -from.palmRotation.z);

        // The elbow angle COPIES now. It used to negate, because the swing was
        // measured around each arm's own axis and the two arms are mirror
        // images - so the same number swung one elbow out and the other in.
        //
        // The angle is body-relative now (0 down, 90 out, 180 up, -90 in) with
        // the handedness folded into the frame, so the same number means the
        // same thing on both arms and a straight copy is the mirror.
        to.useElbowHint = from.useElbowHint;
        to.elbowAngle = from.elbowAngle;
        to.elbowWeight = from.elbowWeight;

        to.thumb = from.thumb;
        to.index = from.index;
        to.middle = from.middle;
        to.ring = from.ring;
        to.little = from.little;
        to.used = from.used;
    }

    static void Save(Row row)
    {
        EditorUtility.SetDirty(row.item);
        AssetDatabase.SaveAssetIfDirty(row.prefab);
        row.so = new SerializedObject(row.item);
    }

    /// <summary>
    /// A hand more than a metre from the item is not a grip on it.
    ///
    /// Measured in METRES, through the item's transform. The first version
    /// compared localPosition.magnitude directly, which is local units - and
    /// on a prefab scaled to 0.6 that reads 0.88 for a hand only 0.53m away,
    /// so it cried wolf on perfectly good grips and stayed quiet on bad ones.
    /// The same confusion between local units and metres produced the original
    /// broken seed.
    /// </summary>
    static bool Implausible(Carryable item, Carryable.HandGrip g)
    {
        if (!g.used) return false;

        Vector3 world = item.transform.TransformPoint(g.localPosition);
        return Vector3.Distance(world, item.transform.position) > 1f;
    }

    /// <summary>
    /// Say so, loudly, and name the hand.
    ///
    /// The old version only appeared on the prefab rows, and only said "these
    /// grip points" - so a grip corrupted on BOTH hands read as a one-hand
    /// problem, and the fix went onto the wrong one. Naming each hand and its
    /// distance turns "the right hand looks weird" into a number you can
    /// disbelieve.
    ///
    /// Two ways to get here, both now closed at the source:
    ///
    ///   the local-space seed, which multiplied every distance by 1/scale
    ///   the old save, which wrote COMPUTED world points with the character's
    ///   palm angle already folded in - hence the arbitrary eulers that come
    ///   with these, nothing like the mirrored 0/90/0 a seed produces
    ///
    /// Both are fixed, but the bad numbers are already sitting in prefabs and
    /// nothing rewrites saved data behind your back.
    /// </summary>
    static float Metres(Carryable item, Carryable.HandGrip g) =>
        Vector3.Distance(item.transform.TransformPoint(g.localPosition),
                         item.transform.position);

    static void WarnIfImplausible(Carryable item)
    {
        if (!item.HasCustomGrip) return;

        bool l = Implausible(item, item.leftGrip);
        bool r = Implausible(item, item.rightGrip);

        if (!l && !r) return;

        string which =
            l && r ? "BOTH hands are"
          : l ? "The LEFT hand is"
          : "The RIGHT hand is";

        string how =
            (l ? "  left " + Metres(item, item.leftGrip).ToString("0.00") + "m" : "") +
            (r ? "  right " + Metres(item, item.rightGrip).ToString("0.00") + "m" : "");

        EditorGUILayout.HelpBox(
            which + " placed more than a metre from this item, which is not a " +
            "grip on it - the arm just stretches toward a point in space." + how +
            "\n\nThese were written by the two bugs that are now fixed: the " +
            "local-space seed, and the save that wrote computed world points. " +
            "Nothing rewrites saved data on its own, so press Seed From Bounds " +
            "and then Save.",
            MessageType.Warning);
    }
}
