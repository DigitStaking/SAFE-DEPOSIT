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
}
