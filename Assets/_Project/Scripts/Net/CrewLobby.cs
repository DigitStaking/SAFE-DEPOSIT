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

    /// <summary>
    /// Join a host on this machine, over Unity Transport.
    ///
    /// The local path, and it stays regardless of Steam: it is the only way
    /// one person can test two players, and it has been how every step of this
    /// phase was verified.
    /// </summary>
    public void JoinLocal()
    {
        if (net == null || net.IsListening) return;

        if (net.StartClient()) { Where = Stage.Joining; Say("joining locally..."); }
        else Say("nothing is hosting on this machine");
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
        localStarted = true;
        Where = Stage.InGame;
    }

    /// <summary>
    /// Set by PLAY ALONE, and by START on the host. Separate from the
    /// networked flag because a solo player has no CampaignNet to read - and
    /// without this, "has the run started" answered TRUE offline while the
    /// menu was still on screen, which is why the whole gameplay HUD was
    /// drawing over the top of it.
    /// </summary>
    static bool localStarted;

    public static bool RunHasStarted =>
        localStarted ||
        (CampaignNet.Instance != null && CampaignNet.Instance.Started.Value);

    /// <summary>
    /// The menu is on screen and owns the mouse.
    ///
    /// One property, asked by everything that has to stand aside: the HUD, the
    /// old corner panel, and the camera. Three separate opinions about whether
    /// a menu is up is how a cursor ends up locked behind a button somebody is
    /// trying to press.
    /// </summary>
    public static bool PanelUp => !RunHasStarted;

    /// <summary>Solo. No networking, no waiting, straight in.</summary>
    public void PlayAlone()
    {
        localStarted = true;
        Where = Stage.InGame;
        Say("solo run");
    }

    void Update()
    {
        // A CLIENT LEARNS IT STARTED BY READING, NOT BY BEING TOLD. The host
        // writes Started; everybody else notices. Doing it here rather than in
        // an RPC means a player who joins after START is already past the menu
        // rather than stuck behind it.
        if (!localStarted && CampaignNet.Instance != null &&
            CampaignNet.Instance.Started.Value)
        {
            localStarted = true;
            Where = Stage.InGame;
        }

        // THE MENU OWNS THE MOUSE WHILE IT IS UP.
        //
        // FirstPersonCamera locks the cursor on Start and re-locks it on any
        // left click - which is correct in a first-person game and exactly
        // wrong over a menu, because clicking a button was what took the
        // cursor away.
        if (PanelUp)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

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
        if (net == null || !PanelUp) return;

        // A SOLID BACKDROP OVER THE WHOLE SCREEN.
        //
        // The first version was a translucent panel floating over a lit, moving
        // elevator interior, and it was unreadable - the crew list sat on top
        // of a player model and the buttons sat on top of the keypad.
        //
        // A menu has to own the screen. Blacking it out entirely also does
        // something the panel could not: it stops a player who has not pressed
        // START from wandering off and looking at the building, which is
        // exactly the wrong first impression of a game about being underground.
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        const float w = 460f, h = 360f;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

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

            if (GUI.Button(new Rect(x, y, iw, 38f), "HOST A RUN"))
                Host();
            y += 44f;

            // ---- JOIN, WHICH WAS MISSING ENTIRELY ----
            //
            // The first version had only HOST, on the reasoning that joining
            // happens through Steam's overlay. True, and useless to somebody
            // whose Steam is not running - which is everybody testing locally,
            // including the person who reported this.
            if (GUI.Button(new Rect(x, y, iw, 38f), "JOIN A RUN"))
                JoinLocal();
            y += 44f;

            // PLAY ALONE, because a main menu that cannot be skipped would
            // break the promise every step of this phase has kept: press Play
            // and the solo game works.
            if (GUI.Button(new Rect(x, y, iw, 30f), "play alone"))
                PlayAlone();
            y += 38f;

            GUI.Label(new Rect(x, y, iw, 46f),
                      SteamBoot.Running
                          ? "JOIN connects to another window on this machine. To " +
                            "join a FRIEND, right-click their name in Steam and " +
                            "choose Join Game - or accept their invite."
                          : "Steam is off, so JOIN connects to a second window on " +
                            "this machine. Start Steam to play with friends.", body);
            y += 52f;
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
        {
            if (p == null) continue;

            // ONLY REAL PLAYERS. The scene holds a placeholder body so the
            // game runs offline, and a server auto-spawns in-scene
            // NetworkObjects - so it appears in the registry alongside the
            // host's real body and the roster read "2 of 4 aboard, Player 0,
            // Player 0".
            //
            // IsPlayerObject for the fourth time in this phase. Every other
            // field on those two objects is identical, which is why it keeps
            // being the only test that works.
            var no = p.GetComponent<NetworkObject>();
            if (net != null && net.IsListening &&
                (no == null || !no.IsSpawned || !no.IsPlayerObject)) continue;

            names.Add(p.gameObject.name);
        }

        return names;
    }
}
