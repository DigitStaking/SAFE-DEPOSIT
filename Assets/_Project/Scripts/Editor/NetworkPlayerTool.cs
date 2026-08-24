// NetworkPlayerTool.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/NetworkPlayerTool.cs
//
// Menu:  SAFE DEPOSIT -> Network -> Prepare Player Prefab
//
// ====================================================================
// PHASE 4 STEP 2. Puts NetworkObject, NetworkTransform and NetworkPlayer on
// the player prefab and registers it as the thing NGO spawns per client.
//
// Same rule as every other builder here: the known-good setup lives in code.
// Run it again whenever something drifts.
// ====================================================================

using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NetworkPlayerTool
{
    const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";

    [MenuItem("SAFE DEPOSIT/Network/Prepare Player Prefab")]
    static void Prepare()
    {
        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        if (contents == null)
        {
            Debug.LogError($"[Net] {PlayerPrefab} not found.");
            return;
        }

        try
        {
            var netObj = contents.GetComponent<NetworkObject>();
            if (netObj == null) netObj = contents.AddComponent<NetworkObject>();

            var netTf = contents.GetComponent<NetworkTransform>();
            if (netTf == null) netTf = contents.AddComponent<NetworkTransform>();

            // OWNER authority. You move your own body and everyone else
            // receives it; the server does not correct you.
            //
            // Server authority plus prediction exists so a shooter cannot be
            // cheated and so 50ms does not decide who won. This is co-op PvE:
            // nobody is being shot, there is nothing to cheat for, and the
            // worst artefact is a crate settling a few centimetres
            // differently on somebody else's screen.
            netTf.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

            // Rotation yes, scale no. The body turns constantly and nothing
            // in this game ever resizes a player - sending scale would be
            // bandwidth spent on a number that never changes.
            netTf.SyncScaleX = netTf.SyncScaleY = netTf.SyncScaleZ = false;

            netTf.InLocalSpace = false;
            netTf.Interpolate = true;

            if (contents.GetComponent<NetworkPlayer>() == null)
                contents.AddComponent<NetworkPlayer>();

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        // ---- register it as the spawned player ----
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        var net = Object.FindFirstObjectByType<NetworkManager>();

        if (net == null)
        {
            Debug.LogWarning("[Net] Player prefab is ready, but there is no " +
                             "NETWORK object in this scene to register it " +
                             "with. Run Build Network Manager first.");
        }
        else
        {
            net.NetworkConfig.PlayerPrefab = asset;
            EditorUtility.SetDirty(net);
            EditorSceneManager.MarkSceneDirty(net.gameObject.scene);
        }

        Debug.Log("[Net] Player prefab prepared: NetworkObject + " +
                  "NetworkTransform (owner authority) + NetworkPlayer, and " +
                  "registered as NetworkManager.PlayerPrefab.\n" +
                  "Offline play is unchanged - the scene player is still " +
                  "there and none of this runs until you press HOST or JOIN.");
    }
}
