// CrewLobby.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/CrewLobby.cs
// Goes on: the NETWORK object.
//
// ====================================================================
// PHASE 4 STEP 11 - THE SCREEN A PLAYER ACTUALLY MEETS FIRST.
//
// Asked for back at Step 2 and deferred with a reason: a lobby built on
// Unity Transport would have been an IP box, and the moment Steam arrived it
// would have been thrown away and built again. Steam is here, so it is due.
//
// WHAT IT REPLACES
//
// A HOST button and a JOIN button in the top-right corner, which worked
// because there were exactly two windows on one machine and I knew which was
// which. Neither of those things is true for a friend who has just been sent
// a zip file.
//
// WHAT IT HAS TO DO, IN ORDER OF HOW MUCH IT MATTERS
//
//   1  say what the crew is called and who is in it
//   2  let the host invite people BY NAME, not by address
//   3  hold everyone at the surface until the host says go
//   4  keep working with no Steam at all, on 127.0.0.1, as it has all phase
//
// Point 4 is not politeness. Steam is a dependency outside this project, and
// the day it is down or somebody is testing without it, the game has to still
// be playable - which has been true at every step of this phase and does not
// stop being true because there is now a nicer screen in front of it.
//
// WHY THERE IS NO SERVER BROWSER
//
// App 480 is Valve's public test id, shared by every developer doing exactly
// this - so its lobby list is full of strangers running unrelated games.
// Browsing it would be worse than useless. You invite the people you know,
// which is what a four-player co-op wants anyway.
// ====================================================================

using System.Collections.Generic;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class CrewLobby : MonoBehaviour
{
    public enum Stage { Menu, Hosting, Joining, InGame }

    public static CrewLobby Instance { get; private set; }
    public static Stage Where { get; private set; } = Stage.Menu;

    /// <summary>Lobby key holding the host's SteamID, so a joiner knows who to
    /// connect to once they are inside.</summary>
    const string HostKey = "host_steam_id";
    const string NameKey = "crew_name";

    [Tooltip("Four, because Crew.MaxMembers is four and a lobby that admits a " +
             "fifth person admits somebody who can never have a body.")]
    public int maxCrew = Crew.MaxMembers;

    NetworkManager net;
    SteamTransport steam;
    string crewName = "";
    string status = "";

    CSteamID lobby;
    Callback<LobbyCreated_t> onCreated;
    Callback<GameLobbyJoinRequested_t> onJoinRequested;
    Callback<LobbyEnter_t> onEntered;

    void Awake()
    {
        Instance = this;
        net = GetComponent<NetworkManager>();
        steam = GetComponent<SteamTransport>();

        crewName = SteamBoot.Running
            ? SteamBoot.MyName + "'s crew"
            : "the crew";
    }

    void OnEnable()
    {
        if (!SteamBoot.Running) return;

        onCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        onEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);

        // THE ONE THAT MAKES IT FEEL LIKE STEAM. Fired when somebody clicks
        // "Join Game" on your name, or accepts an invite - the game is simply
        // told which lobby to enter, and no address is ever seen by anybody.
        onJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
    }

    void OnDisable()
    {
        if (onCreated != null) { onCreated.Dispose(); onCreated = null; }
        if (onEntered != null) { onEntered.Dispose(); onEntered = null; }
        if (onJoinRequested != null) { onJoinRequested.Dispose(); onJoinRequested = null; }
    }

    // ------------------------------------------------------------------
    // HOSTING
    // ------------------------------------------------------------------

    public void Host()
    {
        if (net == null || net.IsListening) return;

        if (!SteamBoot.Running)
        {
            // No Steam: the old local path, unchanged. Still two windows on one
            // machine, still useful, and still the only way to test alone.
            if (net.StartHost()) { Where = Stage.Hosting; Say("hosting locally on 127.0.0.1"); }
            else Say("CANNOT HOST - port already taken");
            return;
        }

        Say("creating lobby...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxCrew);
    }

    void OnLobbyCreated(LobbyCreated_t e)
    {
        if (e.m_eResult != EResult.k_EResultOK)
        {
            Say("Steam refused to make a lobby: " + e.m_eResult);
            return;
        }

        lobby = new CSteamID(e.m_ulSteamIDLobby);

        // The joiner reads this to know who to open a connection to. Without
        // it they would be in the lobby and connected to nothing - which looks
        // exactly like a game that hung.
        SteamMatchmaking.SetLobbyData(lobby, HostKey, SteamBoot.MySteamId.ToString());
        SteamMatchmaking.SetLobbyData(lobby, NameKey, crewName);

        if (!net.StartHost())
        {
            Say("lobby made but the host would not start");
            return;
        }

        Where = Stage.Hosting;
        Say("waiting for the crew - invite them from Steam");
    }

    // ------------------------------------------------------------------
    // JOINING
    // ------------------------------------------------------------------

    void OnJoinRequested(GameLobbyJoinRequested_t e)
    {
        Say("joining...");
        SteamMatchmaking.JoinLobby(e.m_steamIDLobby);
    }

    void OnLobbyEntered(LobbyEnter_t e)
    {
        lobby = new CSteamID(e.m_ulSteamIDLobby);

        // The host enters its own lobby too, and must not connect to itself.
        if (net.IsListening) return;

        string raw = SteamMatchmaking.GetLobbyData(lobby, HostKey);
        crewName = SteamMatchmaking.GetLobbyData(lobby, NameKey);

        ulong hostId;
        if (!ulong.TryParse(raw, out hostId) || hostId == 0)
        {
            Say("that lobby has no host in it");
            return;
        }

        if (steam == null)
        {
            Say("no SteamTransport on the NETWORK object - run Build Network Manager");
            return;
        }

        steam.HostSteamId = hostId;

        if (net.StartClient()) { Where = Stage.Joining; Say("connecting over Steam..."); }
        else Say("could not start the client");
    }

    public void Invite()
    {
        if (!SteamBoot.Running || lobby.m_SteamID == 0) return;

        // Steam's own overlay, because it already knows your friends list and
        // has solved the problem of picking somebody out of it.
        SteamFriends.ActivateGameOverlayInviteDialog(lobby);
    }

    public void Leave()
    {
        if (SteamBoot.Running && lobby.m_SteamID != 0)
        {
            SteamMatchmaking.LeaveLobby(lobby);
            lobby = default;
        }

        if (net != null && net.IsListening) net.Shutdown();

        Where = Stage.Menu;
        Say("left");
    }

    // ------------------------------------------------------------------
    // STARTING
    // ------------------------------------------------------------------

    /// <summary>
    /// The host says go.
    ///
    /// Everybody is already spawned and standing in the lift by this point -
    /// what changes is that the building starts counting. Holding the run
    /// rather than holding the SPAWN is deliberate: a crew that can walk
    /// around and see each other before the timer starts is a crew that
    /// arrives having already decided who is carrying the radios.
    /// </summary>
    public void Start_()
    {
        if (CampaignNet.Instance == null || !CampaignNet.Instance.IsServer) return;

        CampaignNet.Instance.Started.Value = true;
        Where = Stage.InGame;
    }

    public static bool RunHasStarted =>
        CampaignNet.Instance == null || CampaignNet.Instance.Started.Value;

    void Say(string s)
    {
        status = s;
        Debug.Log("[Lobby] " + s);
    }

    // ------------------------------------------------------------------
    // THE SCREEN
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (net == null) return;
        if (net.IsListening && RunHasStarted) return;   // in a run; get out of the way

        const float w = 460f, h = 320f;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;

        var title = new GUIStyle(GUI.skin.label)
        { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = new Color(1f, 0.85f, 0.4f);

        var body = new GUIStyle(GUI.skin.label)
        { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        body.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

        float x = panel.x + 30f, y = panel.y + 20f, iw = w - 60f;

        GUI.Label(new Rect(x, y, iw, 30f), "SAFE DEPOSIT", title);
        y += 34f;

        GUI.Label(new Rect(x, y, iw, 20f),
                  SteamBoot.Running
                      ? "Steam: " + SteamBoot.MyName
                      : "Steam is not running - local play only (127.0.0.1)", body);
        y += 28f;

        if (!net.IsListening)
        {
            GUI.Label(new Rect(x, y, 90f, 24f), "crew name", body);
            crewName = GUI.TextField(new Rect(x + 95f, y, iw - 95f, 24f), crewName, 28);
            y += 34f;

            if (GUI.Button(new Rect(x, y, iw, 40f), "HOST A RUN"))
                Host();
            y += 46f;

            GUI.Label(new Rect(x, y, iw, 40f),
                      SteamBoot.Running
                          ? "To join a friend: open Steam, right-click their name, " +
                            "Join Game. Or accept their invite."
                          : "Start Steam to play with friends. Without it, a second " +
                            "window on this machine can still join.", body);
            y += 46f;
        }
        else
        {
            GUI.Label(new Rect(x, y, iw, 22f), crewName, title);
            y += 30f;

            // WHO IS ACTUALLY HERE. The single most useful thing on this
            // screen: a host who cannot see their friend arrive has no idea
            // whether to keep waiting.
            var names = CrewNames();
            GUI.Label(new Rect(x, y, iw, 20f),
                      names.Count + " of " + maxCrew + " aboard", body);
            y += 24f;

            foreach (var n in names)
            {
                GUI.Label(new Rect(x, y, iw, 20f), n, body);
                y += 20f;
            }

            y = panel.y + h - 110f;

            if (net.IsServer)
            {
                if (SteamBoot.Running && GUI.Button(new Rect(x, y, iw, 32f), "INVITE FRIENDS"))
                    Invite();
                y += 38f;

                if (GUI.Button(new Rect(x, y, iw, 36f), "START THE RUN"))
                    Start_();
                y += 42f;
            }
            else
            {
                GUI.Label(new Rect(x, y, iw, 32f), "waiting for the host to start...", body);
                y += 38f;
            }
        }

        if (GUI.Button(new Rect(x, panel.y + h - 34f, iw, 26f),
                       net.IsListening ? "LEAVE" : "QUIT"))
        {
            if (net.IsListening) Leave();
            else Application.Quit();
        }

        if (!string.IsNullOrEmpty(status))
        {
            var s = new GUIStyle(GUI.skin.label)
            { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = new Color(1f, 0.8f, 0.4f);
            GUI.Label(new Rect(panel.x, panel.y + h + 4f, w, 20f), status, s);
        }
    }

    /// <summary>
    /// Everyone with a body, by name. Asked live, because the whole purpose of
    /// this list is to change while somebody is looking at it.
    /// </summary>
    List<string> CrewNames()
    {
        var names = new List<string>();

        foreach (var p in PlayerRegistry.All)
            if (p != null) names.Add(p.gameObject.name);

        return names;
    }
}
