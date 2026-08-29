// LootItem.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LootItem.cs
// Goes on: every item LootSpawner creates. Added in code, never by hand.
//
// ====================================================================
// THE TAG THAT MAKES LOOT PERSISTENT.
//
// A Carryable knows what it is WORTH. It does not know what it was MADE
// FROM, and rebuilding an item after a scene reload needs both: which tier
// prefab to instantiate, and how heavy it was rolled.
//
// Reading that back off the GameObject's name would work today and break
// the first time a tier is renamed, so it is a real field. Three numbers on
// a component is the cheapest thing that cannot drift.
// ====================================================================

using UnityEngine;

public class LootItem : MonoBehaviour
{
    /// <summary>Index into LootSpawner.tiers.</summary>
    public int tier;

    /// <summary>Rolled once, at first spawn, and kept for the whole campaign
    /// so a crate does not change price between rounds.</summary>
    public int value;

    public float mass;

    // ==================================================================
    // PHASE 4 STEP 6 - THE NAME EVERY MACHINE AGREES ON.
    //
    // Once the roster is shared, every machine builds the same items in the
    // same order, so an item's PLACE IN THE ROSTER is a name all of them
    // already know without being told. Item 17 is the same crate on four
    // machines because item 17 was built from roster entry 17 everywhere.
    //
    // That is what makes "I picked up 17" a sentence worth sending. Without
    // it, a pickup would have to describe a crate by position and hope
    // everyone rounded the same way.
    //
    // No NetworkObject on the crate, and none needed: sixty crates that
    // spend the whole game lying still do not each want a replicated
    // transform. Only the ones somebody touches ever generate traffic.
    // ==================================================================

    public int RosterIndex { get; private set; } = -1;

    static readonly System.Collections.Generic.Dictionary<int, LootItem> byIndex =
        new System.Collections.Generic.Dictionary<int, LootItem>();

    public void SetRosterIndex(int i)
    {
        RosterIndex = i;
        byIndex[i] = this;
    }

    public static LootItem ByIndex(int i) =>
        byIndex.TryGetValue(i, out var it) && it != null ? it : null;

    void OnDestroy()
    {
        if (RosterIndex >= 0 && byIndex.TryGetValue(RosterIndex, out var it) && it == this)
            byIndex.Remove(RosterIndex);
    }
}
