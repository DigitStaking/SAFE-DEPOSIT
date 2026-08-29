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
    // Lost is a THIRD failure, not a synonym for Buried.
    //
    // Buried is a body under a slab: nothing to recover, nothing to pay for.
    // Lost is somebody still down there, alive as far as anyone knows, and
    // ECONOMY Part 7 insists on the distinction - "Lost is not death; dying
    // is failing to pay for the rescue." Collapsing the two would delete the
    // rescue contract before Step 9 gets to build it.
    public enum RunState { Active, Extracted, Buried, Lost }

    [Header("Quota")]
    [Tooltip("Read from Campaign at runtime - shown here for reference only. " +
             "It rises every run.")]
    public int quota = 200;   // Campaign.BaseMafia; overwritten from Campaign at Start

    // ------------------------------------------------------------------
    // THERE IS NO RUN TIMER. THE ROOMS DIE, NOT YOU.
    //
    // runTime = 600 used to sit here as a hard cap on the whole run, and
    // DEMO_PLAN.md's consistency check flagged it as one of four things
    // that were "genuinely not logical yet": "The design says you can stay
    // as long as you like - the ROOMS die, not you. A hard 10-minute cap
    // contradicts the entire pressure system."
    //
    // It is gone, along with firstWarning and panicWindow, which were read
    // by nothing at all - leftovers from the rope-era ascent countdown.
    //
    // roomChargeTime below is the real clock and always was. Every 10
    // minutes you are still down there, a room seals; leaving seals the one
    // currently counting down. That produces the design's
    // floor(runMinutes / 10) + 1 without ever telling a crew to hurry up.
    // ------------------------------------------------------------------

    [Tooltip("Turn the collapse off entirely while tuning other things.")]
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

    // The 'player' field is gone with Phase 3 Step 5. It was crew[0] - "the
    // player" - and every use of it was a question that should have been
    // asked of the whole list.

    // The single 'backpack' field is gone with Phase 3 Step 4. Packs are
    // per-person now, so one cached reference to the first player's pack was
    // a shortcut that could only ever configure one of four people.

    // Every player in the shaft, from PlayerRegistry.
    //
    // The collapse checks all of them. It used to end the campaign the moment
    // ANY of them was caught, on the old rule that "a single person left
    // behind loses the run for the entire crew" - which was true when the
    // crew was one person. Phase 3 Step 5: the room takes the people in it,
    // the run continues while anybody is still standing, and going back for
    // them is a decision rather than a formality.
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
        // The registry, not a sweep. PlayerMotor registers in OnEnable and
        // Unity runs every OnEnable before any Start, so by the time this
        // line executes the list is complete - and it is the SAME list every
        // other system reads, which a second independent scan would not be.
        crew.AddRange(PlayerRegistry.All);

        CollectLevels();
        CacheRubbleMaterial();
        ApplyCampaign();

        runStartTime = Time.time;
        ScheduleNextRoomCharge(initial: true);
    }

    /// <summary>
    /// Pull the persistent campaign state into this run: how much cable you
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

        // Everyone's own pack, not one number applied to whoever happened to
        // be first in the list.
        foreach (var m in crew)
        {
            if (m == null) continue;
            // No longer pushed. PlayerBackpack.Capacity asks the Crew row
            // itself, so buying a slot or spending a spray takes effect the
            // moment it happens rather than at the start of the next round.
            var pack = m.GetComponent<PlayerBackpack>();
            if (pack != null) pack.slots = Crew.Of(m.Slot).LootSlots;
        }

        RebuildRubbleFromCampaign();

        if (Campaign.CableIsUseless)
            Announce("your cable only reaches rooms that are already gone");
    }

    /// <summary>
    /// Put rubble in every doorway Campaign says is gone.
    ///
    /// PHASE 4 STEP 8. Public because a client learns about demolition as a
    /// number arriving on the wire, long after this scene was built - so
    /// something has to be able to say "the building changed, look again".
    ///
    /// Safe to call repeatedly: SealRoomIndex is idempotent, and a room that
    /// is already rubble stays rubble.
    /// </summary>
    public void RebuildRubbleFromCampaign()
    {
        foreach (int room in Campaign.DestroyedRooms)
            SealRoomIndex(room, killOccupants: false);
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

    /// <summary>
    /// The bleed-out completed. Ends the run distinctly from being buried:
    /// the crew is not dead, somebody is still down there. Idempotent, like
    /// Extract - a second call from a second listener changes nothing.
    ///
    /// Step 8 turns this into a named roster entry and an absence from the
    /// next run; Step 9 prices getting them back. For now it is the outcome,
    /// which is what Step 5 asks for: "the clock reaching zero does something
    /// distinct from dying".
    /// </summary>
    public void OnBleedOut(PlayerMotor who)
    {
        // WHO, and WHERE. Both, because the rescue contract prices by depth -
        // ECONOMY: Rescue(R, f) = Mafia(R) x (1 + f/10). Losing somebody on
        // floor 3 and losing them on floor 18 are different problems.
        //
        // The name is PASSED IN as of Phase 3 Step 5. This used to search for
        // "a DownedPlayer" and name whoever turned up first, which is right
        // by luck with one body and a coin flip with two - and the results
        // screen would then confidently name the wrong person.
        string name = who != null ? who.gameObject.name : "a crewmate";
        Campaign.RecordLost(name, CurrentFloorOfLift());

        // What they were carrying goes with them. Sprays are personal as of
        // 26 Aug 2026, and this is the line that makes that mean something:
        // lose the medic and the crew loses its rescues.
        var lostMotor = PlayerRegistry.OwnerOf(who);
        if (lostMotor != null) Campaign.LoseCarriedSupplies(lostMotor.Slot);

        // A CREW SURVIVES LOSING ONE OF ITS PEOPLE. The run only ends when
        // there is nobody left standing to finish it - which is the whole
        // reason Lost is not Buried, and the reason four players can keep
        // working while somebody lies on floor seven.
        if (CrewStanding > 0)
        {
            Announce($"{name} BLED OUT - nobody came");
            return;
        }

        if (State != RunState.Active) return;
        State = RunState.Lost;
        Announce("BLED OUT - nobody came");
    }

    /// <summary>
    /// The cable parted. Everyone aboard is Lost - not Buried. They are at
    /// the bottom of a shaft rather than under a slab, which is the same
    /// distinction the bleed-out makes and the one the rescue contract is
    /// priced on.
    ///
    /// PHASE2_SPEC Step 10: "At 100% the cable snaps: everyone aboard is
    /// Lost, the run is over."
    /// </summary>
    public void OnCableSnapped(int floor)
    {
        if (State != RunState.Active) return;

        foreach (var m in crew)
        {
            if (m == null) continue;
            Campaign.RecordLost(m.gameObject.name, floor);
            Campaign.LoseCarriedSupplies(m.Slot);
        }

        State = RunState.Lost;
        Announce("THE CABLE PARTED");
    }

    void Update()
    {
        if (State != RunState.Active) return;

        // EXTRACTION IS NO LONGER CHECKED HERE.
        //
        // It used to poll "is everyone above depth X" every frame, because
        // with a rope there was no single moment that meant 'the run is
        // over' - people trickled up one at a time. The elevator replaces
        // that with an actual event: the car reaches floor 0 with the crew
        // inside. So extraction is PUSHED by ElevatorDashboard calling
        // Extract() rather than polled for, and this method is left with
        // only the collapse to run.
        UpdateCollapse();
    }

    /// <summary>Everyone who started the run, standing or not.</summary>
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
    /// Everyone still on their feet. Step 8 needs the distinction: a downed
    /// player is still a PlayerMotor in the scene, so CrewSize never drops
    /// when somebody goes down, and "is there anybody left to walk back in"
    /// has to ask a different question than "how many did we bring".
    /// </summary>
    int CrewStanding
    {
        get
        {
            int n = 0;
            foreach (var m in crew)
            {
                if (m == null) continue;
                var h = m.GetComponent<PlayerHealth>();
                if (h != null && h.IsDowned) continue;
                n++;
            }
            return n;
        }
    }

    /// <summary>
    /// Ends the run and counts the haul. Called by ElevatorDashboard the
    /// moment the car reaches the surface with the crew aboard - the
    /// departure vote and the "who is missing" check live there, because
    /// that is where the button is. This method's only job is to settle up.
    ///
    /// Idempotent: a second call after the run has already ended does
    /// nothing, so a stray arrival event cannot double-count the haul.
    /// </summary>
    public void Extract()
    {
        if (State != RunState.Active) return;

        // Surfacing commits the currently charged room — even if you leave
        // early. Stay longer = more timers complete mid-run = more rooms gone.
        OnExtractSeal();

        // Order matters. OnExtractSeal has already destroyed the loot in any
        // room that just sealed, so those items are gone before the snapshot
        // and drop out of the campaign for good - which is the point of the
        // demolition.
        Recovered = CountRecoveredValue(out var sold);

        // The building keeps what you did not take. Everything still standing
        // is recorded exactly where it lies, so next round's floors are the
        // ones this crew left behind rather than a fresh set.
        LootSpawner.CaptureRemaining(sold);

        // ==============================================================
        // SOLD MEANS GONE. THE CREW HANDED IT OVER.
        //
        // Counting it and leaving it lying there was survivable while a round
        // change ended the session and reloaded everything - the objects went
        // with the scene, so nobody noticed they had never actually been
        // taken away.
        //
        // Step 8 changed that. Players now PERSIST across the round load, and
        // a stowed item is PARENTED TO ITS CARRIER - so anything still in a
        // backpack rode into round 2 in that backpack, was found again by
        // CountRecoveredValue as Stowed, and was paid for a second time. And a
        // third. Free money for as long as you never took it out.
        //
        // Reported as exactly that, with the tell attached: the items could be
        // pulled back out and could not be picked up again, because the roster
        // had correctly written them off while the objects themselves lived on.
        //
        // Destroyed here, at the moment they are paid for. The packs are
        // cleared too - a bag holding six destroyed references reports itself
        // full and quietly refuses the next round's loot.
        // ==============================================================
        foreach (var m in crew)
        {
            if (m == null) continue;
            var pack = m.GetComponent<PlayerBackpack>();
            if (pack != null) pack.ClearSold(sold);
        }

        int handedOver = 0;
        foreach (var c in sold)
        {
            if (c == null) continue;
            Destroy(c.gameObject);
            handedOver++;
        }

        if (handedOver > 0)
            Debug.Log($"[Run] {handedOver} item(s) handed over for ${Recovered} " +
                      "and removed - they are the mafia's now.");

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
    /// Everything you actually got out with: in a player's hands, on their
    /// back, or lying loose anywhere inside the car. Loot still on a floor
    /// does not count, which is the entire reason the load limit matters.
    ///
    /// The "loose in the car" half is deliberately the SAME rule
    /// ElevatorDeck uses to compute load - if its mass counted against the
    /// cable on the way up, its value counts on arrival. Anything else
    /// would mean loot that costs you capacity but pays nothing.
    /// </summary>
    int CountRecoveredValue() => CountRecoveredValue(out _);

    /// <summary>
    /// Also hands back the exact set it paid out for, so LootSpawner can
    /// record everything ELSE without the two ever disagreeing about whether
    /// a given crate came home. One rule, evaluated once.
    /// </summary>
    int CountRecoveredValue(out HashSet<Carryable> sold)
    {
        int total = 0;
        sold = new HashSet<Carryable>();

        var deckLoot = new HashSet<Carryable>();
        var lift = SceneRefs.Lift;
        if (lift != null)
            foreach (var rb in lift.Riders)
            {
                if (rb == null) continue;
                var c = rb.GetComponent<Carryable>();
                if (c != null) deckLoot.Add(c);
            }

        foreach (var c in FindObjectsByType<Carryable>(FindObjectsSortMode.None))
        {
            if (c == null) continue;

            switch (c.State)
            {
                case Carryable.CarryState.Stowed:
                case Carryable.CarryState.Held:
                    total += c.value;
                    sold.Add(c);
                    break;

                case Carryable.CarryState.Free:
                    if (deckLoot.Contains(c)) { total += c.value; sold.Add(c); }
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

        // Through SealRoom, not straight into the set. SealRoom is host-only
        // and publishes the result; a bare Add would seal the room on this
        // machine and tell nobody, which is the bug this whole change is for.
        Campaign.SealRoom(threatenedRoom);
        sealedThisRun.Add(threatenedRoom);

        if (killed)
        {
            // THE ROOM TAKES THE PEOPLE IN IT, NOT THE RUN.
            //
            // This used to end the campaign outright the moment anybody was
            // caught - correct behaviour when "anybody" and "everybody" were
            // the same person, and wrong the moment they are not. Three
            // crewmates who got out do not lose the building because the
            // fourth was slow.
            //
            // Everyone caught goes down where they stood, which starts their
            // bleed-out and makes them a Carryable - so a room sealing is now
            // survivable IF somebody goes back for them, and that is exactly
            // the decision the collapse exists to force.
            foreach (var member in caughtInSeal)
            {
                if (member == null) continue;
                var h = member.GetComponent<PlayerHealth>();
                if (h != null && !h.IsDowned)
                    h.TakeDamage(Crew.MaxHealth, $"room {threatenedRoom:00} sealed");
            }

            string names = NamesOf(caughtInSeal);
            Announce($"ROOM {threatenedRoom:00} COLLAPSED ON {names}");

            // Only when it took the last person standing is the run over, and
            // even then it is Buried rather than Lost: they are under a slab,
            // not lying somewhere a rescue could reach.
            if (CrewStanding <= 0)
            {
                State = RunState.Buried;
                Campaign.CampaignOver = true;
                Campaign.EpitaphReason =
                    $"{names} was inside room {threatenedRoom:00} when it sealed";
                return;
            }

            ScheduleNextRoomCharge(initial: false);
            return;
        }

        Announce($"ROOM {threatenedRoom:00} SEALED — door is rubble");
        ScheduleNextRoomCharge(initial: false);
    }

    void ScheduleNextRoomCharge(bool initial)
    {
        // Was Mathf.Min(runTime, roomChargeTime) for the first charge - the
        // last thing runTime did. With no run timer there is nothing to take
        // a minimum against: every charge, first or not, is one full
        // roomChargeTime.
        nextRoomDeadline = Time.time + roomChargeTime;
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

        // NAMES, not a boolean. "Somebody was inside" is what this used to
        // answer, and with four people it is not enough to act on - the crew
        // needs to know WHO the room took, and Campaign.LostCrew needs it to
        // price getting them back.
        caughtInSeal.Clear();
        foreach (var member in crew)
        {
            if (member == null) continue;
            if (RoomSeal.IsPlayerInside(level, member.transform))
                caughtInSeal.Add(member);
        }
        return caughtInSeal.Count > 0;
    }

    readonly List<PlayerMotor> caughtInSeal = new List<PlayerMotor>();

    static string NamesOf(List<PlayerMotor> people)
    {
        if (people == null || people.Count == 0) return "nobody";

        var sb = new System.Text.StringBuilder();
        foreach (var p in people)
        {
            if (p == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(p.gameObject.name);
        }
        return sb.Length > 0 ? sb.ToString() : "nobody";
    }

    /// <summary>
    /// Which floor the crew went down on, for the epitaph. Read from the
    /// elevator rather than tracked, because the car is where they were.
    /// </summary>
    int CurrentFloorOfLift()
    {
        var lift = SceneRefs.Lift;
        return lift != null ? lift.CurrentFloor : 0;
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

        // ----------------------------------------------------------------
        // THE BANK BELONGS ON THIS LINE, AND NOT ONLY FOR CONVENIENCE.
        //
        // Campaign.Settle() pays the mafia out of MONEY PLUS THE HAUL:
        //   Money += recovered; if (Money < owed) you are dead.
        //
        // So banked money counts toward the quota, and this readout used to
        // ignore it entirely - it compared the haul alone against the number
        // and said you were short while you were actually clear. With 170 in
        // the bank and a 214 quota you needed 44, and the HUD said 214.
        //
        // Showing the bank fixes the display; comparing against bank + haul
        // fixes the LIE. "Still need" is the only number anyone actually
        // wants: it is the answer to "can we leave yet".
        // ----------------------------------------------------------------
        int carried = CountRecoveredValue();
        int need = Mathf.Max(0, quota - Campaign.Money - carried);
        bool met = need <= 0;

        style.normal.textColor = met
            ? new Color(0.5f, 0.95f, 0.5f)
            : new Color(1f, 1f, 1f, 0.75f);

        GUI.Label(new Rect(24f, 44f, 760f, 22f),
            $"bank {Campaign.Money}   carrying {carried}   quota {quota}   " +
            (met ? "QUOTA MET" : $"still need {need}"), style);

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

            // A run nobody came back from pays nothing. The haul is still in
            // the building, on the floor next to whoever was carrying it.
            // BEING LOST IS NOT THE END OF THE CAMPAIGN.
            //
            // It used to set CampaignOver, which made Lost a synonym for dead
            // and deleted the whole point of it. ECONOMY Part 7: "Lost is not
            // death; dying is failing to pay for the rescue." The building
            // still has you in it and the shop is about to offer a price.
            //
            // The one case that IS terminal is having nobody left to send -
            // and that is a fact about the CREW, not about being lost. With
            // four players the run just continues short-handed. Solo, there is
            // nobody to walk back in, so the campaign ends here unless Step 9
            // gives you a way to buy yourself out.
            if (State == RunState.Lost && CrewStanding <= 0 &&
                string.IsNullOrEmpty(Campaign.EpitaphReason))
            {
                Campaign.CampaignOver = true;
                Campaign.EpitaphReason = Campaign.CableStrain >= 1f
                    ? "the cable parted. it gave you ten seconds"
                    : "there is nobody left above ground to come back for you";
            }

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
        if (State == RunState.Lost)
        {
            title.normal.textColor = new Color(0.8f, 0.2f, 0.2f);
            headline = "LOST";
        }
        else if (State == RunState.Buried)
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
            $"quota {quota}      crew {CrewStanding}/{CrewSize}", body);

        // ---- who did not come back ----
        //
        // PHASE2_SPEC Step 8: "a run can end with someone missing and the
        // game says who." A count would say a problem exists; a NAME says
        // whose problem it is, which is the same reason the departure vote
        // names the person still in a room instead of counting heads.
        float missingY = y + 84f;
        if (Campaign.AnyoneLost)
        {
            var miss = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            miss.normal.textColor = new Color(1f, 0.4f, 0.35f);

            GUI.Label(new Rect(0f, missingY, Screen.width, 24f), "STILL DOWN THERE", miss);

            miss.fontSize = 15;
            miss.normal.textColor = new Color(1f, 0.75f, 0.7f);
            int row = 0;
            foreach (var m in Campaign.LostCrew)
            {
                GUI.Label(new Rect(0f, missingY + 26f + row * 20f, Screen.width, 20f),
                          $"{m.name}  -  floor {m.floor:00}, run {m.runLost}", miss);
                row++;
            }
            missingY += 30f + row * 20f;
        }

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

        status.normal.textColor = Campaign.CableIsUseless
            ? new Color(1f, 0.35f, 0.3f)
            : (Campaign.LiveRoomsInReach <= 1
                ? new Color(1f, 0.7f, 0.3f)
                : new Color(1f, 1f, 1f, 0.65f));

        GUI.Label(new Rect(0f, y + 34f, Screen.width, 22f),
            $"cable {Campaign.CableLength:0}m reaches floor {Campaign.DeepestReachableFloor}" +
            $"      capacity {Campaign.Capacity:0}kg" +
            $"      sealed rooms: {Campaign.DestroyedRooms.Count}" +
            $"      live rooms in reach: {Campaign.LiveRoomsInReach}", status);

        if (Campaign.CableIsUseless)
        {
            status.normal.textColor = new Color(1f, 0.35f, 0.3f);
            GUI.Label(new Rect(0f, y + 56f, Screen.width, 22f),
                "no live rooms left in cable range. buy cable or this is over.",
                status);
        }
        else if (Campaign.AnyoneLost)
        {
            // Sits in the SHOP, not only on the results screen, and that is
            // the point of Step 9: the person you left behind has to be in
            // your eyeline at the moment you are deciding whether to spend
            // their rescue on cable instead. ECONOMY: "the crew spends two
            // rounds deciding, every single time they open the shop, whether
            // the rope matters more than their friend."
            status.normal.textColor = new Color(1f, 0.5f, 0.4f);

            var names = new System.Text.StringBuilder();
            foreach (var m in Campaign.LostCrew)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append($"{m.name} (floor {m.floor:00})");
            }

            GUI.Label(new Rect(0f, y + 56f, Screen.width, 22f),
                $"still down there: {names}", status);
        }

        float by = y + 92f;

        // Three buttons now, so they get a row of their own rather than the
        // two-wide pair this used to be.
        // FOUR ACROSS NOW. The med spray joins cable, capacity and pack -
        // ECONOMY Part 8 prices it at 35, which is the cheapest thing on this
        // row and the only one that buys nothing at all unless somebody goes
        // down. That is the point of it: it is the line item you regret in
        // both directions.
        const float bw = 178f, gap = 8f;
        float bx = cx - (bw * 4f + gap * 3f) * 0.5f;

        // ---- cable ----
        GUI.enabled = Campaign.CableLeftThisRound > 0 &&
                      Campaign.Money >= Campaign.CableChunkCost;
        string cableLabel = Campaign.CableLeftThisRound > 0
            ? $"+{Campaign.CableChunk}m cable  ({Campaign.CableChunkCost})   " +
              $"{Campaign.CableLeftThisRound} left"
            : "cable   none left this round";
        if (GUI.Button(new Rect(bx, by, bw, 40f), cableLabel))
        {
            Campaign.BuyCable();
        }

        // ---- capacity ----
        //
        // ECONOMY Part 4 measures these in PEOPLE rather than kilos, which is
        // why the label says what the upgrade actually BUYS: the second one
        // is "we can save someone without losing money", the third is "we can
        // save HIM". Shown as the resulting capacity, not as "+50kg", for the
        // same reason.
        GUI.enabled = !Campaign.CapacityMaxed &&
                      Campaign.CapacityLeftThisRound > 0 &&
                      Campaign.Money >= Campaign.CapacityUpgradeCost;
        string capLabel =
            Campaign.CapacityMaxed
                ? $"capacity {Campaign.Capacity:0}kg   MAX"
            : Campaign.CapacityLeftThisRound <= 0
                ? $"capacity {Campaign.Capacity:0}kg   done this round"
                : $"capacity → {Campaign.Capacity + Campaign.CapacityStep:0}kg   " +
                  $"({Campaign.CapacityUpgradeCost})";
        if (GUI.Button(new Rect(bx + bw + gap, by, bw, 40f), capLabel))
        {
            Campaign.BuyCapacity();
        }

        // ---- backpack ----
        // Bought for a PERSON. Solo that is you; Phase 7's shop UI is where
        // the leader picks which crewmate gets it, and the label already
        // names them so that change is a picker rather than a rewrite.
        var buyer = PlayerRegistry.Local;
        int buyerSlot = buyer != null ? buyer.Slot : 0;
        var buyerPack = Crew.Of(buyerSlot);

        GUI.enabled = Campaign.Money >= Campaign.BackpackSlotCost &&
                      buyerPack.BackpackSlots < Crew.MaxBackpackSlots;

        string packLabel = buyerPack.BackpackSlots >= Crew.MaxBackpackSlots
            ? $"pack {buyerPack.BackpackSlots}/{Crew.MaxBackpackSlots}   MAX"
            : $"+1 pack slot for {(buyer != null ? buyer.gameObject.name : "you")}" +
              $"   ({Campaign.BackpackSlotCost})";

        if (GUI.Button(new Rect(bx + (bw + gap) * 2f, by, bw, 40f), packLabel))
        {
            Campaign.BuyBackpackSlot(buyerSlot);
        }

        // ---- med spray ----
        //
        // Bought FOR A PERSON, like the pack beside it. This was crew-wide
        // for about an hour and it was the weaker design: a shared counter
        // cannot be lost, follows everyone around, and nobody is responsible
        // for it.
        //
        // On a person it becomes a job. Somebody is the medic, everyone knows
        // who, and if they go down the crew's rescues go with them - so the
        // one carrying the sprays has a reason to play safe, and that reason
        // is not their own life, it is everybody else's.
        // Greyed out when the pack is full of sprays: one takes a slot, so
        // you can never carry more than you could have carried crates.
        GUI.enabled = Campaign.Money >= Campaign.MedSprayCost &&
                      buyerPack.MedSprays < buyerPack.BackpackSlots;

        if (GUI.Button(new Rect(bx + (bw + gap) * 3f, by, bw, 40f),
                       $"+1 med spray  ({Campaign.MedSprayCost})" +
                       $"   {(buyer != null ? buyer.gameObject.name : "you")} " +
                       $"has {buyerPack.MedSprays}   " +
                       $"({buyerPack.LootSlots} slot(s) left for loot)"))
        {
            Campaign.BuyMedSpray(buyerSlot);
        }

        GUI.enabled = true;

        float tail = by + 46f;

        body.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
        GUI.Label(new Rect(0f, tail, Screen.width, 22f),
            $"next run quota {Campaign.NextQuota}" +
            $"  —  each 10 min a room seals; leaving seals the charged room too", body);

        if (GUI.Button(new Rect(cx - 110f, tail + 36f, 220f, 40f), "go back down"))
        {
            GoBackDown();
        }
    }

    /// <summary>
    /// Start the next round.
    ///
    /// PHASE 4 STEP 8. One method, because a client's press has to end up
    /// running exactly this - AdvanceRun spends the round's purchase caps and
    /// seals a room, and a version of that which only some machines run is
    /// how two crews end up in different buildings.
    ///
    /// AdvanceRun already refuses to do anything on a client (it is guarded on
    /// MaySpend, Step 3), so a client calling this locally advances nothing
    /// and simply asks. The host's copy does the work, and everybody is loaded
    /// into the result together.
    /// </summary>
    public void GoBackDown()
    {
        Campaign.AdvanceRun();
        ReloadScene();
    }

    // ==================================================================
    // PHASE 4 STEP 8 - EVERYONE GOES DOWN TOGETHER, OR NOBODY DOES.
    //
    // This was SceneManager.LoadScene, which reloads the scene on THIS
    // MACHINE ONLY and takes the NetworkManager with it, because the manager
    // is a scene object like everything else. So pressing "go back down"
    // ended the session: round 2 was you, alone, in a fresh single-player
    // game, wondering where your friend went. Reported exactly that way, and
    // it is the reason a med spray bought at the surface could never be
    // carried into a round where somebody needed it.
    //
    // NGO's own scene manager loads the scene on EVERY connected machine and
    // keeps the session alive across it - that is what
    // NetworkConfig.EnableSceneManagement, set since Step 1, has been for.
    //
    // ONLY THE HOST MAY START IT. Not for authority's sake but for a blunter
    // reason: two machines each loading the scene for everybody produces two
    // transitions, and the second one interrupts the first. A client's press
    // is a request, exactly like buying cable and calling the lift.
    // ==================================================================
    void ReloadScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var net = Unity.Netcode.NetworkManager.Singleton;

        if (net == null || !net.IsListening)
        {
            // Offline. Unchanged, and it has to stay unchanged - every step of
            // this phase has promised the solo game keeps working.
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            return;
        }

        if (net.IsServer)
        {
            net.SceneManager.LoadScene(scene.name,
                                       UnityEngine.SceneManagement.LoadSceneMode.Single);
            return;
        }

        if (CampaignNet.Instance != null) CampaignNet.Instance.NextRoundServerRpc();
    }
}
