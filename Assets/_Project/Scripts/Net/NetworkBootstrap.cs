// NetworkBootstrap.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/NetworkBootstrap.cs
// Goes on: the NETWORK object, built by SAFE DEPOSIT -> Network -> Build
// Network Manager.
//
// ====================================================================
// PHASE 4 STEPS 1 AND 2 - CONNECT, THEN SPAWN.
//
// Step 1 answered one question on its own - can two copies of this game find
// each other - so that when anything later breaks, the connection is not one
// of the suspects.
//
// Step 2 spawns a body per client. The only thing this file owes it is
// ClearScenePlayer below: the hand-placed body has to step aside before NGO
// starts handing out its own.
//
// ====================================================================
// SINGLE PLAYER MUST KEEP WORKING, AND IT DOES
//
// Press Play and do nothing: the game runs exactly as it did before this file
// existed. Nothing here starts itself.
//
// That is deliberate and it holds for the whole phase. Eleven steps is a long
// time to have a broken game, and every one of them should leave the solo
// build playable - both because it is the only way to keep testing the parts
// that are not networked yet, and because a demo that cannot be launched
// without a second person is a demo nobody can look at.
//
// ====================================================================
// WHY UNITY TRANSPORT AND NOT STEAM, TODAY
//
// One machine runs one Steam account, so two windows on this PC cannot talk to
// each other over Steam's relay. Building Step 1 on Steam would make it
// untestable alone, in the phase that most needs fast iteration.
//
// Unity Transport ships with Netcode for GameObjects and connects two windows
// over 127.0.0.1 with no account, no Steam and no setup. Steam transport is
// still the shipping path - it becomes the default at Step 11 with lobbies and
// invites, and swapping is one field on one component.
//
// That swappability is the reason NGO was chosen over Photon in the first
// place. This is its first dividend.
// ====================================================================

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("Local testing")]
    [Tooltip("Where a client looks for the host. 127.0.0.1 is this machine.")]
    public string address = "127.0.0.1";

    public ushort port = 7777;

    [Tooltip("The old corner HOST/JOIN panel. OFF by default since Step 11 - " +
             "CrewLobby replaced it. Kept because it is still the fastest way " +
             "to start two local windows when Steam is not running.")]
    public bool showPanel = false;

    NetworkManager net;
    string lastEvent = "";
    float lastEventTime = -99f;

    void Awake()
    {
        // ---- PHASE 4 STEP 8: THE COPY THAT ARRIVES WITH THE NEW SCENE ----
        //
        // The round transition is now a NETWORKED scene load, so the running
        // NetworkManager survives it - it has to, or there is nothing left to
        // keep the session alive with. But the scene being loaded contains its
        // own NETWORK object, and that copy arrives into a game that already
        // has one.
        //
        // The new arrival is the one that leaves. It has no session, no
        // connections and no history; the survivor is mid-round with four
        // people attached to it. Deciding by "who got here first" would be
        // exactly backwards.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.gameObject != gameObject)
        {
            Destroy(gameObject);
            return;
        }

        net = GetComponent<NetworkManager>();
        if (net == null)
        {
            Debug.LogError("[Net] NetworkBootstrap needs a NetworkManager on the " +
                           "same object. Run SAFE DEPOSIT > Network > Build " +
                           "Network Manager.");
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (net == null) return;
        net.OnClientConnectedCallback += OnConnected;
        net.OnClientDisconnectCallback += OnDisconnected;

        // ---- EVERY WAY IN, NOT JUST MINE ----
        //
        // The placeholder removal used to live inside this file's own Host and
        // Join methods. Then CrewLobby arrived and called StartHost directly -
        // correctly, it is the menu now - and skipped the cleanup entirely, so
        // the host stood next to a second copy of itself again.
        //
        // A rule that only applies to one entry point is not a rule, it is a
        // habit. NGO fires these whenever a session begins, by any route, so
        // this is the one place that cannot be bypassed by adding another
        // button somewhere.
        net.OnServerStarted += ClearPlaceholders;
        net.OnClientStarted += ClearPlaceholders;

        // SceneManager only exists once a session is running, so the
        // round-change hook cannot go here - Host and Join do it the moment
        // there is one.
    }

    /// <summary>
    /// Remove any body that is not somebody's player object.
    ///
    /// IsPlayerObject, for the fifth time in this phase. The scene holds a
    /// Player so the game runs offline, and a server auto-spawns in-scene
    /// NetworkObjects - so the placeholder is spawned, owned, and identical to
    /// a real player on every field except this one.
    /// </summary>
    void ClearPlaceholders()
    {
        int removed = 0;
        var bodies = new System.Collections.Generic.List<PlayerMotor>(PlayerRegistry.All);

        foreach (var body in bodies)
        {
            if (body == null) continue;

            var netObj = body.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned && netObj.IsPlayerObject) continue;

            if (netObj != null && netObj.IsSpawned && net.IsServer) netObj.Despawn(true);
            else Destroy(body.gameObject);

            removed++;
        }

        if (removed > 0) Say(removed + " placeholder body(s) removed");
    }

    void OnDisable()
    {
        if (net == null) return;
        net.OnClientConnectedCallback -= OnConnected;
        net.OnClientDisconnectCallback -= OnDisconnected;
        net.OnServerStarted -= ClearPlaceholders;
        net.OnClientStarted -= ClearPlaceholders;
    }

    /// <summary>
    /// Listen for round changes. Called from Host and Join because
    /// NetworkManager.SceneManager does not exist until a session starts.
    /// </summary>
    void HookSceneLoads()
    {
        if (net == null || net.SceneManager == null) return;
        net.SceneManager.OnLoadComplete -= ClearScenePlayersAfterLoad;
        net.SceneManager.OnLoadComplete += ClearScenePlayersAfterLoad;
    }

    void OnConnected(ulong id) => Say($"client {id} connected");
    void OnDisconnected(ulong id) => Say($"client {id} left");

    void Say(string what)
    {
        lastEvent = what;
        lastEventTime = Time.time;
        Debug.Log($"[Net] {what}");
    }

    /// <summary>
    /// Point the transport at an address. Done here rather than left on the
    /// component so the Inspector value cannot silently disagree with the one
    /// in code - the same trap ElevatorDeck's serialized capacity turned out
    /// to be in Phase 2.
    /// </summary>
    void ApplyAddress()
    {
        var utp = net.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp != null) utp.SetConnectionData(address, port);
    }

    /// <summary>
    /// Remember every body that exists BEFORE a session starts. All of them
    /// are hand-placed by definition - nothing else can have made one yet.
    ///
    /// This list is the whole fix for the host having two bodies it could
    /// both drive.
    /// </summary>
    readonly System.Collections.Generic.List<GameObject> preSessionBodies =
        new System.Collections.Generic.List<GameObject>();

    /// <summary>
    /// Snapshot the hand-placed bodies. Called immediately before StartHost or
    /// StartClient, while "spawned" is still false for everything.
    /// </summary>
    void RememberScenePlayers()
    {
        preSessionBodies.Clear();
        foreach (var body in PlayerRegistry.All)
            if (body != null) preSessionBodies.Add(body.gameObject);
    }

    /// <summary>
    /// Remove the bodies that were placed in the scene by hand.
    ///
    /// PHASE 4 STEP 2. The scene contains a Player so the game stays playable
    /// offline - a promise made in Step 1 and still kept. But the moment a
    /// session starts, NGO spawns a body PER CLIENT from
    /// NetworkConfig.PlayerPrefab, and the host stands next to a second copy
    /// of itself.
    ///
    /// THIRD ATTEMPT AT THIS TEST. The first asked "does it have a
    /// NetworkObject" and was wrong within the hour: Prepare Player Prefab
    /// adds one to the PREFAB and the scene body is an instance, so it
    /// inherited one.
    ///
    /// The second asked "IsSpawned", and this file confidently called that
    /// "the honest question". It was honest for a CLIENT and a lie for the
    /// HOST, because THE HOST IS THE SERVER AND THE SERVER SPAWNS IN-SCENE
    /// NetworkObjects AUTOMATICALLY. StartHost spawned the scene body before
    /// this method ever ran, so IsSpawned came back true and the guard meant
    /// to protect real players protected the placeholder again. The editor log
    /// said it plainly, twice in four lines:
    ///
    ///     spawned Player 0 (me)  owner=True  local=True   <- Player(Clone)
    ///     spawned Player 0 (me)  owner=True  local=True   <- Player
    ///
    /// Two owned bodies, same slot, same keyboard. Both genuinely the host's,
    /// which is why gating input on ownership could never have fixed it.
    ///
    /// THE THIRD TEST ASKS NOTHING. Identity is captured BEFORE the session
    /// exists, when the answer cannot be in doubt, and acted on after. No
    /// property of a live NetworkObject is consulted, so there is no property
    /// left to be subtly wrong about. Capture early, act late.
    /// </summary>
    void ClearScenePlayer()
    {
        int removed = 0;

        foreach (var body in preSessionBodies)
        {
            if (body == null) continue;

            // A spawned NetworkObject has to be despawned rather than simply
            // destroyed, or the clients keep a body the server has forgotten.
            // Only the server may do it - a client that reaches this line has
            // not synced yet, so its copy is still an ordinary object.
            var netObj = body.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned && net.IsServer)
                netObj.Despawn(true);
            else
                Destroy(body);

            removed++;
        }

        preSessionBodies.Clear();

        if (removed > 0)
            Say($"{removed} scene body(s) removed - the network spawns them now");
    }

    // ==================================================================
    // AND AGAIN AFTER EVERY ROUND, BECAUSE THE SCENE BRINGS A NEW ONE.
    //
    // The two-bodies bug came back in round 2, and it came back for the same
    // reason it existed in the first place: THE SCENE CONTAINS A PLAYER, and
    // a server auto-spawns in-scene NetworkObjects.
    //
    // Capturing the hand-placed bodies before StartHost fixed it for the
    // start of a session, and that was the whole story while a round change
    // ended the session. Step 8 made the round change a scene LOAD instead -
    // so the placeholder comes back with every new building, gets spawned by
    // the host, and the host owns two bodies again.
    //
    // The capture-before-start trick cannot help here: there is no "before"
    // to capture at, the load happens mid-session.
    //
    // IsPlayerObject is the test, and it is the one the editor log taught me
    // the first time round. A real player is somebody's player object. The
    // placeholder is a spawned, owned, perfectly ordinary NetworkObject that
    // is nobody's player - identical on every other field, which is exactly
    // why guessing between them cost three attempts.
    // ==================================================================
    void ClearScenePlayersAfterLoad(ulong clientId, string sceneName,
                                    UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (net == null || clientId != net.LocalClientId) return;

        int removed = 0;
        var bodies = new System.Collections.Generic.List<PlayerMotor>(PlayerRegistry.All);

        foreach (var body in bodies)
        {
            if (body == null) continue;

            var netObj = body.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned && netObj.IsPlayerObject) continue;

            if (netObj != null && netObj.IsSpawned && net.IsServer) netObj.Despawn(true);
            else Destroy(body.gameObject);

            removed++;
        }

        if (removed > 0)
            Say($"{removed} placeholder body(s) removed after the round change");
    }

    // ==================================================================
    // START FIRST, CLEAR SECOND. THE ORDER IS THE WHOLE THING.
    //
    // Both of these used to clear the scene body and then try to connect. If
    // connecting failed - and the likeliest failure by far is pressing HOST
    // in a second window while the first is already hosting - you were left
    // with no scene player, no spawned player, and a camera with nothing to
    // follow. A failed connection took the game down with it.
    //
    // Nothing is destroyed until a session actually exists. Failing now leaves
    // you exactly where you were: offline, single player, still playable.
    // ==================================================================

    public void Host()
    {
        if (net == null || net.IsListening) return;
        ApplyAddress();
        RememberScenePlayers();     // before anything can be spawned

        if (!net.StartHost())
        {
            // The transport reports this as four lines of stack trace about
            // binding a UDP socket. Say the actual problem instead.
            Say($"CANNOT HOST - port {port} is already taken. Something else " +
                "is already hosting. Only ONE window hosts; the other " +
                "presses JOIN.");
            return;
        }

        ClearScenePlayer();
        HookSceneLoads();
        Say("HOSTING - now press JOIN in the other window");
    }

    public void Join()
    {
        if (net == null || net.IsListening) return;
        ApplyAddress();
        RememberScenePlayers();     // before anything can be spawned

        if (!net.StartClient())
        {
            Say("CANNOT JOIN - nothing is hosting at " + address + ".");
            return;
        }

        ClearScenePlayer();
        HookSceneLoads();
        Say("joining...");
    }

    /// <summary>
    /// Hand the socket back when Play stops.
    ///
    /// Without this the EDITOR PROCESS keeps port 7777 bound after you leave
    /// Play mode - the session ended, the process did not - and the next
    /// attempt to host fails with "address already in use" against a game
    /// that is no longer running. Diagnosed by asking Windows who held the
    /// port and getting back "Unity".
    ///
    /// OnApplicationQuit rather than OnDisable: NetworkManager makes itself
    /// DontDestroyOnLoad, so OnDisable would also fire on the scene reload
    /// between rounds, and tearing the session down every time the crew
    /// surfaces is exactly what Step 8 has to avoid.
    /// </summary>
    void OnApplicationQuit()
    {
        if (net != null && net.IsListening) net.Shutdown();
    }

    public void Leave()
    {
        if (net == null || !net.IsListening) return;
        net.Shutdown();
        Say("disconnected");
    }

    // --------------------------------------------------------------------
    // THE PANEL
    //
    // Top-right, because every existing HUD is top-left, centre or bottom -
    // RunManager's quota, CableWear's countdown, PlayerCarry's prompt. A
    // temporary panel should not land on top of a real one.
    //
    // Step 11 replaces this with a lobby and this whole method goes.
    // --------------------------------------------------------------------

    void OnGUI()
    {
        // Never over the lobby. This is the panel the lobby replaced, kept for
        // fast local testing - and two sets of HOST/JOIN buttons on one screen
        // is worse than either alone.
        if (!showPanel || net == null || CrewLobby.PanelUp) return;

        const float w = 190f, h = 26f, pad = 10f;
        float x = Screen.width - w - pad;
        float y = pad;

        var label = new GUIStyle(GUI.skin.label) { fontSize = 12 };

        if (!net.IsListening)
        {
            label.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(x, y, w, 20f), "OFFLINE - single player", label);
            y += 20f;

            var hint = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.4f);
            GUI.Label(new Rect(x, y, w, 26f),
                      "ONE window hosts. The other joins.", hint);
            y += 26f;

            if (GUI.Button(new Rect(x, y, w, h), "HOST")) Host();
            y += h + 4f;

            if (GUI.Button(new Rect(x, y, w, h), $"JOIN  {address}")) Join();
            y += h + 4f;
        }
        else
        {
            // ConnectedClientsIds is SERVER-ONLY in NGO. A client asking for
            // it gets an exception rather than a number, which is a fine
            // first lesson in "who is allowed to know what" - the same
            // host-authority rule Step 3 applies to the money.
            string who = net.IsHost ? "HOST" : "CLIENT";
            string count = net.IsServer
                ? $"{net.ConnectedClientsIds.Count} connected"
                : (net.IsConnectedClient ? "connected" : "connecting...");

            label.normal.textColor = net.IsServer || net.IsConnectedClient
                ? new Color(0.5f, 0.95f, 0.5f)
                : new Color(1f, 0.8f, 0.3f);

            GUI.Label(new Rect(x, y, w, 20f), $"{who}   {count}", label);
            y += 20f;

            GUI.Label(new Rect(x, y, w, 20f), $"my id {net.LocalClientId}", label);
            y += 20f;

            // Step 1 spawns nobody on purpose. Without saying so, a working
            // connection looks identical to a broken one.
            var note = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
            note.normal.textColor = new Color(1f, 1f, 1f, 0.4f);
            GUI.Label(new Rect(x, y, w, 30f),
                      "Step 2: bodies spawn per client, owner-authoritative.", note);
            y += 30f;

            if (GUI.Button(new Rect(x, y, w, h), "LEAVE")) Leave();
            y += h + 4f;
        }

        if (Time.time - lastEventTime < 8f && !string.IsNullOrEmpty(lastEvent))
        {
            var msg = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            msg.normal.textColor = lastEvent.StartsWith("CANNOT")
                ? new Color(1f, 0.4f, 0.35f)
                : new Color(1f, 0.85f, 0.4f);

            GUI.Label(new Rect(x, y, w, 60f), lastEvent, msg);
        }
    }
}
