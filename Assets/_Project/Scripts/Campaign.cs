// Campaign.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Campaign.cs
// Static campaign state that survives scene reloads.

using System.Collections.Generic;

public static class Campaign
{
    public const float FloorHeight = 4f;
    public const int TotalFloors = 5;

    public const int StartingMoney = 0;
    public const float StartingRope = 12f;
    public const int BaseQuota = 800;
    public const int QuotaStep = 600;

    public const int RopeCostPerMetre = 45;
    public const int RopeChunk = 4;
    public const int BackpackSlotCost = 900;

    /// <summary>Rooms permanently sealed after you surface. Ratchet.</summary>
    public const int RoomsLostOnSurface = 2;

    public static int Money = StartingMoney;
    public static float RopeLength = StartingRope;
    public static int RunNumber = 1;
    public static int BackpackSlots = 2;
    public static bool CampaignOver;
    public static string EpitaphReason = "";

    /// <summary>1-based room indices sealed forever (rubble, not deleted geometry).</summary>
    public static readonly HashSet<int> DestroyedRooms = new HashSet<int>();

    public static int Quota => BaseQuota + (RunNumber - 1) * QuotaStep;

    public static int DeepestReachableFloor =>
        UnityEngine.Mathf.FloorToInt(RopeLength / FloorHeight);

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

    public static bool RopeIsUseless => LiveRoomsInReach <= 0;

    public static int RopeChunkCost => RopeCostPerMetre * RopeChunk;

    public static void Reset()
    {
        Money = StartingMoney;
        RopeLength = StartingRope;
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
    /// Between runs: raise the mafia number. Room seals happen mid-run on
    /// each 10-minute charge AND once more for the threatened room when you
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
        int deep = UnityEngine.Mathf.Max(DeepestReachableFloor, TotalFloors);
        for (int i = 1; i <= TotalFloors && i <= deep + 2; i++)
            if (!DestroyedRooms.Contains(i)) candidates.Add(i);

        // Prefer rooms the rope can still touch; if none, any remaining.
        var prefer = candidates.FindAll(i => i <= DeepestReachableFloor);
        if (prefer.Count == 0) prefer = candidates;

        for (int n = 0; n < count && prefer.Count > 0; n++)
        {
            int pick = UnityEngine.Random.Range(0, prefer.Count);
            DestroyedRooms.Add(prefer[pick]);
            prefer.RemoveAt(pick);
        }
    }

    public static bool BuyRope()
    {
        if (Money < RopeChunkCost) return false;
        Money -= RopeChunkCost;
        RopeLength += RopeChunk;
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
