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

    [Tooltip("Draw the HOST / JOIN panel. Turn off once lobbies exist (Step 11).")]
    public bool showPanel = true;

    NetworkManager net;
    string lastEvent = "";
    float lastEventTime = -99f;

    void Awake()
    {
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
    }

    void OnDisable()
    {
        if (net == null) return;
        net.OnClientConnectedCallback -= OnConnected;
        net.OnClientDisconnectCallback -= OnDisconnected;
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
    /// Remove every body that was placed in the scene by hand.
    ///
    /// PHASE 4 STEP 2. The scene contains a Player so the game stays playable
    /// offline - a promise made in Step 1 and still kept. But the moment a
    /// session starts, NGO spawns a body PER CLIENT from
    /// NetworkConfig.PlayerPrefab, and the host would stand next to a second
    /// copy of itself that nobody owns and nothing controls.
    ///
    /// SPAWNED, NOT "HAS A NetworkObject". The first version of this tested
    /// for the component and was wrong within an hour of being written:
    /// Prepare Player Prefab adds NetworkObject to the PREFAB, and the scene
    /// body is an INSTANCE of that prefab, so it inherited one. The guard
    /// meant to protect real players started protecting the placeholder
    /// instead, and the host got two bodies again.
    ///
    /// IsSpawned is the honest question. A hand-placed instance has the
    /// component and has never been spawned; a real player has both. The
    /// difference is what NGO did, not what the prefab carries.
    ///
    /// ALL of them, not just PlayerRegistry.Local - the two-body test rig
    /// leaves a second hand-placed body behind, and "the local one" would
    /// have left it standing there.
    /// </summary>
    void ClearScenePlayer()
    {
        int removed = 0;

        // Copied first: destroying a body unregisters it in OnDisable, and
        // mutating the registry while walking it is its own bug.
        var bodies = new System.Collections.Generic.List<PlayerMotor>(PlayerRegistry.All);

        foreach (var body in bodies)
        {
            if (body == null) continue;

            var netObj = body.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) continue;   // a real player

            Destroy(body.gameObject);
            removed++;
        }

        if (removed > 0)
            Say($"{removed} scene body(s) removed - the network spawns them now");
    }

    public void Host()
    {
        if (net == null || net.IsListening) return;
        ApplyAddress();
        ClearScenePlayer();

        if (net.StartHost())
        {
            Say("HOSTING - now press JOIN in the other window");
            return;
        }

        // The only way this realistically fails is the port already being
        // held, and the transport's own message for that is four lines of
        // stack trace about binding a UDP socket. Say the actual problem.
        Say($"CANNOT HOST - port {port} is already taken. Something else is " +
            "already hosting. Only ONE window hosts; the other presses JOIN.");
    }

    public void Join()
    {
        if (net == null || net.IsListening) return;
        ApplyAddress();
        ClearScenePlayer();
        Say(net.StartClient() ? "joining..." : "failed to start client");
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
        if (!showPanel || net == null) return;

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
