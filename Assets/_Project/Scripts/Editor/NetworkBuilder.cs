// NetworkBuilder.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/NetworkBuilder.cs
//
// Menu:  SAFE DEPOSIT -> Network -> Build Network Manager
//
// ====================================================================
// PHASE 4 STEP 1. Same pattern as every other builder in this project:
// the known-good setup lives in code, not in somebody's memory of which
// checkbox they ticked. Run it again whenever something drifts.
// ====================================================================

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NetworkBuilder
{
    const string ObjectName = "NETWORK";

    [MenuItem("SAFE DEPOSIT/Network/Build Network Manager")]
    static void Build()
    {
        var existing = GameObject.Find(ObjectName);
        if (existing != null)
        {
            Debug.Log($"[Net] '{ObjectName}' already exists - reconfiguring it.");
        }

        var go = existing != null ? existing : new GameObject(ObjectName);

        var net = go.GetComponent<NetworkManager>();
        if (net == null) net = go.AddComponent<NetworkManager>();

        var utp = go.GetComponent<UnityTransport>();
        if (utp == null) utp = go.AddComponent<UnityTransport>();

        utp.SetConnectionData("127.0.0.1", 7777);
        net.NetworkConfig.NetworkTransport = utp;

        // NOT "don't destroy on load".
        //
        // RunManager.ReloadScene rebuilds the scene between rounds, and a
        // surviving NetworkManager would come back into a scene that also
        // contains a fresh one - two singletons, and NGO picks a fight about
        // it. Step 8 owns the networked scene transition and is where this
        // gets a real answer; until then, one per scene is correct and simple.
        net.NetworkConfig.EnableSceneManagement = true;

        var boot = go.GetComponent<NetworkBootstrap>();
        if (boot == null) boot = go.AddComponent<NetworkBootstrap>();
        boot.address = "127.0.0.1";
        boot.port = 7777;
        boot.showPanel = true;

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;

        Debug.Log("[Net] NETWORK built: NetworkManager + UnityTransport + " +
                  "NetworkBootstrap, pointed at 127.0.0.1:7777.\n" +
                  "Press Play and do nothing - the game still runs solo. " +
                  "HOST / JOIN is top-right.");
    }
}
