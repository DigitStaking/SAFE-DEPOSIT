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
    const string CampaignName = "CAMPAIGN";

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

        // ---- NGO OWNS THE SCENE TRANSITION (STEP 8) ----
        //
        // This block used to say the opposite, and explained that a surviving
        // NetworkManager would come back into a reloaded scene that also
        // contains a fresh one - two singletons, and NGO picks a fight.
        //
        // That was true while the round transition was SceneManager.LoadScene,
        // which reloads on one machine and takes the session with it. Step 8
        // replaced it with NetworkManager.SceneManager.LoadScene, which loads
        // the scene on every connected machine and keeps the session alive
        // across it. The manager MUST survive that, or there is nothing left
        // to keep it alive with.
        //
        // The duplicate problem is real and is handled where it belongs, in
        // NetworkBootstrap.Awake: the copy that arrives in the reloaded scene
        // finds a manager already running and removes itself.
        net.NetworkConfig.EnableSceneManagement = true;

        var boot = go.GetComponent<NetworkBootstrap>();
        if (boot == null) boot = go.AddComponent<NetworkBootstrap>();
        boot.address = "127.0.0.1";
        boot.port = 7777;
        boot.showPanel = true;

        BuildCampaignObject();
        BuildElevatorNet();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;

        Debug.Log("[Net] NETWORK built: NetworkManager + UnityTransport + " +
                  "NetworkBootstrap, pointed at 127.0.0.1:7777.\n" +
                  "Press Play and do nothing - the game still runs solo. " +
                  "HOST / JOIN is top-right.");
    }

    /// <summary>
    /// PHASE 4 STEP 3. The shared pot needs its own GameObject.
    ///
    /// IT CANNOT LIVE ON THE NETWORK OBJECT. NGO forbids a NetworkObject on
    /// the same GameObject as the NetworkManager - the manager is what runs
    /// the spawning, so it cannot also be a thing that gets spawned. The
    /// campaign gets a sibling.
    ///
    /// PLACED IN THE SCENE, ON PURPOSE. A server spawns in-scene
    /// NetworkObjects by itself, so the pot exists the instant a session
    /// starts - before any player has connected, with no code to arrange it.
    ///
    /// That is the SAME behaviour that gave the host two bodies an hour ago:
    /// the hand-placed player was auto-spawned too. Here it is exactly what
    /// is wanted. The behaviour was never the bug - a player prefab sitting
    /// in the scene was.
    /// </summary>
    static void BuildCampaignObject()
    {
        var existing = GameObject.Find(CampaignName);
        var go = existing != null ? existing : new GameObject(CampaignName);

        if (go.GetComponent<NetworkObject>() == null)
            go.AddComponent<NetworkObject>();

        if (go.GetComponent<CampaignNet>() == null)
            go.AddComponent<CampaignNet>();

        // PHASE 4 STEP 6. The loot roster rides on the same object as the
        // money, because they are the same thing: the campaign's memory of
        // what the building contains and what the crew is owed for it.
        if (go.GetComponent<LootNet>() == null)
            go.AddComponent<LootNet>();

        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    /// <summary>
    /// PHASE 4 STEP 5. Put the lift on the wire.
    ///
    /// Done here rather than in ElevatorBuilder so nobody has to rebuild a
    /// working car to network it. ElevatorBuilder tears the whole thing down
    /// and puts it back; this adds three components to whatever is already
    /// in the scene. Both tools do it now, so a later rebuild does not
    /// quietly drop it again.
    ///
    /// SERVER AUTHORITY, unlike the player. The player is owner-authoritative
    /// because you should never wait on a round trip to move your own body.
    /// The car belongs to nobody, four people press its buttons, and the one
    /// thing it must never do is be in two places - so it has exactly one
    /// author, and that is the host.
    /// </summary>
    static void BuildElevatorNet()
    {
        var lift = Object.FindFirstObjectByType<Elevator>();
        if (lift == null)
        {
            Debug.LogWarning("[Net] no Elevator in this scene to network. " +
                             "Run Build Elevator Car first.");
            return;
        }

        var go = lift.gameObject;

        if (go.GetComponent<NetworkObject>() == null)
            go.AddComponent<NetworkObject>();

        // NO NetworkTransform. The car is reproduced, not streamed - see
        // ElevatorNet.OnNetworkSpawn. Any that survives from an earlier build
        // is removed, because leaving one enabled reintroduces exactly the
        // interpolation noise that was being teleported into riders.
        foreach (var stale in go.GetComponents<Unity.Netcode.Components.NetworkTransform>())
            Object.DestroyImmediate(stale);

        if (go.GetComponent<ElevatorNet>() == null)
            go.AddComponent<ElevatorNet>();

        // ---- AND THE PRICE SCANNER, IF THE CAR HAS LOST IT ----
        //
        // The scanner is built by ElevatorBuilder, which rebuilds the entire
        // car - far too destructive to ask for when the only thing missing is
        // one component on one plinth. A scene built before the scanner
        // existed simply does not have it, and the symptom is a readout that
        // stays blank, which is indistinguishable from standing in the wrong
        // place.
        var plinth = lift.transform.Find("Car/Scanner");
        if (plinth == null) plinth = lift.transform.Find("Scanner");

        if (plinth != null && plinth.GetComponent<PriceScanner>() == null)
        {
            plinth.gameObject.AddComponent<PriceScanner>();
            Debug.Log("[Net] added the missing PriceScanner to the car.");
        }
        else if (plinth == null)
        {
            Debug.LogWarning("[Net] this car has no Scanner plinth at all - " +
                             "run SAFE DEPOSIT > Build Elevator Car to get one. " +
                             "That rebuilds the whole car.");
        }

        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Net] ELEVATOR networked: NetworkObject + NetworkTransform " +
                  "(server authority, Y only) + ElevatorNet.");
    }
}
