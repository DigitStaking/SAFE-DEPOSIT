// NetworkBootstrap.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/NetworkBootstrap.cs
// Goes on: the NETWORK object, built by SAFE DEPOSIT -> Network -> Build
// Network Manager.
//
// ====================================================================
// PHASE 4 STEP 1 - TWO WINDOWS, CONNECTED.
//
// "Done when: a host window and a client window agree they are connected."
//
// That is the entire scope. No player is spawned, nothing replicates, nobody
// can see anybody. This step exists to answer one question - can two copies of
// this game find each other - and answering it on its own means that when
// Step 2 fails, the connection is not one of the suspects.
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

    public void Host()
    {
        if (net == null || net.IsListening) return;
        ApplyAddress();
        Say(net.StartHost() ? "HOSTING" : "failed to host");
    }

    public void Join()
    {
        if (net == null || net.IsListening) return;
        ApplyAddress();
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

            if (GUI.Button(new Rect(x, y, w, h), "LEAVE")) Leave();
            y += h + 4f;
        }

        if (Time.time - lastEventTime < 4f && !string.IsNullOrEmpty(lastEvent))
        {
            label.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(x, y, w, 20f), lastEvent, label);
        }
    }
}
