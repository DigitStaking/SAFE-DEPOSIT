// SecondPlayerTool.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/SecondPlayerTool.cs
//
// Menu:  SAFE DEPOSIT -> Test -> Add Second Player
//        SAFE DEPOSIT -> Test -> Remove Second Player
//
// ====================================================================
// PHASE 3 STEP 7 - THE TWO-BODY TEST.
//
// Six steps of "stop assuming there is one of everything" are only claims
// until there are two of something. This puts a second player in the scene so
// all six can be checked at once, by one person, with no netcode involved.
//
// It is a TEST RIG, NOT A MODE. Split-screen is not a feature of this game
// and nothing here is meant to ship - the second body exists to be looked at.
// Remove Second Player takes it all out again.
//
// ====================================================================
// WHY THE SECOND BODY GETS A CAMERA AT ALL
//
// Most of what this test proves needs no camera: whether their head is still
// attached, whether your headlamp is on your own skull, whether the load
// gauge charges 140kg, whether they have their own hit points.
//
// One thing does. "Two cameras that do not fight" cannot be demonstrated with
// one camera, and it was the single most likely thing to break - Camera.main
// returned whichever one Unity felt like, fourteen times over. So the second
// body gets a real camera, rendering into a corner of the screen, and if the
// main view is still yours then the fourteen answers were right.
//
// The corner is also the fastest way to SEE the two failures the spec
// predicted: look at the small view and you are looking at your own body
// through somebody else's eyes.
// ====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SecondPlayerTool
{
    const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
    const string SecondName = "Player_2 (TEST RIG)";
    const string SecondCamName = "Camera_2 (TEST RIG)";

    /// <summary>Beside the first body, not on top of it - two capsules in the
    /// same cubic metre spend the first second shoving each other apart.</summary>
    static readonly Vector3 Offset = new Vector3(1.6f, 0f, 0f);

    [MenuItem("SAFE DEPOSIT/Test/Add Second Player")]
    static void Add()
    {
        if (GameObject.Find(SecondName) != null)
        {
            Debug.LogWarning("[TwoBody] A second player is already in the scene.");
            return;
        }

        var first = Object.FindFirstObjectByType<PlayerMotor>();
        if (first == null)
        {
            Debug.LogError("[TwoBody] No player in the scene to copy from.");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        if (prefab == null)
        {
            Debug.LogError($"[TwoBody] {PlayerPrefab} not found.");
            return;
        }

        // ---- the body ----
        var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        body.name = SecondName;
        body.transform.SetPositionAndRotation(
            first.transform.position + first.transform.rotation * Offset,
            first.transform.rotation);

        // ---- its camera ----
        //
        // Cloned from the first player's rather than built from scratch, so it
        // inherits every tuned value - fov, near clip, eye offset, sensitivity
        // - instead of me guessing at them a second time and producing a view
        // that differs from the real one in ways nobody would notice.
        var firstCam = first.View;
        GameObject camGo;

        if (firstCam != null)
        {
            camGo = Object.Instantiate(firstCam.gameObject);
            camGo.name = SecondCamName;

            var fp = camGo.GetComponent<FirstPersonCamera>();
            fp.target = body.transform;
        }
        else
        {
            camGo = new GameObject(SecondCamName, typeof(Camera), typeof(FirstPersonCamera));
            camGo.GetComponent<FirstPersonCamera>().target = body.transform;
            Debug.LogWarning("[TwoBody] The first player had no camera to clone; " +
                             "the second one is using defaults.");
        }

        var cam = camGo.GetComponent<Camera>();
        if (cam != null)
        {
            // A corner, and ABOVE the main view in depth so it draws over it.
            cam.rect = new Rect(0.72f, 0.72f, 0.27f, 0.27f);
            cam.depth = 10f;

            // Never MainCamera. Two objects tagged MainCamera is the exact
            // ambiguity this whole phase exists to delete, and leaving the tag
            // on a clone would reintroduce it in the one scene built to prove
            // it is gone.
            cam.tag = "Untagged";
        }

        Undo.RegisterCreatedObjectUndo(body, "Add Second Player");
        Undo.RegisterCreatedObjectUndo(camGo, "Add Second Player");
        EditorSceneManager.MarkSceneDirty(body.scene);
        Selection.activeGameObject = body;

        Debug.Log("[TwoBody] Second player added. Press Play, then read the " +
                  "[TwoBody audit] block in the console.\n" +
                  "Look for: your own head still on (small view), their head " +
                  "still on (main view), ONE HUD, and 140kg on the gauge with " +
                  "both of you in the car.");
    }

    [MenuItem("SAFE DEPOSIT/Test/Remove Second Player")]
    static void Remove()
    {
        int n = 0;
        foreach (var name in new[] { SecondName, SecondCamName })
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            Undo.DestroyObjectImmediate(go);
            n++;
        }

        if (n == 0) Debug.Log("[TwoBody] Nothing to remove.");
        else Debug.Log($"[TwoBody] Removed {n} test object(s).");
    }
}
