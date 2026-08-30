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
    string joinCode = "";

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
    // WHICH TRANSPORT
    // ------------------------------------------------------------------

    /// <summary>
    /// Point NetworkManager at the transport that can actually reach the other
    /// person, and do it before every start.
    /// </summary>
    /// <remarks>
    /// THE MISSING STEP. The Steam lobby worked, the invite arrived, the
    /// friend accepted - and the host still read "1 connected", because
    /// NetworkConfig was still pointing at UnityTransport and the join went to
    /// 127.0.0.1. Everything Steam-shaped was correct and the packets were
    /// being posted to the wrong address.
    ///
    /// Chosen at runtime rather than set in the editor, because the right
    /// answer changes with circumstance and neither one is "the" setting:
    /// with Steam running you want the relay, and without it you want two
    /// windows on one machine, which is still how this gets tested alone.
    /// </remarks>
    void UseRightTransport()
    {
        if (net == null) return;

        if (SteamBoot.Running && steam != null)
        {
            net.NetworkConfig.NetworkTransport = steam;
            return;
        }

        var utp = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (utp != null) net.NetworkConfig.NetworkTransport = utp;
    }

    // ------------------------------------------------------------------
    // HOSTING
    // ------------------------------------------------------------------

    public void Host()
    {
        if (net == null || net.IsListening) return;

        UseRightTransport();

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

        // ---- WHAT MAKES "JOIN GAME" APPEAR ON YOUR NAME ----
        //
        // Steam shows a friend as joinable only when their game has published
        // a "connect" string. Without it there is nothing for right-click to
        // do, which is why a friend could see me In Game and had no way in.
        //
        // +connect_lobby is Valve's own convention and the client understands
        // it: clicking Join launches or signals the game with that lobby, and
        // GameLobbyJoinRequested_t arrives on their side, which is already
        // handled below.
        SteamFriends.SetRichPresence("connect", "+connect_lobby " + lobby.m_SteamID);

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
        UseRightTransport();

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

        UseRightTransport();

        if (net.StartClient()) { Where = Stage.Joining; Say("joining locally..."); }
        else Say("nothing is hosting on this machine");
    }

    public void Invite()
    {
        if (!SteamBoot.Running || lobby.m_SteamID == 0) return;

        // Steam's own overlay. Only works when the game was LAUNCHED FROM
        // STEAM, because that is when the overlay is injected - so it does
        // nothing at all for a build somebody double-clicked, which is what
        // everybody does with a build a friend sent them. Kept because it is
        // the nicest route when it is available; not relied on.
        SteamFriends.ActivateGameOverlayInviteDialog(lobby);
    }

    /// <summary>
    /// Invite one friend directly, with no overlay involved.
    ///
    /// THE ROUTE THAT ACTUALLY WORKS TODAY. Sending needs no overlay at all,
    /// and the invite arrives in their Steam client as a notification with a
    /// Join button - which fires GameLobbyJoinRequested_t in their game, the
    /// callback already wired up above.
    /// </summary>
    public void InviteFriend(CSteamID friend)
    {
        if (!SteamBoot.Running || lobby.m_SteamID == 0) return;

        bool sent = SteamMatchmaking.InviteUserToLobby(lobby, friend);
        Say(sent
            ? "invited " + SteamFriends.GetFriendPersonaName(friend)
            : "Steam refused to send that invite");
    }

    /// <summary>
    /// Friends who are running this same game right now.
    ///
    /// The list is short on purpose: somebody playing something else cannot
    /// join, and offering to invite them is offering a button that fails.
    /// </summary>
    List<CSteamID> FriendsInGame()
    {
        var found = new List<CSteamID>();
        if (!SteamBoot.Running) return found;

        var me = SteamUtils.GetAppID();
        int n = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

        for (int i = 0; i < n; i++)
        {
            var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);

            FriendGameInfo_t game;
            if (!SteamFriends.GetFriendGamePlayed(id, out game)) continue;
            if (game.m_gameID.AppID() != me) continue;

            found.Add(id);
        }

        return found;
    }

    /// <summary>
    /// Join a lobby by its id, pasted in.
    ///
    /// The route with no moving parts: no overlay, no rich presence, no Steam
    /// UI at all. If everything else fails, somebody reads a number out loud
    /// and it works.
    /// </summary>
    public void JoinByCode(string code)
    {
        if (!SteamBoot.Running) { Say("Steam is not running"); return; }

        ulong id;
        if (!ulong.TryParse((code ?? "").Trim(), out id) || id == 0)
        {
            Say("that does not look like a lobby code");
            return;
        }

        Say("joining lobby " + id + "...");
        SteamMatchmaking.JoinLobby(new CSteamID(id));
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

        const float w = 470f, h = 560f;
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

            // ---- JOIN BY CODE ----
            //
            // The route with no moving parts: no overlay, no rich presence, no
            // Steam UI at all. Somebody reads a number out loud and it works,
            // which is the only kind of instruction that survives being given
            // over voice chat to a friend who has never run this before.
            if (SteamBoot.Running)
            {
                GUI.Label(new Rect(x, y, 78f, 24f), "lobby code", body);
                joinCode = GUI.TextField(new Rect(x + 82f, y, iw - 160f, 24f), joinCode, 24);

                if (GUI.Button(new Rect(x + iw - 74f, y, 74f, 24f), "JOIN"))
                    JoinByCode(joinCode);
                y += 32f;
            }

            // PLAY ALONE, because a main menu that cannot be skipped would
            // break the promise every step of this phase has kept: press Play
            // and the solo game works.
            if (GUI.Button(new Rect(x, y, iw, 30f), "play alone"))
                PlayAlone();
            y += 36f;

            y = DrawMicSettings(x, y, iw, body);

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
                // ---- THE CODE, WHICH NEEDS NO OVERLAY ----
                //
                // ActivateGameOverlayInviteDialog only works when the Steam
                // OVERLAY is injected, and the overlay is only injected into a
                // game LAUNCHED FROM STEAM. Running the exe directly - which is
                // what anybody does with a build a friend sent them - means the
                // invite button does nothing at all, silently.
                //
                // So the code is on screen, and it is the route that always
                // works: read it out, paste it, done. Steam still carries the
                // connection; it just is not asked to draw anything.
                if (SteamBoot.Running && lobby.m_SteamID != 0)
                {
                    GUI.Label(new Rect(x, y, iw, 18f), "LOBBY CODE", body);
                    y += 18f;

                    var code = new GUIStyle(GUI.skin.textField)
                    { fontSize = 15, alignment = TextAnchor.MiddleCenter };

                    GUI.TextField(new Rect(x, y, iw - 70f, 26f),
                                  lobby.m_SteamID.ToString(), code);

                    if (GUI.Button(new Rect(x + iw - 65f, y, 65f, 26f), "copy"))
                        GUIUtility.systemCopyBuffer = lobby.m_SteamID.ToString();
                    y += 32f;

                    GUI.Label(new Rect(x, y, iw, 18f),
                              "send that to your friend - they paste it into JOIN", body);
                    y += 24f;
                }

                // ---- FRIENDS ALREADY IN THE GAME ----
                //
                // One button each, by name, sending a real Steam invite with
                // no overlay involved. This is the route that works for a
                // build somebody double-clicked.
                var playing = FriendsInGame();

                if (playing.Count > 0)
                {
                    GUI.Label(new Rect(x, y, iw, 18f), "friends in game", body);
                    y += 20f;

                    foreach (var f in playing)
                    {
                        if (GUI.Button(new Rect(x, y, iw, 26f),
                                       "invite " + SteamFriends.GetFriendPersonaName(f)))
                            InviteFriend(f);
                        y += 30f;
                    }
                    y += 4f;
                }

                if (SteamBoot.Running && GUI.Button(new Rect(x, y, iw, 26f),
                        "Steam overlay invite (only if launched from Steam)"))
                    Invite();
                y += 32f;

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
    /// Choose a microphone and prove it works, before joining anything.
    ///
    /// Microphone.devices[0] is whatever Windows lists first, and on a machine
    /// with a webcam, a headset and a capture card that is very often the
    /// wrong one - which is indistinguishable from voice being broken.
    ///
    /// The TEST holds the mic open on the menu with the level bar showing, so
    /// somebody can say "hello" and SEE it move. Deliberately usable with no
    /// session, no lobby and no second player, because that is exactly when
    /// you want to find out - not while a crewmate is bleeding out.
    /// </summary>
    float DrawMicSettings(float x, float y, float iw, GUIStyle body)
    {
        var devices = VoiceMic.Devices;

        if (devices.Length == 0)
        {
            var warn = new GUIStyle(body);
            warn.normal.textColor = new Color(1f, 0.5f, 0.4f);

            GUI.Label(new Rect(x, y, iw, 34f),
                      "NO MICROPHONE FOUND. The game runs, but nobody will " +
                      "hear you. Check Windows sound settings.", warn);
            return y + 38f;
        }

        GUI.Label(new Rect(x, y, iw, 18f), "microphone", body);
        y += 20f;

        // One button per device. A dropdown in IMGUI is a fight; four buttons
        // are not, and nobody has forty microphones.
        foreach (var d in devices)
        {
            bool current = d == VoiceMic.Device;

            GUI.enabled = !current;
            if (GUI.Button(new Rect(x, y, iw, 24f), (current ? "> " : "   ") + d))
                VoiceMic.Use(d);
            GUI.enabled = true;
            y += 27f;
        }

        y += 4f;

        if (GUI.Button(new Rect(x, y, 150f, 26f),
                       VoiceMic.Testing ? "stop test" : "TEST MIC"))
        {
            if (VoiceMic.Testing) VoiceMic.StopTest();
            else VoiceMic.StartTest();
        }

        // The bar, beside the button rather than under it, so pressing TEST
        // and watching it move is one glance rather than two.
        var bar = new Rect(x + 160f, y + 7f, iw - 160f, 12f);

        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);

        GUI.color = new Color(0.55f, 1f, 0.65f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * VoiceMic.Level, bar.height),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        y += 30f;

        if (VoiceMic.Testing)
        {
            GUI.Label(new Rect(x, y, iw, 18f), "say something - the bar should move", body);
            y += 20f;
        }

        return y;
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
