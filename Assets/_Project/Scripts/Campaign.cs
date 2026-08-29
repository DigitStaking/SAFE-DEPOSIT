// Campaign.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Campaign.cs
// Static campaign state that survives scene reloads.
//
// ====================================================================
// ELEVATOR_SPEC STEP 12 - THE ECONOMY RETUNE.
//
// Every number below now comes from ECONOMY_AND_CAMPAIGN.md rather than
// from the pre-elevator prototype. The consistency table in DEMO_PLAN.md
// listed eleven constants that contradicted the design; this is where they
// stop contradicting it.
//
// ====================================================================
// THE ONE THAT WAS NOT A CONSTANT: THE QUOTA CURVE
//
// The mafia's demand was BaseQuota + (RunNumber-1) * QuotaStep - LINEAR,
// 800 then 1400 then 2000. The design is EXPONENTIAL: 200 x 1.072^(R-1).
//
// That is not a tuning change, it is the difficulty curve. ECONOMY Part 2
// explains why the shape matters: income grows at 7% a round and the mafia
// at 7.2%, and "the mafia grows 0.2 points faster than everything else.
// That is the entire difficulty curve. It's invisible per round and
// inescapable over fifty." A linear demand cannot express that at all - it
// is either trivial early or impossible late, and it was both.
//
// ====================================================================
// RENAMED: ROPE -> CABLE
//
// ELEVATOR_SPEC's own instruction, deferred to this step on purpose:
// "Rename them to Cable* in Step 12, with the rest of the constants - not
// before, or every step in between has to be re-tested for a change that
// alters no behaviour."
// ====================================================================

using System.Collections.Generic;
using UnityEngine;

public static class Campaign
{
    // ---- THE THREE CONSTANTS (ECONOMY_AND_CAMPAIGN.md Part 2) ----
    //
    // Everything else scales from these. Change one and the whole economy
    // retunes, which is the entire point of writing it this way.

    /// <summary>Money a good crew extracts in a full round-1 clear.</summary>
    public const int BaseIncome = 400;

    /// <summary>What the mafia takes in round 1. Non-negotiable.</summary>
    public const int BaseMafia = 200;

    /// <summary>Income growth per round. 7%.</summary>
    public const float IncomeGrowth = 1.07f;

    /// <summary>
    /// Mafia growth per round. 7.2% - deliberately 0.2 points above
    /// IncomeGrowth. That gap IS the difficulty curve.
    /// </summary>
    public const float MafiaGrowth = 1.072f;

    // ---- SHAFT ----

    /// <summary>
    /// Metres of cable per floor. 5, so one cable purchase buys exactly one
    /// floor - the reason CableChunk below is also 5. Must match
    /// GrayboxBuilder.FloorHeight and Elevator.floorHeight.
    /// </summary>
    public const float FloorHeight = 5f;

    /// <summary>20 for the demo, 100 for the full game.</summary>
    public const int TotalFloors = 20;

    // ---- STARTING STATE ----

    public const int StartingMoney = 0;

    /// <summary>15m = 3 floors, the round-1 reach from ECONOMY Part 3.</summary>
    public const float StartingCable = 15f;

    // ---- SHOP ----

    /// <summary>One purchase = one floor deeper.</summary>
    public const int CableChunk = 5;

    /// <summary>
    /// Round-1 price of CableChunk metres. ECONOMY Part 5: "Rope +5m
    /// (+1 room) 80". Scales with IncomeGrowth like every other shop price,
    /// so its cost RELATIVE to a round's income never changes - round 40
    /// feels exactly as tight as round 1.
    /// </summary>
    public const int CableChunkBaseCost = 80;

    public const int BackpackSlotBaseCost = 120;

    /// <summary>
    /// ECONOMY Part 8: 35, "revives a downed player where they lie".
    ///
    /// STOCKED FOR THE CREW, NOT FOR A PERSON - which is the opposite call to
    /// BackpackSlots, and deliberately so. A pack is a ROLE: somebody is the
    /// mule, everyone knows who, and losing them costs the crew their
    /// carrying capacity. A med spray is an INSURANCE POLICY, and the whole
    /// tension of buying one is that the money could have been cable instead.
    /// Making the crew choose once, together, is the interesting version;
    /// making them choose four times, per person, is bookkeeping.
    ///
    /// It also means anybody can carry the kit, so the argument about who
    /// runs back for the downed player is about courage and not inventory.
    /// </summary>
    public const int MedSprayBaseCost = 35;

    // ---- OVERLOAD: TEN SECONDS, THEN IT PARTS ----
    //
    // Replaces the per-metre fray model on 21 Aug 2026, on request. That one
    // let you ride overloaded and billed you slowly in rope; this one refuses
    // to move at all and gives you a countdown to fix it.
    //
    // The difference is where the decision sits. Deferred wear is a thing you
    // notice three trips later, alone, reading a rope. A ten-second alarm with
    // the doors shut is four people looking at a pile of loot and having to
    // say out loud which crate goes back - which is the argument this whole
    // game is built to host, and the elevator is where it belongs.
    //
    // It also restores ELEVATOR_SPEC line 141 - "it will not move while
    // overloaded" - as literally true, instead of something reinterpreted to
    // make room for the trap.

    /// <summary>Seconds of overload before the cable parts.</summary>
    public const float OverloadGrace = 10f;

    /// <summary>0 = fine, 1 = parting. Live strain, not accumulated damage:
    /// it fills while overloaded and empties when the load comes off.</summary>
    static float localStrain;
    public static float CableStrain
    {
        get => Net != null ? Net.Strain.Value : localStrain;
        set { if (Net != null) { if (Net.IsServer) Net.Strain.Value = value; } else localStrain = value; }
    }

    // ---- PER-ROUND PURCHASE CAPS ----
    //
    // You may buy two cable chunks and one capacity upgrade per round, no
    // matter how rich you are.
    //
    // Without a cap, money converts straight into depth: bank a big haul and
    // you can jump five floors in one shop visit, which flattens the curve
    // the whole economy is built on. ECONOMY's 7% income against 7.2% mafia
    // only works as a difficulty curve if depth is bought GRADUALLY - the
    // squeeze is supposed to be felt every round, not skipped by one good
    // run.
    //
    // It also gives a bad round a floor rather than a cliff: falling behind
    // costs you a round of progress, not the campaign, because nobody could
    // have run away with it while you were behind either.

    public const int MaxCablePerRound = 2;
    public const int MaxCapacityPerRound = 1;

    // ---- CAPACITY (ECONOMY_AND_CAMPAIGN.md Part 4) ----
    //
    // "Capacity is not optional and it is not a power fantasy - it is a TAX
    // you pay to keep taking everything. Fall behind on upgrades and you
    // start leaving loot on the floor of a building that's being demolished."

    /// <summary>Total mass the cable lifts: crew + cargo + survivors.</summary>
    public const float BaseCapacity = 550f;

    /// <summary>What one upgrade adds. The doc measures these in PEOPLE, not
    /// kilos: the 2nd upgrade is "we can save someone without losing money",
    /// the 3rd is "we can save HIM".</summary>
    public const float CapacityStep = 50f;

    public const int CapacityBaseCost = 50;
    public const float CapacityCostGrowth = 1.25f;

    /// <summary>Nine across fifty rounds, per ECONOMY Part 4.</summary>
    public const int MaxCapacityUpgrades = 9;

    /// <summary>PLAYER_MASS. One number, read by ElevatorDeck's load sum and
    /// by PlayerMotor's Rigidbody, so the gauge and the physics agree.</summary>
    public const float PlayerMass = 70f;

    // ---- HEALTH (PHASE2_SPEC Part 2) ----
    //
    // "100 HP. No regeneration. Ever."
    //
    // HP LIVES HERE, NOT ON THE PLAYER, and that is the whole decision of
    // Step 2. RunManager.ReloadScene() rebuilds the scene between runs, so a
    // field on a MonoBehaviour is back at 100 every round no matter what
    // happened last round. ECONOMY Part 5 sells a Bandage in the SHOP for 10
    // - and a shop item that heals you is meaningless if surfacing already
    // did. So damage is campaign state, exactly like Money, and the only way
    // back up is something you paid for.
    //
    // Nothing in this file or in PlayerHealth ever adds to this number on a
    // timer. There is no regeneration path to disable later because one was
    // never written.

    // HEALTH, THE BLEED-OUT CLOCK, LOST AND BACKPACK SLOTS MOVED TO Crew.
    //
    // Phase 3 Step 4. They were here because they had to survive
    // RunManager.ReloadScene, which was the right reason and the wrong file:
    // everything else in Campaign is genuinely shared - ECONOMY Part 6, "All
    // loot goes into one pot" - while those four describe ONE PERSON. With
    // two bodies both would have been reading the same hundred hit points.
    //
    // Crew.cs is static for exactly the same reload reason, so nothing about
    // the original argument was lost; it just found the right table.

    // ---- THE LOST (PHASE2_SPEC Step 8) ----
    //
    // Bleeding out does not kill you and it does not end the campaign. It
    // takes you OUT OF THE BUILDING'S REACH - you are still down there, and
    // the shop will sell you a way to go and get yourself back.
    //
    // ECONOMY Part 5 prices it Rescue(R, f) = Mafia(R) x (1 + f/10), which is
    // why the FLOOR is recorded and not just the fact: a shallow loss is
    // recoverable and a deep one "is a crisis that takes both of your two
    // runs and every purchase in between". Step 9 charges it. Step 8 is only
    // responsible for knowing who and where.
    //
    // `paid` exists now and is untouched until Step 9. ECONOMY is explicit
    // that "partial payment carries over", which means the debt has to be a
    // running total from the moment it is created rather than a price
    // computed fresh at the till - a crew that puts 200 toward a 372 rescue
    // must not lose it by pressing the wrong button.

    public class LostCrewMember
    {
        public string name;
        public int floor;
        public int runLost;
        public int paid;
    }

    public static readonly List<LostCrewMember> LostCrew = new List<LostCrewMember>();

    public static bool AnyoneLost => LostCrew.Count > 0;

    public static void RecordLost(string who, int floor)
    {
        if (string.IsNullOrEmpty(who)) who = "a crewmate";

        // Nobody is lost twice. Being down in the building is a state, not an
        // event you can accumulate.
        foreach (var m in LostCrew)
            if (m.name == who) return;

        LostCrew.Add(new LostCrewMember {
            name = who, floor = floor, runLost = RunNumber, paid = 0,
        });
    }

    /// <summary>
    /// Rooms sealed when you surface: exactly one, the room whose charge was
    /// counting down as you left. The OTHERS are sealed mid-run, one per
    /// completed 10-minute charge, which together produce the design's
    /// floor(runMinutes / 10) + 1. See RunManager.OnExtractSeal.
    /// </summary>
    public const int RoomsLostOnSurface = 1;

    // ---- LIVE STATE ----

    // ================================================================
    // PHASE 4 STEP 3 - THESE ARE NO LONGER STORAGE. THEY ARE QUESTIONS.
    //
    // They were plain statics, and a static is one copy PER PROCESS. Two
    // windows are two processes, so "the bank" was two numbers that had never
    // met: the host bought cable, the host's number moved, and the client's
    // shop went on showing the old one. Nothing threw. The two games simply
    // disagreed, quietly, forever.
    //
    // Now each one asks CampaignNet when a session exists and falls back to a
    // private field when it does not. The NAMES ARE UNCHANGED, which is the
    // entire point: 97 places read Campaign.Money and not one of them had to
    // be edited to make the money shared.
    //
    // Offline there is no CampaignNet, the fallback field answers, and the
    // game behaves exactly as it did before this block was written.
    // ================================================================

    static CampaignNet Net => CampaignNet.Instance;

    static int    localMoney = StartingMoney;
    static float  localCable = StartingCable;
    static int    localRun = 1;
    static int    localCapacity;
    static int    localCableBought;
    static int    localCapacityBought;
    static bool   localOver;
    static string localEpitaph = "";

    // Assigning to these on a CLIENT throws, by design. NGO refuses a
    // server-write variable written from a client, and a stack trace with a
    // line number is a far better failure than two crews arguing about the
    // bank balance. Clients ask via the Buy* methods below.
    public static int Money
    {
        get => Net != null ? Net.Money.Value : localMoney;
        set { if (Net != null) Net.Money.Value = value; else localMoney = value; }
    }

    public static float CableLength
    {
        get => Net != null ? Net.Cable.Value : localCable;
        set { if (Net != null) Net.Cable.Value = value; else localCable = value; }
    }

    public static int RunNumber
    {
        get => Net != null ? Net.RunNumber.Value : localRun;
        set { if (Net != null) Net.RunNumber.Value = value; else localRun = value; }
    }

    public static int CapacityUpgrades
    {
        get => Net != null ? Net.Capacity.Value : localCapacity;
        set { if (Net != null) Net.Capacity.Value = value; else localCapacity = value; }
    }

    /// <summary>Reset by AdvanceRun. See the caps above.</summary>
    public static int CableBoughtThisRound
    {
        get => Net != null ? Net.CableBought.Value : localCableBought;
        set { if (Net != null) Net.CableBought.Value = value; else localCableBought = value; }
    }

    public static int CapacityBoughtThisRound
    {
        get => Net != null ? Net.CapacityBought.Value : localCapacityBought;
        set { if (Net != null) Net.CapacityBought.Value = value; else localCapacityBought = value; }
    }

    public static bool CampaignOver
    {
        get => Net != null ? Net.Over.Value : localOver;
        set { if (Net != null) Net.Over.Value = value; else localOver = value; }
    }

    public static string EpitaphReason
    {
        get => Net != null ? Net.Epitaph.Value.ToString() : localEpitaph;
        set
        {
            // FixedString128Bytes, not string. A NetworkVariable has to have a
            // size known before it is sent, and "a string" does not. 128 bytes
            // is longer than every epitaph this game writes; the longest is
            // "somebody was still inside when a room sealed" at 43.
            if (Net != null) Net.Epitaph.Value = new Unity.Collections.FixedString128Bytes(
                                                    value ?? "");
            else localEpitaph = value ?? "";
        }
    }

    /// <summary>
    /// Hosting must not wipe the campaign. Whoever hosts has been playing
    /// offline and their statics hold the real save - money earned, cable
    /// bought, runs survived - so those numbers seed the session.
    ///
    /// Clients do the opposite: they take what arrives. Their own statics are
    /// their own save from their own sessions, and adopting the host's is the
    /// whole meaning of joining somebody's game.
    /// </summary>
    public static void PushLocalStateToNetwork()
    {
        if (Net == null) return;
        Net.Money.Value = localMoney;
        Net.Cable.Value = localCable;
        Net.RunNumber.Value = localRun;
        Net.Capacity.Value = localCapacity;
        Net.CableBought.Value = localCableBought;
        Net.CapacityBought.Value = localCapacityBought;
        Net.Over.Value = localOver;
        Net.Strain.Value = localStrain;
        Net.Seeded.Value = localSeeded;
        Net.Sprays.Value = localSprays;
        Net.Sealed.Value = SealedMask();
        Net.Epitaph.Value = new Unity.Collections.FixedString128Bytes(localEpitaph ?? "");
    }

    /// <summary>
    /// Leaving a session keeps what the run earned rather than snapping back
    /// to whatever this machine had before it joined.
    /// </summary>
    public static void PullNetworkStateToLocal()
    {
        if (Net == null) return;
        localMoney = Net.Money.Value;
        localCable = Net.Cable.Value;
        localRun = Net.RunNumber.Value;
        localCapacity = Net.Capacity.Value;
        localCableBought = Net.CableBought.Value;
        localCapacityBought = Net.CapacityBought.Value;
        localOver = Net.Over.Value;
        localStrain = Net.Strain.Value;
        localSeeded = Net.Seeded.Value;
        localSprays = Net.Sprays.Value;
        ApplySealedMask(Net.Sealed.Value);
        localEpitaph = Net.Epitaph.Value.ToString();
    }

    /// <summary>1-based room indices sealed forever (rubble, not deleted geometry).</summary>
    public static readonly HashSet<int> DestroyedRooms = new HashSet<int>();

    // ---- THE BUILDING REMEMBERS ----
    //
    // Loot is generated ONCE, at the start of a campaign, and after that the
    // building keeps whatever state the crew left it in. Take three crates
    // off floor 4 and floor 4 has three fewer crates for the rest of the
    // campaign. Shove a pallet into a corner and it is still in that corner
    // next round.
    //
    // It has to live here because RunManager.ReloadScene() destroys every
    // runtime object between rounds - the same reason Health and Money are
    // here. LootSpawner writes this roster when you extract and rebuilds
    // from it on the next load instead of rolling fresh loot.
    //
    // This is what turns the demolition from a timer into a LOSS. A floor
    // that respawns is a floor you never really lost, and the whole economy
    // assumes the opposite: "you start leaving loot on the floor of a
    // building that's being demolished."

    public class LootRecord
    {
        public int tier;
        public int value;
        public float mass;
        public string name;
        public Vector3 position;
        public Quaternion rotation;
    }

    public static readonly List<LootRecord> LootRoster = new List<LootRecord>();

    /// <summary>
    /// Distinguishes "the building has been stocked and then stripped bare"
    /// from "the building has never been stocked". Without it, an empty
    /// roster would look like a fresh campaign and refill the whole tower.
    /// </summary>
    static int localSprays;

    /// <summary>
    /// How many med sprays the crew is carrying. Host-owned, like the money,
    /// because it is spent out of the same shared decision.
    /// </summary>
    public static int MedSprays
    {
        get => Net != null ? Net.Sprays.Value : localSprays;
        set { if (Net != null) { if (Net.IsServer) Net.Sprays.Value = value; } else localSprays = value; }
    }

    public static bool BuyMedSpray()
    {
        if (Money < MedSprayCost) return false;
        if (MaySpend) return BuyMedSprayAuthoritative();

        Net.BuyMedSprayServerRpc();
        return true;
    }

    public static bool BuyMedSprayAuthoritative()
    {
        if (Money < MedSprayCost) return false;
        Money -= MedSprayCost;
        MedSprays++;
        return true;
    }

    /// <summary>
    /// Spend one. Host only - a client that could decrement this could revive
    /// the whole crew off a kit that ran out three floors ago.
    /// </summary>
    public static bool ConsumeMedSpray()
    {
        if (!MaySpend || MedSprays <= 0) return false;
        MedSprays--;
        return true;
    }

    static bool localSeeded;
    public static bool LootSeeded
    {
        get => Net != null ? Net.Seeded.Value : localSeeded;
        set { if (Net != null) { if (Net.IsServer) Net.Seeded.Value = value; } else localSeeded = value; }
    }

    // ---- THE CURVES ----

    /// <summary>
    /// What the mafia demands this round. EXPONENTIAL, not linear - see the
    /// note at the top of this file for why that distinction is the whole
    /// difficulty design rather than a tuning preference.
    ///
    /// The +-10% randomiser from ECONOMY Part 9 is NOT applied here. It has
    /// to be rolled once per round and shown before the run rather than
    /// recomputed on every read, so it needs somewhere to live - that is
    /// Block 7's "mafia demand + results screen" work, not this step's.
    /// </summary>
    public static int Quota =>
        Mathf.RoundToInt(BaseMafia * Mathf.Pow(MafiaGrowth, RunNumber - 1));

    /// <summary>
    /// What the mafia will want NEXT round. ECONOMY Part 10 is explicit that
    /// this belongs on the results screen: "Show the next demand on the
    /// results screen of the previous round, so it's always a plan and never
    /// an ambush." A demand you cannot see coming is not difficulty.
    /// </summary>
    public static int NextQuota =>
        Mathf.RoundToInt(BaseMafia * Mathf.Pow(MafiaGrowth, RunNumber));

    /// <summary>Money available on the floor this round, before the cut.</summary>
    public static int Income =>
        Mathf.RoundToInt(BaseIncome * Mathf.Pow(IncomeGrowth, RunNumber - 1));

    /// <summary>
    /// Every shop price scales together, so relative cost stays constant
    /// across fifty rounds. ECONOMY Part 2: "if shop prices stayed flat, a
    /// walkie-talkie would be a real decision in round 1 and pocket change
    /// by round 20."
    /// </summary>
    public static int ScaledPrice(int basePrice) =>
        Mathf.RoundToInt(basePrice * Mathf.Pow(IncomeGrowth, RunNumber - 1));

    public static int CableChunkCost => ScaledPrice(CableChunkBaseCost);
    public static int BackpackSlotCost => ScaledPrice(BackpackSlotBaseCost);
    public static int MedSprayCost => ScaledPrice(MedSprayBaseCost);

    /// <summary>What the cable lifts right now, upgrades included.</summary>
    public static float Capacity => BaseCapacity + CapacityStep * CapacityUpgrades;

    /// <summary>
    /// 50 x 1.25^n, where n is how many you already own.
    ///
    /// NOT run through ScaledPrice, unlike every other shop item, and that is
    /// deliberate rather than an oversight: ECONOMY Part 4 states the total
    /// outright - "Nine upgrades across fifty rounds cost 1,290 total" - and
    /// 1,290 is exactly the sum of 50 x 1.25^n for n = 0..8 with no round
    /// scaling applied. Multiplying by g as well would roughly triple it and
    /// contradict the doc's own arithmetic.
    ///
    /// The doc IS ambiguous here - its shop table labels these "Cost (R1)",
    /// which would imply scaling - so this is worth revisiting in Phase 7
    /// when the full shop is built. Until then the stated total wins over the
    /// implied one, because it is the number the design was reasoned against.
    /// </summary>
    public static int CapacityUpgradeCost =>
        Mathf.RoundToInt(CapacityBaseCost * Mathf.Pow(CapacityCostGrowth, CapacityUpgrades));

    public static bool CapacityMaxed => CapacityUpgrades >= MaxCapacityUpgrades;

    public static int CableLeftThisRound =>
        Mathf.Max(0, MaxCablePerRound - CableBoughtThisRound);

    public static int CapacityLeftThisRound =>
        Mathf.Max(0, MaxCapacityPerRound - CapacityBoughtThisRound);

    public static int DeepestReachableFloor =>
        Mathf.FloorToInt(CableLength / FloorHeight);

    public static int LiveRoomsInReach
    {
        get
        {
            int n = 0;
            int deep = DeepestReachableFloor;
            for (int i = 1; i <= deep && i <= TotalFloors; i++)
                if (!DestroyedRooms.Contains(i)) n++;
            return n;
        }
    }

    public static bool CableIsUseless => LiveRoomsInReach <= 0;

    public static void Reset()
    {
        // Host only online. Every client runs its own RunManager and would
        // otherwise each try to write the same pot. Step 8 makes the round
        // itself a host-driven event; this guard is what keeps the meantime
        // from throwing.
        if (!MaySpend) return;

        Money = StartingMoney;
        CableLength = StartingCable;
        RunNumber = 1;
        CapacityUpgrades = 0;
        CableBoughtThisRound = 0;
        CapacityBoughtThisRound = 0;
        LostCrew.Clear();
        CableStrain = 0f;

        // The per-player table lives in Crew now, but a new campaign still
        // has to wipe it - four fresh people, not the last crew's injuries.
        Crew.Reset();
        CampaignOver = false;
        EpitaphReason = "";
        DestroyedRooms.Clear();
        LootRoster.Clear();
        LootSeeded = false;
    }

    public static bool Settle(int recovered)
    {
        // Host only online - the same reason Reset and AdvanceRun are. Four
        // clients each settling the same haul would pay the quota four times.
        // Clients read the result, they do not compute it. Step 8 turns the
        // whole round into a host-driven event; this keeps the meantime sane.
        if (!MaySpend) return !CampaignOver;

        int owed = Quota;
        Money += recovered;

        if (Money < owed)
        {
            CampaignOver = true;
            EpitaphReason =
                $"quota {owed}, extracted {recovered}, bankroll after sale {Money} - short";
            return false;
        }

        Money -= owed;
        return true;
    }

    /// <summary>
    /// Between runs: advance the round, which raises the mafia's number on
    /// its own through the Quota curve. Room seals happen mid-run on each
    /// 10-minute charge AND once more for the threatened room when you
    /// surface (see RunManager.OnExtractSeal).
    /// </summary>
    public static void AdvanceRun()
    {
        // Host only online. Every client runs its own RunManager and would
        // otherwise each try to write the same pot. Step 8 makes the round
        // itself a host-driven event; this guard is what keeps the meantime
        // from throwing.
        if (!MaySpend) return;

        RunNumber++;

        // The caps are PER ROUND, so this is where they refill. Deliberately
        // in AdvanceRun rather than at the shop's first draw: the shop is
        // drawn every frame it is open, and resetting there would refill the
        // allowance while the player was still standing in it.
        CableBoughtThisRound = 0;
        CapacityBoughtThisRound = 0;
    }

    /// <summary>
    /// Every sealed room as one number, one bit per floor. Twenty floors, so
    /// it fits in a uint with room to spare.
    /// </summary>
    public static uint SealedMask()
    {
        uint m = 0u;
        foreach (int r in DestroyedRooms)
            if (r >= 1 && r <= 32) m |= 1u << (r - 1);
        return m;
    }

    /// <summary>Adopt the host's demolished building wholesale.</summary>
    public static void ApplySealedMask(uint m)
    {
        DestroyedRooms.Clear();
        for (int r = 1; r <= 32; r++)
            if ((m & (1u << (r - 1))) != 0u) DestroyedRooms.Add(r);
    }

    /// <summary>
    /// Called by the host after any change to DestroyedRooms. The set is the
    /// local truth; this is how everyone else hears about it.
    ///
    /// Left as an explicit call rather than hidden behind the set, because
    /// HashSet cannot tell anybody it changed and wrapping it in a property
    /// would mean touching all ten places that read it - the same trade this
    /// file made in Step 3 and did not regret.
    /// </summary>
    public static void PublishSealedRooms()
    {
        if (Net != null && Net.IsServer) Net.Sealed.Value = SealedMask();
    }

    /// <summary>
    /// HOST ONLY once a session is running. A client that seals a room on its
    /// own is a client building a different building - which is exactly what
    /// was happening: the seal timer runs on every machine, so each one
    /// demolished whatever its own clock reached first, and one player stood
    /// in a doorway another saw as rubble.
    /// </summary>
    public static void SealRoom(int room1Based)
    {
        if (room1Based < 1) return;
        if (!MaySpend) return;

        DestroyedRooms.Add(room1Based);
        PublishSealedRooms();
    }

    public static void SealRandomRooms(int count)
    {
        // HOST ONLY. This rolls dice. Four machines each rolling their own
        // would demolish four different buildings, and there is no way to
        // reconcile that afterwards - which is worse than the timer bug,
        // because at least a timer eventually agrees on WHICH rooms exist.
        if (!MaySpend) return;

        var candidates = new List<int>();
        int deep = Mathf.Max(DeepestReachableFloor, TotalFloors);
        for (int i = 1; i <= TotalFloors && i <= deep + 2; i++)
            if (!DestroyedRooms.Contains(i)) candidates.Add(i);

        // Prefer rooms the cable can still touch; if none, any remaining.
        var prefer = candidates.FindAll(i => i <= DeepestReachableFloor);
        if (prefer.Count == 0) prefer = candidates;

        for (int n = 0; n < count && prefer.Count > 0; n++)
        {
            int pick = Random.Range(0, prefer.Count);
            DestroyedRooms.Add(prefer[pick]);
            prefer.RemoveAt(pick);
        }

        PublishSealedRooms();
    }

    // ================================================================
    // BUYING, WHEN THERE ARE FOUR OF YOU
    //
    // Offline these ran on the spot. Online, ONLY THE HOST MAY SPEND. Not
    // because a co-op crew would cheat each other - there is nobody to cheat -
    // but because two machines that can both take money out of the same pot
    // will eventually both take it out in the same frame, and then there is no
    // answer to "how much is in the bank", only two answers.
    //
    // So a client's press is a REQUEST. It goes to the host, the host runs the
    // very same rules it always ran, and the new number comes back to
    // everybody. The client's shop updates because it is reading a replicated
    // value - not because it changed anything itself.
    //
    // The client still checks first, so the button greys out at the right
    // moment and the press feels immediate. But a check on the asking machine
    // is a COURTESY, NOT A GUARANTEE: by the time the request lands, somebody
    // else may have spent the money. The host checks again because the host is
    // the one that decides. If it says no, nothing changes and the replicated
    // numbers simply never move.
    //
    // The *Authoritative half is the original code, untouched. That is
    // deliberate - the rules of the economy did not change just because there
    // are now four people in the lift, and the day they DO change, they change
    // in one place.
    // ================================================================

    /// <summary>True when this machine is allowed to write the pot: always
    /// offline, host only online.</summary>
    static bool MaySpend => Net == null || Net.IsServer;

    public static bool BuyCable()
    {
        if (CableLeftThisRound <= 0 || Money < CableChunkCost) return false;
        if (MaySpend) return BuyCableAuthoritative();

        Net.BuyCableServerRpc();
        return true;                 // asked. The host answers by moving the numbers.
    }

    public static bool BuyCableAuthoritative()
    {
        if (CableLeftThisRound <= 0) return false;
        if (Money < CableChunkCost) return false;
        Money -= CableChunkCost;
        CableLength += CableChunk;
        CableBoughtThisRound++;
        return true;
    }

    public static bool BuyCapacity()
    {
        if (CapacityLeftThisRound <= 0) return false;
        if (CapacityMaxed || Money < CapacityUpgradeCost) return false;
        if (MaySpend) return BuyCapacityAuthoritative();

        Net.BuyCapacityServerRpc();
        return true;
    }

    public static bool BuyCapacityAuthoritative()
    {
        if (CapacityLeftThisRound <= 0) return false;
        if (CapacityMaxed || Money < CapacityUpgradeCost) return false;
        Money -= CapacityUpgradeCost;
        CapacityUpgrades++;
        CapacityBoughtThisRound++;
        return true;
    }

    /// <summary>
    /// Bought FOR a named crewmate, not for the crew.
    ///
    /// The money is still the shared pot - ECONOMY Part 6 - but the pack
    /// belongs to a person, which is what makes somebody the mule and makes
    /// losing them cost the crew their carrying capacity as well as a friend.
    /// The slot argument is the whole difference between those two things.
    /// </summary>
    public static bool BuyBackpackSlot(int slot)
    {
        var member = Crew.Of(slot);
        if (Money < BackpackSlotCost) return false;
        if (member.BackpackSlots >= Crew.MaxBackpackSlots) return false;
        if (MaySpend) return BuyBackpackSlotAuthoritative(slot);

        Net.BuyBackpackSlotServerRpc(slot);
        return true;
    }

    /// <summary>
    /// The money is shared and the PACK IS NOT. Step 4 replicates the slot
    /// count itself; until then the host's Crew row moves and the buyer's
    /// screen will not show it. That is a known half-step, not a bug to chase.
    /// </summary>
    public static bool BuyBackpackSlotAuthoritative(int slot)
    {
        var member = Crew.Of(slot);
        if (Money < BackpackSlotCost) return false;
        if (member.BackpackSlots >= Crew.MaxBackpackSlots) return false;

        Money -= BackpackSlotCost;
        member.BackpackSlots++;
        return true;
    }
}
