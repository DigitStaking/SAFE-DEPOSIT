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

    public const int MaxHealth = 100;

    /// <summary>
    /// Current HP, 0-100. Phase 3 makes this per-player along with the rest
    /// of Campaign; until then there is one crew member and this is theirs.
    /// </summary>
    public static int Health = MaxHealth;

    /// <summary>
    /// Seconds left on the bleed-out, or 0 when not downed. Here rather than
    /// on DownedPlayer for the same reason as Health: a scene reload would
    /// otherwise hand a dying player a fresh ninety seconds because the
    /// component holding the number was new.
    /// </summary>
    public static float BleedOutLeft;

    /// <summary>
    /// The bleed-out completed. NOT death - ECONOMY Part 7 is clear that
    /// "Lost is not death; dying is failing to pay for the rescue". Step 8
    /// turns this into a roster and Step 9 into a price.
    /// </summary>
    public static bool PlayerLost;

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

    public static int Money = StartingMoney;
    public static float CableLength = StartingCable;
    public static int RunNumber = 1;
    public static int BackpackSlots = 2;
    public static int CapacityUpgrades;

    /// <summary>Reset by AdvanceRun. See the caps above.</summary>
    public static int CableBoughtThisRound;
    public static int CapacityBoughtThisRound;
    public static bool CampaignOver;
    public static string EpitaphReason = "";

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
    public static bool LootSeeded;

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
        Money = StartingMoney;
        CableLength = StartingCable;
        RunNumber = 1;
        BackpackSlots = 2;
        CapacityUpgrades = 0;
        CableBoughtThisRound = 0;
        CapacityBoughtThisRound = 0;
        Health = MaxHealth;
        BleedOutLeft = 0f;
        PlayerLost = false;
        LostCrew.Clear();
        CampaignOver = false;
        EpitaphReason = "";
        DestroyedRooms.Clear();
        LootRoster.Clear();
        LootSeeded = false;
    }

    public static bool Settle(int recovered)
    {
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
        RunNumber++;

        // The caps are PER ROUND, so this is where they refill. Deliberately
        // in AdvanceRun rather than at the shop's first draw: the shop is
        // drawn every frame it is open, and resetting there would refill the
        // allowance while the player was still standing in it.
        CableBoughtThisRound = 0;
        CapacityBoughtThisRound = 0;
    }

    public static void SealRoom(int room1Based)
    {
        if (room1Based >= 1) DestroyedRooms.Add(room1Based);
    }

    public static void SealRandomRooms(int count)
    {
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
    }

    public static bool BuyCable()
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
        Money -= CapacityUpgradeCost;
        CapacityUpgrades++;
        CapacityBoughtThisRound++;
        return true;
    }

    public static bool BuyBackpackSlot()
    {
        if (Money < BackpackSlotCost || BackpackSlots >= 6) return false;
        Money -= BackpackSlotCost;
        BackpackSlots++;
        return true;
    }
}
