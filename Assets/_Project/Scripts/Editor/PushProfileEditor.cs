// PushProfileEditor.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/PushProfileEditor.cs
//
// ========================================================================
// TEST THE GESTURE FROM THE ASSET YOU ARE EDITING.
//
// The same Test button the Push Library has, put on the profile itself,
// because this is where you are already looking when you find the value worth
// changing - the same reasoning that put "Push these settings to Player
// prefab" on PlayerCarryArms as well as in the Grip Library.
//
// The important thing this says out loud is the one people get wrong: there is
// NO SAVE BUTTON, and that is not an omission. A PushProfile is an asset, so
// every change here is already written to the file. Play mode does not take it
// away.
// ========================================================================

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PushProfile))]
public class PushProfileEditor : Editor
{
    static bool showHelp = true;

    public override void OnInspectorGUI()
    {
        var p = (PushProfile)target;

        showHelp = EditorGUILayout.Foldout(showHelp, "How to tune a push", true);

        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "Edit -> Test -> adjust -> Test again. There is no save step: " +
                "this is an asset, so every change is already on disk and " +
                "survives Stop.\n\n" +
                "IN THIS ORDER:\n\n" +
                "1. Press Play. Press Test below - the gesture fires where you " +
                "stand, no door needed.\n\n" +
                "2. Too fast or too slow? Duration, Hold, Return. All in real " +
                "seconds.\n\n" +
                "3. Hands going the wrong distance? Left/Right Offset Z is " +
                "forward. These are OFFSETS from the resting pose, so 0 means " +
                "'do not move' and the gesture can never snap.\n\n" +
                "4. Palms wrong? Palm Rotation first - it is mirrored, so it " +
                "turns both hands as one gesture. Only use a hand's own " +
                "Rotation to correct one of them afterwards.\n\n" +
                "5. Looks like a punch? The fingers are too curled. A shove is " +
                "an open hand - keep them near zero.\n\n" +
                "Assign it to an object with the Assign button in the Push " +
                "Library, or by adding a Pushable component and dropping this " +
                "in.",
                MessageType.None);
        }

        var wide = new GUIStyle(GUI.skin.button) { fixedHeight = 26f };

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            var was = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);

            if (GUILayout.Button(Application.isPlaying
                                     ? "Test this push now"
                                     : "Test this push now  (press Play first)", wide))
                Test(p);

            GUI.backgroundColor = was;
        }

        if (Application.isPlaying)
            EditorGUILayout.HelpBox(
                "Changes here are live. Drag a slider mid-swing and it lands on " +
                "the hands the same frame - the profile is read every frame, " +
                "never cached when the shove starts.",
                MessageType.Info);

        EditorGUILayout.Space();

        DrawDefaultInspector();

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Mirror left onto right"))
            {
                Undo.RecordObject(p, "Mirror push hand");
                p.left.MirrorInto(p.right);
                EditorUtility.SetDirty(p);
            }

            if (GUILayout.Button("Restore recommended"))
            {
                Undo.RecordObject(p, "Restore push profile");
                p.RestoreRecommended();
                EditorUtility.SetDirty(p);
            }
        }

        if (GUILayout.Button("Assign to selected objects"))
            Assign(p);
    }

    /// <summary>
    /// Fire it through the real code path.
    ///
    /// PlayerPush.TestSwing rather than anything simulated here - a preview
    /// running different code from the game is a preview that eventually lies
    /// about something that matters.
    /// </summary>
    static void Test(PushProfile p)
    {
        PlayerPush push = null;

        foreach (var candidate in Object.FindObjectsByType<PlayerPush>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            push = candidate;
            if (PlayerRegistry.IsLocalFor(candidate)) break;
        }

        if (push == null)
        {
            Debug.LogWarning("[Push] No PlayerPush in the scene to test with.");
            return;
        }

        push.TestSwing(p);
    }

    static void Assign(PushProfile p)
    {
        var targets = Selection.gameObjects;

        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Nothing selected",
                "Select the object or prefab that should use '" + p.displayName +
                "' in the Hierarchy or Project, then press Assign again.",
                "OK");
            return;
        }

        int n = 0;

        foreach (var go in targets)
        {
            var push = go.GetComponent<Pushable>();
            if (push == null) push = Undo.AddComponent<Pushable>(go);

            Undo.RecordObject(push, "Assign push profile");
            push.profile = p;
            EditorUtility.SetDirty(push);

            if (PrefabUtility.IsPartOfPrefabAsset(go))
                AssetDatabase.SaveAssetIfDirty(go);

            n++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Push] '" + p.displayName + "' assigned to " + n + " object(s).");
    }
}
