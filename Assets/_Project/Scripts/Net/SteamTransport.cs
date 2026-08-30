// SteamTransport.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/SteamTransport.cs
// Goes on: the NETWORK object, as an alternative to UnityTransport.
//
// ====================================================================
// PHASE 4 STEP 11 - HOW YOUR FRIENDS ACTUALLY GET IN.
//
// Unity Transport connects to an ADDRESS. That is why everything so far has
// been two windows on one machine: a real friend would need your public IP and
// a forwarded port, which most people cannot do and none should have to.
//
// SteamNetworkingSockets connects to a PERSON. You hand it a SteamID and
// Valve's relay network carries the packets - through NATs, without an IP
// being typed, and without a server to rent. That is the whole reason this
// project chose the free stack in August, and this file is where it pays.
//
// WRITTEN BY HAND, AND THAT IS THE POINT
//
// A ready-made Facepunch transport exists, and is why Facepunch was originally
// recommended - and it did not compile: a stray #endregion three lines from
// the end of a file nobody here can fix upstream. A transport is one class
// with eight methods, so owning it is cheaper than depending on somebody
// else's broken copy.
//
// WHAT NGO ACTUALLY NEEDS
//
// Send, Poll, Start, Stop, Disconnect, and a round-trip time. Everything else
// - the spawning, the variables, the RPCs, all ten steps of this phase - sits
// on top of those and does not care what carries the bytes. Nothing above this
// file changes when you swap transports, which is exactly why the swap is safe
// to make at step eleven rather than step one.
// ====================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class SteamTransport : NetworkTransport
{
    /// <summary>
    /// The virtual port. Not a real one - nothing is opened on your router.
    /// It only separates several kinds of connection between the same two
    /// people, and this game has exactly one kind.
    /// </summary>
    const int VirtualPort = 0;

    const int MaxMessagesPerPoll = 64;

    /// <summary>Who to connect TO, when starting as a client.</summary>
    public ulong HostSteamId;

    public override ulong ServerClientId => 0;

    bool isServer;
    HSteamListenSocket listenSocket;
    HSteamNetConnection hostConnection;      // client side: my link to the host

    // A NGO client id is a number NGO invents; a Steam connection is a handle
    // Steam invents. This dictionary is the only place the two are married,
    // which is what stops the rest of the game ever needing to know a SteamID
    // exists.
    readonly Dictionary<ulong, HSteamNetConnection> connections =
        new Dictionary<ulong, HSteamNetConnection>();

    readonly Queue<Pending> events = new Queue<Pending>();

    struct Pending
    {
        public NetworkEvent evt;
        public ulong client;
        public ArraySegment<byte> data;
    }

    ulong nextClientId = 1;
    Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;

    // ------------------------------------------------------------------
    // LIFECYCLE
    // ------------------------------------------------------------------

    public override void Initialize(NetworkManager networkManager = null)
    {
        if (!SteamBoot.Running)
        {
            Debug.LogError("[SteamTransport] Steam is not running. Start Steam, " +
                           "or put UnityTransport back on the NETWORK object " +
                           "for local testing.");
            return;
        }

        // Relay access has to be asked for and takes a moment to come up.
        // Doing it here rather than on the first connection means the
        // handshake has already happened by the time somebody presses HOST.
        SteamNetworkingUtils.InitRelayNetworkAccess();

        statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>
            .Create(OnConnectionStatusChanged);
    }

    public override bool StartServer()
    {
        if (!SteamBoot.Running) return false;

        isServer = true;
        listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(VirtualPort, 0, null);

        Debug.Log("[SteamTransport] listening as " + SteamBoot.MyName +
                  ". Friends can join from their Steam list.");
        return true;
    }

    public override bool StartClient()
    {
        if (!SteamBoot.Running) return false;

        if (HostSteamId == 0)
        {
            Debug.LogError("[SteamTransport] no host to connect to - join " +
                           "through a friend or an invite, not by pressing JOIN.");
            return false;
        }

        isServer = false;

        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(new CSteamID(HostSteamId));

        hostConnection = SteamNetworkingSockets.ConnectP2P(ref identity, VirtualPort, 0, null);

        Debug.Log("[SteamTransport] connecting to " + HostSteamId +
                  " over Steam's relay.");
        return true;
    }

    public override void Shutdown()
    {
        foreach (var c in connections.Values)
            SteamNetworkingSockets.CloseConnection(c, 0, "shutdown", false);
        connections.Clear();

        if (hostConnection.m_HSteamNetConnection != 0)
        {
            SteamNetworkingSockets.CloseConnection(hostConnection, 0, "shutdown", false);
            hostConnection = default;
        }

        if (listenSocket.m_HSteamListenSocket != 0)
        {
            SteamNetworkingSockets.CloseListenSocket(listenSocket);
            listenSocket = default;
        }

        if (statusChanged != null)
        {
            statusChanged.Dispose();
            statusChanged = null;
        }

        events.Clear();
    }

    // ------------------------------------------------------------------
    // CONNECTIONS
    // ------------------------------------------------------------------

    /// <summary>
    /// Steam telling us a connection changed state.
    ///
    /// The server ACCEPTS here rather than anywhere else, and that matters: an
    /// unaccepted P2P connection is dropped by Steam after a few seconds, so a
    /// host that is slow to answer looks to the joiner exactly like a host
    /// that refused.
    /// </summary>
    void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t info)
    {
        var state = info.m_info.m_eState;
        var conn = info.m_hConn;

        if (isServer)
        {
            if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                SteamNetworkingSockets.AcceptConnection(conn);
                return;
            }

            if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                ulong id = nextClientId++;
                connections[id] = conn;
                Enqueue(NetworkEvent.Connect, id, default);
                return;
            }

            if (IsGone(state))
            {
                ulong id = FindClient(conn);
                if (id != 0)
                {
                    connections.Remove(id);
                    Enqueue(NetworkEvent.Disconnect, id, default);
                }
                SteamNetworkingSockets.CloseConnection(conn, 0, "closed", false);
            }
            return;
        }

        // ---- client side ----
        if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
        {
            Enqueue(NetworkEvent.Connect, ServerClientId, default);
            return;
        }

        if (IsGone(state))
        {
            Enqueue(NetworkEvent.Disconnect, ServerClientId, default);
            SteamNetworkingSockets.CloseConnection(conn, 0, "closed", false);
            hostConnection = default;
        }
    }

    void Enqueue(NetworkEvent evt, ulong client, ArraySegment<byte> data)
    {
        events.Enqueue(new Pending { evt = evt, client = client, data = data });
    }

    static bool IsGone(ESteamNetworkingConnectionState s)
    {
        return s == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer ||
               s == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;
    }

    ulong FindClient(HSteamNetConnection conn)
    {
        foreach (var kv in connections)
            if (kv.Value.m_HSteamNetConnection == conn.m_HSteamNetConnection) return kv.Key;

        return 0;
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        HSteamNetConnection conn;
        if (!connections.TryGetValue(clientId, out conn)) return;

        SteamNetworkingSockets.CloseConnection(conn, 0, "kicked", false);
        connections.Remove(clientId);
    }

    public override void DisconnectLocalClient()
    {
        if (hostConnection.m_HSteamNetConnection == 0) return;

        SteamNetworkingSockets.CloseConnection(hostConnection, 0, "left", false);
        hostConnection = default;
    }

    // ------------------------------------------------------------------
    // TRAFFIC
    // ------------------------------------------------------------------

    HSteamNetConnection ConnectionFor(ulong clientId)
    {
        if (!isServer) return hostConnection;

        HSteamNetConnection conn;
        return connections.TryGetValue(clientId, out conn) ? conn : default;
    }

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)
    {
        var conn = ConnectionFor(clientId);
        if (conn.m_HSteamNetConnection == 0) return;

        // RELIABLE vs UNRELIABLE, honestly mapped. NGO has already decided
        // which of its messages can afford to be lost, and passing that
        // through rather than sending everything reliably is the difference
        // between a lift that stutters under packet loss and one that does not.
        int flags = (delivery == NetworkDelivery.Unreliable ||
                     delivery == NetworkDelivery.UnreliableSequenced)
            ? Constants.k_nSteamNetworkingSend_Unreliable
            : Constants.k_nSteamNetworkingSend_Reliable;

        // Pinned so Steam can read it from native code without the GC moving
        // it underneath.
        GCHandle pin = GCHandle.Alloc(payload.Array, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(payload.Array, payload.Offset);

            long ignored;
            SteamNetworkingSockets.SendMessageToConnection(
                conn, ptr, (uint)payload.Count, flags, out ignored);
        }
        finally
        {
            pin.Free();
        }
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload,
                                           out float receiveTime)
    {
        receiveTime = Time.realtimeSinceStartup;

        Receive();

        if (events.Count > 0)
        {
            var e = events.Dequeue();
            clientId = e.client;
            payload = e.data;
            return e.evt;
        }

        clientId = 0;
        payload = default;
        return NetworkEvent.Nothing;
    }

    void Receive()
    {
        if (isServer)
        {
            // Copied, because a disconnect during the loop mutates the
            // dictionary we would otherwise be walking.
            var snapshot = new List<KeyValuePair<ulong, HSteamNetConnection>>(connections);
            foreach (var kv in snapshot) Drain(kv.Value, kv.Key);
        }
        else if (hostConnection.m_HSteamNetConnection != 0)
        {
            Drain(hostConnection, ServerClientId);
        }
    }

    void Drain(HSteamNetConnection conn, ulong from)
    {
        var ptrs = new IntPtr[MaxMessagesPerPoll];
        int n = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, ptrs, MaxMessagesPerPoll);

        for (int i = 0; i < n; i++)
        {
            var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptrs[i]);

            var bytes = new byte[msg.m_cbSize];
            Marshal.Copy(msg.m_pData, bytes, 0, msg.m_cbSize);

            Enqueue(NetworkEvent.Data, from, new ArraySegment<byte>(bytes));

            // Released every time. A leaked Steam message is native memory
            // that never comes back, and at sixty polls a second that is a
            // leak you meet as a slow death rather than as a crash.
            SteamNetworkingMessage_t.Release(ptrs[i]);
        }
    }

    public override ulong GetCurrentRtt(ulong clientId)
    {
        var conn = ConnectionFor(clientId);
        if (conn.m_HSteamNetConnection == 0) return 0;

        SteamNetConnectionRealTimeStatus_t status = default;
        SteamNetConnectionRealTimeLaneStatus_t lane = default;

        var r = SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, 0, ref lane);

        return r == EResult.k_EResultOK ? (ulong)Mathf.Max(0, status.m_nPing) : 0UL;
    }
}
