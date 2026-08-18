// RunManager.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/RunManager.cs
// Goes on: an empty GameObject named "RunManager" at the scene root.
//
// ========================================================================
// THIS IS WHAT MAKES IT A GAME INSTEAD OF A SANDBOX.
//
// Three systems, and they only work together:
//
//   QUOTA       the gang wants a number. Without it, grabbing one cash
//               bundle and climbing straight out is a winning strategy, so
//               there is no reason to ever go deep. The quota is what makes
//               greed mandatory rather than optional.
//
//   EXTRACTION  DISABLED IN STEP 2. It measured depth against the rope's
//               anchor, and the rope is gone. It comes back in Step 10,
//               driven by the elevator reaching the surface.
//               Until then a run cannot end, so the results screen and the
//               shop below are unreachable. That is expected.
//
//   COLLAPSE    charges go off floor by floor FROM THE ROOF DOWN. The way
//               out closes behind you rather than a timer ticking in the
//               corner, so the pressure is always about the exit.
//
// The collapse also does the thing you designed: the easy top floors stop
// existing. Farming a floor you know is not a slow strategy, it is a slow
// death, and the only direction that still has value in it is down.
// ========================================================================

using System.Collections.Generic;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public enum RunState { Active, Extracted, Buried }

    [Header("Quota")]
    [Tooltip("Read from Campaign at runtime - shown here for reference only. " +
             "It rises every run.")]
    public int quota = 800;

    [Header("The deadline")]
    // ------------------------------------------------------------------
    // ONE RUN, ONE DEADLINE, SHARED BY EVERYONE.
    //
    // When it reaches zero the government fires the charges. Anyone still
    // below the surface dies - and if ANY member of the crew dies, the whole
    // run is lost. Not "you continue with three players". Everyone loses.
    //
    // That rule is what turns the timer from an annoyance into the loudest
    // thing in the game. You cannot leave without accounting for every
    // single person, which means the last two minutes of every run are four
    // people shouting positions at each other.
    // ------------------------------------------------------------------

    [Tooltip("Seconds in a run before the charges fire.\n\n" +
             "Set per floor: a floor with a long puzzle chain gets more time " +
             "than a smash-and-grab. Around 10 to 20 minutes shipped; keep it " +
             "much shorter while testing or you will lose a whole afternoon " +
             "to five runs.")]
    public float runTime = 600f;

    [Tooltip("Seconds before zero that the first warning fires.")]
    public float firstWarning = 120f;

    [Tooltip("Seconds before zero at which the countdown becomes constant and " +
             "loud - the point where anyone still deep has already lost.")]
    public float panicWindow = 30f;

    [Tooltip("Turn the deadline off entirely while tuning other things.")]
    public bool enableCollapse = true;

    [Header("Room charges (mid-run)")]
    [Tooltip("Seconds between random room seals while you are still down there.")]
    public float roomChargeTime = 600f;

    [Tooltip("Warning starts this many seconds before a room seals.")]
    public float roomWarnTime = 60f;

    public RunState State { get; private set; } = RunState.Active;
    public bool IsRunActive => State == RunState.Active;
    public int Recovered { get; private set; }
    public int FloorsLost => Campaign.DestroyedRooms.Count;

    PlayerMotor player;
    PlayerBackpack backpack;

    // Every player in the shaft. The collapse checks all of them, because a
    // single person left behind loses the run for the entire crew.
    //
    // Typed as PlayerMotor since the rope went: the only things this list is
    // used for are the transform (is this player inside a sealing room) and
    // the name (who is it), and every player has a motor.
    readonly List<PlayerMotor> crew = new List<PlayerMotor>();

    readonly List<Transform> levels = new List<Transform>();
    readonly HashSet<int> sealedThisRun = new HashSet<int>();
    Material rubbleMat;
    float runStartTime;
    float nextRoomDeadline;
    int threatenedRoom;
    bool roomWarned;
    string lastEvent = "";
    float lastEventTime = -99f;

    public float TimeLeft => Mathf.Max(0f, nextRoomDeadline - Time.time);

    void Start()
    {
        crew.AddRange(FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None));
        player = crew.Count > 0 ? crew[0] : null;
        backpack = player != null ? player.GetComponent<PlayerBackpack>() : null;

        CollectLevels();
        CacheRubbleMaterial();
        ApplyCampaign();

        runStartTime = Time.time;
        ScheduleNextRoomCharge(initial: true);
    }

    /// <summary>
    /// Pull the persistent campaign state into this run: how much rope you
    /// bought, which floors the government has already blown, what the mafia
    /// wants this time.
    ///
    /// Done here rather than saved into the scene, so restarting a run is
    /// just a scene reload - the shaft rebuilds itself clean and Campaign
    /// remembers everything that matters.
    /// </summary>
    void ApplyCampaign()
    {
        quota = Campaign.Quota;

        if (backpack != null) backpack.slots = Campaign.BackpackSlots;

        foreach (int room in Campaign.DestroyedRooms)
            SealRoomIndex(room, killOccupants: false);

        if (Campaign.RopeIsUseless)
            Announce("your cable only reaches rooms that are already gone");
    }

    void CacheRubbleMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh != null)
        {
            rubbleMat = new Material(sh);
            rubbleMat.SetColor("_BaseColor", new Color(0.28f, 0.24f, 0.22f));
            rubbleMat.SetFloat("_Smoothness", 0.08f);
        }
    }

    // The graybox builder names levels Level_01 downward, so floor 1 is the
    // shallowest and collapses first. Collecting them in order means the
    // collapse can just walk the list.
    void CollectLevels()
    {
        var shaft = GameObject.Find("SHAFT");
        if (shaft == null)
        {
            Debug.LogWarning("[RunManager] No SHAFT found - collapse disabled.");
            return;
        }

        for (int i = 1; i <= 99; i++)
        {
            var t = shaft.transform.Find($"Level_{i:00}");
            if (t == null) break;
            levels.Add(t);
        }
    }

    void Update()
    {
        if (State != RunState.Active) return;

        // EXTRACTION IS GONE UNTIL STEP 10.
        //
        // It measured "are you out" as depth below the rope's anchor, and
        // there is no rope and no anchor now. Rather than invent a stand-in
        // that Step 10 would only have to unpick, the run simply cannot end
        // yet. The collapse below still runs, so the floor still kills you.
        UpdateCollapse();
    }

    int CrewSize
    {
        get
        {
            int n = 0;
            foreach (var m in crew) if (m != null) n++;
            return n;
        }
    }

    /// <summary>
    /// Ends the run and counts the haul. Nothing calls this in Step 2 -
    /// Step 10 wires it to the elevator arriving at the surface.
    /// </summary>
    void Extract()
    {
        // Surfacing commits the currently charged room — even if you leave
        // early. Stay longer = more timers complete mid-run = more rooms gone.
        OnExtractSeal();
        Recovered = CountRecoveredValue();
        State = RunState.Extracted;
    }

    /// <summary>
    /// Rooms lost when leaving:
    ///   already sealed mid-run (each finished 10-min charge)
    /// + the room that was currently counting down (always seals on exit)
    ///
    /// Leave before first charge ends  -> 1 room
    /// One charge done + second started -> 2 rooms
    /// Two charges done + third started -> 3 rooms
    /// </summary>
    void OnExtractSeal()
    {
        if (threatenedRoom > 0 && !IsRoomSealed(threatenedRoom))
        {
            SealRoomIndex(threatenedRoom, killOccupants: false);
            Campaign.SealRoom(threatenedRoom);
            sealedThisRun.Add(threatenedRoom);
            Announce($"left the shaft — room {threatenedRoom:00} sealed behind you");
        }
    }

    /// <summary>
    /// Everything you actually got out with: clipped to the rope, on your
    /// back, or in your hands. Loot still lying on a floor does not count,
    /// which is the entire reason the rope matters.
    /// </summary>
    int CountRecoveredValue()
    {
        int total = 0;

        foreach (var c in FindObjectsByType<Carryable>(FindObjectsSortMode.None))
        {
            if (c == null) continue;

            // Cargo on the elevator deck joins this list in Step 8.
            switch (c.State)
            {
                case Carryable.CarryState.Stowed:
                case Carryable.CarryState.Held:
                    total += c.value;
                    break;
            }
        }

        return total;
    }

    // --------------------------------------------------------------------
    // ROOM CHARGES — random reachable room seals with rubble.
    // --------------------------------------------------------------------

    void UpdateCollapse()
    {
        if (!enableCollapse) return;
        if (levels.Count == 0) return;

        if (threatenedRoom <= 0 || IsRoomSealed(threatenedRoom) || !IsRoomReachable(threatenedRoom))
            ChooseThreatenedRoom();

        if (threatenedRoom <= 0)
            return;

        float left = TimeLeft;

        if (!roomWarned && left <= roomWarnTime)
        {
            roomWarned = true;
            Announce($"ROOM {threatenedRoom:00} CHARGED — get out or die");
        }

        if (left > 0f) return;

        bool killed = SealRoomIndex(threatenedRoom, killOccupants: true);
        Campaign.DestroyedRooms.Add(threatenedRoom);
        sealedThisRun.Add(threatenedRoom);

        if (killed)
        {
            State = RunState.Buried;
            Campaign.CampaignOver = true;
            Campaign.EpitaphReason = $"you were inside room {threatenedRoom:00} when it sealed";
            Announce($"ROOM {threatenedRoom:00} COLLAPSED ON YOU");
            return;
        }

        Announce($"ROOM {threatenedRoom:00} SEALED — door is rubble");
        ScheduleNextRoomCharge(initial: false);
    }

    void ScheduleNextRoomCharge(bool initial)
    {
        float t = initial ? Mathf.Min(runTime, roomChargeTime) : roomChargeTime;
        nextRoomDeadline = Time.time + t;
        roomWarned = false;
        threatenedRoom = 0;
        ChooseThreatenedRoom();
    }

    void ChooseThreatenedRoom()
    {
        var opts = new List<int>();
        int deep = Campaign.DeepestReachableFloor;
        for (int i = 1; i <= levels.Count; i++)
        {
            if (IsRoomSealed(i)) continue;
            if (i > deep) continue;
            opts.Add(i);
        }

        threatenedRoom = opts.Count == 0 ? 0 : opts[Random.Range(0, opts.Count)];
    }

    bool IsRoomSealed(int room1Based)
    {
        return Campaign.DestroyedRooms.Contains(room1Based) || sealedThisRun.Contains(room1Based);
    }

    bool IsRoomReachable(int room1Based)
    {
        return room1Based >= 1 && room1Based <= Campaign.DeepestReachableFloor;
    }

    bool SealRoomIndex(int room1Based, bool killOccupants)
    {
        int idx = room1Based - 1;
        if (idx < 0 || idx >= levels.Count) return false;
        var level = levels[idx];
        if (level == null) return false;

        if (level.Find("RubbleSeal") == null)
            RoomSeal.SealDoorway(level, rubbleMat);

        if (!killOccupants) return false;

        foreach (var member in crew)
        {
            if (member == null) continue;
            if (RoomSeal.IsPlayerInside(level, member.transform))
                return true;
        }
        return false;
    }

    string SealedRoomsLabel()
    {
        if (Campaign.DestroyedRooms.Count == 0)
            return "rooms sealed: none";

        var all = new List<int>(Campaign.DestroyedRooms);
        all.Sort();
        var parts = new List<string>();
        foreach (int r in all) parts.Add($"{r:00}");
        return "rooms sealed: " + string.Join(", ", parts) +
               $"    live in reach: {Campaign.LiveRoomsInReach}";
    }

    void Announce(string message)
    {
        lastEvent = message;
        lastEventTime = Time.time;
        Debug.Log($"[Run] {message}");
    }

    // --------------------------------------------------------------------
    // PROTOTYPE HUD. Throwaway - replace with a real canvas.
    // --------------------------------------------------------------------

    void OnGUI()
    {
        if (State != RunState.Active)
        {
            // The results screen needs the mouse back.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            DrawResults();
            return;
        }

        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

        int carried = CountRecoveredValue();
        bool met = carried >= quota;

        style.normal.textColor = met
            ? new Color(0.5f, 0.95f, 0.5f)
            : new Color(1f, 1f, 1f, 0.75f);

        GUI.Label(new Rect(24f, 44f, 480f, 22f),
            $"quota  {carried} / {quota}" + (met ? "   MET" : ""), style);

        // A COUNTDOWN, NOT A BAR. A number is something one player can shout
        // at the others; a bar is something each of them has to look at.
        // In a game whose whole tension is four people coordinating an exit,
        // that difference matters more than it sounds.
        if (enableCollapse)
        {
            float left = TimeLeft;
            bool panic = left <= roomWarnTime;

            var warn = new GUIStyle(GUI.skin.label)
            { fontSize = panic ? 20 : 14 };

            warn.normal.textColor = panic
                ? new Color(1f, 0.25f, 0.2f)
                : new Color(1f, 1f, 1f, 0.65f);

            int minutes = Mathf.FloorToInt(left / 60f);
            int seconds = Mathf.FloorToInt(left % 60f);

            string threat = threatenedRoom > 0
                ? $"room {threatenedRoom:00} seals in {minutes}:{seconds:00}"
                : "no live rooms in reach";

            GUI.Label(new Rect(24f, 66f, 560f, 30f), threat, warn);

            var dash = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            dash.normal.textColor = new Color(1f, 0.75f, 0.45f, 0.85f);
            GUI.Label(new Rect(24f, 92f, 700f, 22f), SealedRoomsLabel(), dash);

        }

        if (Time.time - lastEventTime < 4f && !string.IsNullOrEmpty(lastEvent))
        {
            var big = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(1f, 0.5f, 0.25f,
                Mathf.Clamp01(4f - (Time.time - lastEventTime)));

            GUI.Label(new Rect(0f, Screen.height * 0.22f, Screen.width, 30f), lastEvent, big);
        }
    }

    // --------------------------------------------------------------------
    // BETWEEN RUNS: settle up, then the shop.
    //
    // Deliberately one screen, not two. Seeing "you were paid 2400" directly
    // above "rope costs 180 a floor" is what makes the decision land - the
    // money you just risked your friends for is the same money that buys the
    // depth you need next time.
    // --------------------------------------------------------------------

    bool settled;
    bool survivedSettlement;

    void DrawResults()
    {
        if (!settled)
        {
            settled = true;
            survivedSettlement = State == RunState.Extracted && Campaign.Settle(Recovered);
            if (State == RunState.Buried && string.IsNullOrEmpty(Campaign.EpitaphReason))
            {
                Campaign.CampaignOver = true;
                Campaign.EpitaphReason = "somebody was still inside when a room sealed";
            }
        }

        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        GUI.color = Color.white;

        var title = new GUIStyle(GUI.skin.label)
        { fontSize = 34, alignment = TextAnchor.MiddleCenter };
        var body = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleCenter };

        float y = Screen.height * 0.16f;

        // ---- headline ----
        string headline;
        if (State == RunState.Buried)
        {
            title.normal.textColor = new Color(1f, 0.3f, 0.25f);
            headline = "BURIED";
        }
        else if (survivedSettlement)
        {
            title.normal.textColor = new Color(0.5f, 0.95f, 0.5f);
            headline = "OUT, AND PAID";
        }
        else
        {
            title.normal.textColor = new Color(1f, 0.6f, 0.25f);
            headline = "OUT, AND SHORT";
        }

        GUI.Label(new Rect(0f, y, Screen.width, 44f), headline, title);

        body.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(0f, y + 54f, Screen.width, 24f),
            $"run {Campaign.RunNumber}      recovered {Recovered}      " +
            $"quota {quota}      crew {CrewSize}", body);

        if (Campaign.CampaignOver)
        {
            body.normal.textColor = new Color(1f, 0.45f, 0.35f);
            GUI.Label(new Rect(0f, y + 90f, Screen.width, 24f),
                Campaign.EpitaphReason, body);

            body.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(0f, y + 124f, Screen.width, 24f),
                "the mafia does not take excuses.", body);

            if (GUI.Button(new Rect(Screen.width * 0.5f - 110f, y + 170f, 220f, 38f),
                           "start over"))
            {
                Campaign.Reset();
                ReloadScene();
            }
            return;
        }

        DrawShop(y + 100f, body);
    }

    void DrawShop(float y, GUIStyle body)
    {
        float cx = Screen.width * 0.5f;

        var head = new GUIStyle(GUI.skin.label)
        { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        head.normal.textColor = new Color(1f, 0.85f, 0.4f);
        GUI.Label(new Rect(0f, y, Screen.width, 28f), $"MONEY  {Campaign.Money}", head);

        // The two numbers that decide whether you have a game left.
        //
        // Shown together on purpose. A player who walks into the dead end
        // has to have watched themselves do it - "rope reaches floor 3,
        // floors 1-3 destroyed" is a sentence you can act on. Finding out
        // twenty minutes into a run is not.
        var status = new GUIStyle(GUI.skin.label)
        { fontSize = 15, alignment = TextAnchor.MiddleCenter };

        status.normal.textColor = Campaign.RopeIsUseless
            ? new Color(1f, 0.35f, 0.3f)
            : (Campaign.LiveRoomsInReach <= 1
                ? new Color(1f, 0.7f, 0.3f)
                : new Color(1f, 1f, 1f, 0.65f));

        GUI.Label(new Rect(0f, y + 34f, Screen.width, 22f),
            $"rope {Campaign.RopeLength:0}m reaches floor {Campaign.DeepestReachableFloor}" +
            $"      sealed rooms: {Campaign.DestroyedRooms.Count}" +
            $"      live rooms in reach: {Campaign.LiveRoomsInReach}", status);

        if (Campaign.RopeIsUseless)
        {
            status.normal.textColor = new Color(1f, 0.35f, 0.3f);
            GUI.Label(new Rect(0f, y + 56f, Screen.width, 22f),
                "no live rooms left in rope range. buy rope or this is over.",
                status);
        }

        float by = y + 92f;

        // ---- rope ----
        GUI.enabled = Campaign.Money >= Campaign.RopeChunkCost;
        if (GUI.Button(new Rect(cx - 250f, by, 240f, 40f),
                       $"+{Campaign.RopeChunk}m rope   ({Campaign.RopeChunkCost})"))
        {
            Campaign.BuyRope();
        }

        // ---- backpack ----
        GUI.enabled = Campaign.Money >= Campaign.BackpackSlotCost && Campaign.BackpackSlots < 6;
        if (GUI.Button(new Rect(cx + 10f, by, 240f, 40f),
                       $"+1 pack slot   ({Campaign.BackpackSlotCost})"))
        {
            Campaign.BuyBackpackSlot();
        }

        GUI.enabled = true;

        body.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
        GUI.Label(new Rect(0f, by + 46f, Screen.width, 22f),
            $"next run quota {Campaign.BaseQuota + Campaign.RunNumber * Campaign.QuotaStep}" +
            $"  —  each 10 min a room seals; leaving seals the charged room too", body);

        if (GUI.Button(new Rect(cx - 110f, by + 82f, 220f, 40f), "go back down"))
        {
            Campaign.AdvanceRun();
            ReloadScene();
        }
    }

    void ReloadScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
    }
}
