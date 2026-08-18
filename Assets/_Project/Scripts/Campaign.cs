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
    public static bool CampaignOver;
    public static string EpitaphReason = "";

    /// <summary>1-based room indices sealed forever (rubble, not deleted geometry).</summary>
    public static readonly HashSet<int> DestroyedRooms = new HashSet<int>();

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
        CampaignOver = false;
        EpitaphReason = "";
        DestroyedRooms.Clear();
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
        if (Money < CableChunkCost) return false;
        Money -= CableChunkCost;
        CableLength += CableChunk;
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
