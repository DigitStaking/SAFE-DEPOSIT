// PlayerCarryArmsEditor.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/PlayerCarryArmsEditor.cs
//
// ========================================================================
// A WAY BACK, AND A WAY TO TEST.
//
// "parametre are different i don't know how i can test and fill this"
//
// Two separate problems in one sentence, and the second is the real one.
//
// THE VALUES DRIFT. Eighteen numbers, several of them sliders, and nothing
// about looking at 0.668 tells you whether it was chosen or dragged past. So
// there is one button that puts every one of them back to the values that are
// known to work.
//
// NOTHING HERE NEEDS FILLING IN. That is worth stating plainly, because the
// question assumes the opposite. Auto mode measures every item from its own
// bounds and needs no per-item setup at all - a new prop dropped into the
// level is grippable immediately. The per-item Custom grips exist for the
// handful of things measurement gets wrong, not as a table to be filled in
// before anything works.
//
// THE ORDER TO TEST IN is the part that was missing, so it is written on the
// component now rather than living in a chat log.
//
// AND THE VALUES CAN BE KEPT. Unity discards play-mode changes on Stop, which
// is why these numbers drifted in the first place - the only way to hold onto
// something found by feel was to write it on paper, stop, and type it back.
// While playing there is now a button that writes them onto the PREFAB, which
// is an asset and survives Stop.
// ========================================================================

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerCarryArms))]
public class PlayerCarryArmsEditor : Editor
{
    static bool showHelp = true;

    public override void OnInspectorGUI()
    {
        var arms = (PlayerCarryArms)target;

        showHelp = EditorGUILayout.Foldout(showHelp, "How to test this", true);

        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "You do not have to fill anything in. Auto mode measures every " +
                "item from its own bounds - a new prop is grippable with no " +
                "setup.\n\n" +
                "TO TEST, IN THIS ORDER:\n\n" +
                "1. Tick Draw Grips, press Play, pick something up, and press P " +
                "for third person. Two spheres appear where the hands are being " +
                "SENT, each with a short line showing which way the palm faces.\n\n" +
                "2. If the spheres are in the wrong PLACE, the position numbers " +
                "are wrong: Grip Height On Box first, then Grip Width.\n\n" +
                "3. If the spheres are right but the hands look wrong, it is the " +
                "ANGLE: nudge Left/Right Palm Euler one axis at a time, 15 " +
                "degrees at a time. This is the number that depends on your rig " +
                "and cannot be derived.\n\n" +
                "4. Fingers not closing at all? Curl Fingers must be ticked, and " +
                "HandFingerCurl must be on this same object.\n\n" +
                "Every one of these can be dragged WHILE PLAYING. Unity throws " +
                "play-mode changes away on Stop, so when you find values you " +
                "like, press 'Push these settings to Player.prefab' BEFORE you " +
                "stop - the button only appears while playing.",
                MessageType.None);
        }

        // ---- the one that matters when things have drifted ----
        var fingers = arms.GetComponent<HandFingerCurl>();

        if (fingers == null)
        {
            EditorGUILayout.HelpBox(
                "No HandFingerCurl on this object, so the fingers cannot close - " +
                "the hands will be placed correctly but stay flat, which reads " +
                "as pushing the item rather than holding it.",
                MessageType.Warning);

            if (GUILayout.Button("Add HandFingerCurl"))
            {
                Undo.AddComponent<HandFingerCurl>(arms.gameObject);
            }
        }
        else if (!arms.curlFingers)
        {
            EditorGUILayout.HelpBox(
                "Curl Fingers is off, so the fingers stay straight no matter " +
                "what the per-finger values below say.",
                MessageType.Warning);

            if (GUILayout.Button("Turn Curl Fingers on"))
            {
                Undo.RecordObject(arms, "Curl fingers");
                arms.curlFingers = true;
                EditorUtility.SetDirty(arms);
            }
        }

        EditorGUILayout.Space();

        var wide = new GUIStyle(GUI.skin.button) { fixedHeight = 26f };

        // ---- THE PLAY-MODE ESCAPE HATCH ----
        //
        // "i can just click play test and see what the best position and
        //  change parametre in inspector and later i can push this parametres
        //  directly to library"
        //
        // Unity throws away every play-mode change on Stop. That is why the
        // numbers on this component drifted so far from anything deliberate:
        // the only way to keep a value found by feel was to write it on paper,
        // stop, and type it back in. Nobody does that more than twice.
        //
        // This writes them onto the PREFAB instead, which is an asset and
        // survives Stop. Same button as the Grip Library's, put here as well
        // because this is where you are already looking when you find the
        // value worth keeping.
        if (Application.isPlaying && PrefabUtility.IsPartOfPrefabInstance(arms.gameObject))
        {
            EditorGUILayout.HelpBox(
                "Playing. Anything you change here is lost on Stop unless you " +
                "push it to the prefab first.",
                MessageType.Info);

            var green = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);

            if (GUILayout.Button("Push these settings to Player.prefab", wide))
                PushToPrefab(arms);

            GUI.backgroundColor = green;

            EditorGUILayout.Space();
        }

        if (GUILayout.Button("Restore recommended settings", wide))
        {
            Undo.RecordObject(arms, "Restore carry grip settings");
            arms.RestoreRecommended();
            EditorUtility.SetDirty(arms);
        }

        EditorGUILayout.Space();

        DrawDefaultInspector();
    }

    /// <summary>
    /// Copy the live values onto the Player prefab.
    ///
    /// Opened through PrefabUtility rather than edited via the instance,
    /// because the instance stops existing on Stop - which is the entire
    /// problem being solved. Field-by-field rather than a whole-component
    /// copy, because this component also holds runtime state (the eased
    /// weight, the cached Animator) that has no business in an asset.
    /// </summary>
    static void PushToPrefab(PlayerCarryArms live)
    {
        const string path = "Assets/_Project/Prefabs/Player.prefab";

        var root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
        {
            Debug.LogError("[Grip] Could not open " + path);
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

            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();

            Debug.Log("[Grip] PlayerCarryArms settings saved to Player.prefab. " +
                      "They survive Stop.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
