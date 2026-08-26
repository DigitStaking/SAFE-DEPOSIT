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

            // ---- LOCAL SPACE, AND THIS IS THE WHOLE ELEVATOR FIX ----
            //
            // This was false, and it is why a teammate lagged behind the lift.
            //
            // In WORLD space a rider sends its absolute position. On a moving
            // car that position already contains the car's movement, and it
            // arrives one interpolation buffer late - so on your screen their
            // body is where the floor USED to be. At the fast speed of 8m/s a
            // 100ms buffer is 80cm. They appear to sink through the floor of a
            // rising lift, which is exactly the report.
            //
            // No amount of tuning fixes it, because nothing is wrong: the
            // number is correct and simply old. The question was wrong. "Where
            // are you in the world" changes 8 metres a second on a moving
            // lift. "Where are you in the car" does not change at all while
            // somebody stands still.
            //
            // So riders get PARENTED to the car - ElevatorNet does it on the
            // host and NGO replicates the parent change - and this sends the
            // offset from the car instead. A stationary rider now sends a
            // constant, and a constant cannot arrive late.
            //
            // Unparented, local space and world space are the same thing, so
            // this costs nothing anywhere else in the game.
            netTf.InLocalSpace = true;
            netTf.Interpolate = true;

            // ---- ANIMATION ON THE WIRE ----
            //
            // NetworkTransform sends where you are. It does not send what you
            // are DOING - walking, dancing, kneeling, reaching for a crate are
            // all animator parameters, and none of them ever left the machine
            // that produced them. So you danced and nobody saw it.
            //
            // The Animator lives on the MODEL, a child of the root, so the
            // reference has to be set explicitly. Left unset, NetworkAnimator
            // finds nothing and silently replicates nothing - which looks
            // exactly like not having added it at all.
            var netAnim = contents.GetComponent<OwnerNetworkAnimator>();
            if (netAnim == null) netAnim = contents.AddComponent<OwnerNetworkAnimator>();
            netAnim.Animator = contents.GetComponentInChildren<Animator>(true);

            if (netAnim.Animator == null)
                Debug.LogWarning("[Net] no Animator found under the player prefab - " +
                                 "emotes and the walk cycle will not replicate.");

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
                  "NetworkTransform + OwnerNetworkAnimator (both owner " +
                  "authority) + NetworkPlayer, and " +
                  "registered as NetworkManager.PlayerPrefab.\n" +
                  "Offline play is unchanged - the scene player is still " +
                  "there and none of this runs until you press HOST or JOIN.");
    }
}
