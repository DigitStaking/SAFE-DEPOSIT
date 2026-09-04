// PlayerPrefabRepair.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/PlayerPrefabRepair.cs
//
// ========================================================================
// PUTS BACK WHAT THE FBX REIMPORT KNOCKED OFF THE PLAYER PREFAB.
//
// FirstPersonArmsMeshBuilder used to flip Read/Write on Player.fbx and call
// SaveAndReimport() automatically. That reimport regenerated the FBX's
// internal fileIDs - and PlayerModel_FBX_VISUAL is a NESTED PREFAB INSTANCE
// of that FBX, whose extra components are stored as m_AddedComponents keyed
// by targetCorrespondingSourceObject: those exact fileIDs.
//
// When they changed, Unity could no longer resolve what the added components
// were attached TO, and dropped them. Silently. No error, no warning, no
// dialog - the components were simply not in the prefab any more:
//
//   ProceduralLegsIK   gone  ->  the legs stopped doing foot IK and fell back
//                                to playing the raw clip, which is exactly
//                                what "he using animation" looked like in
//                                third person
//   PlayerPushArms     gone  ->  the shove had no arms left to swing
//
// FirstPersonHands survived, which is the only reason this was not even more
// obvious - one of the three happened to keep a resolvable link.
//
// The tool no longer reimports anything (see the guard in
// FirstPersonArmsMeshBuilder). This file exists to undo the damage already
// done, with the tuned values restored rather than defaults, because those
// were dialled in by hand over several sessions and are not recoverable from
// anywhere else.
// ========================================================================

using UnityEditor;
using UnityEngine;

public static class PlayerPrefabRepair
{
    const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
    const string VisualName = "PlayerModel_FBX_VISUAL";

    [MenuItem("SAFE DEPOSIT/Player/Repair Player Prefab Components")]
    public static void Repair()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
        {
            Debug.LogError("[Repair] Could not open " + PlayerPrefabPath);
            return;
        }

        try
        {
            var visual = root.transform.Find(VisualName);
            if (visual == null)
            {
                Debug.LogError("[Repair] No " + VisualName + " under the Player prefab.");
                return;
            }

            var log = new System.Text.StringBuilder("[Repair] ");
            int added = 0;

            // ---- ProceduralLegsIK ----
            var legs = visual.GetComponent<ProceduralLegsIK>();
            if (legs == null)
            {
                legs = visual.gameObject.AddComponent<ProceduralLegsIK>();
                added++;
                log.Append("ProceduralLegsIK ADDED back. ");
            }
            else log.Append("ProceduralLegsIK already present. ");

            // The value that was chosen by eye over several rounds of testing
            // - "i will keep ik weight at 0.55 better" - not the code default.
            legs.weight = 0.552f;

            // ---- PlayerPushArms ----
            var push = visual.GetComponent<PlayerPushArms>();
            if (push == null)
            {
                push = visual.gameObject.AddComponent<PlayerPushArms>();
                added++;
                log.Append("PlayerPushArms ADDED back. ");
            }
            else log.Append("PlayerPushArms already present. ");

            // Restored from the values that were on the prefab before the
            // reimport dropped it, NOT from the code defaults - palmEuler in
            // particular was found by hand and is the one that made the palms
            // sit flat instead of edge-on.
            push.palmEuler = new Vector3(-90f, 0f, 0f);
            push.height = 1.1f;
            push.reach = 0.72f;
            push.windPart = 0.28f;
            push.thrustPart = 0.45f;
            push.weight = 1f;

            // ---- PlayerCarryArms ----
            var carryArms = visual.GetComponent<PlayerCarryArms>();
            if (carryArms == null)
            {
                visual.gameObject.AddComponent<PlayerCarryArms>();
                added++;
                log.Append("PlayerCarryArms ADDED. ");
            }
            else log.Append("PlayerCarryArms already present. ");

            // ---- HandFingerCurl ----
            //
            // Placing a hand somewhere is only half a grip - an open flat hand
            // against a crate reads as pushing it. This closes the fingers,
            // and PlayerCarryArms looks for it on this same object.
            var curl = visual.GetComponent<HandFingerCurl>();
            if (curl == null)
            {
                visual.gameObject.AddComponent<HandFingerCurl>();
                added++;
                log.Append("HandFingerCurl ADDED. ");
            }
            else log.Append("HandFingerCurl already present. ");

            if (added > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
            }

            Debug.Log(log.ToString() + (added > 0
                ? "  Prefab saved."
                : "  Nothing needed changing."));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
