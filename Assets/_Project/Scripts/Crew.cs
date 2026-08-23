// Crew.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Crew.cs
// Goes on: nothing. Static, like Campaign.
//
// ====================================================================
// PHASE 3 STEP 4 - THE FOUR THINGS THAT BELONG TO A PERSON.
//
// PHASE3_SPEC's survey found that almost everything in Campaign is shared BY
// DESIGN - ECONOMY Part 6, "All loot goes into one pot." Money, cable, the
// loot roster, the destroyed rooms, the round counter are crew state and
// always were.
//
// Exactly four fields are not:
//
//     Health          your HP
//     BleedOutLeft    your ninety seconds
//     Lost            whether you are still down there
//     BackpackSlots   your pack, bought FOR you by the leader
//
// This file is where those four live now. Campaign keeps the pot.
//
// ====================================================================
// WHY IT IS STILL STATIC, AND WHY THAT IS NOT A COMPROMISE
//
// The obvious move is to put HP on PlayerHealth as a plain int. It is wrong
// for the same reason it was wrong in Phase 2, and the reason has not changed
// with the number of players: RunManager.ReloadScene destroys every runtime
// object between rounds. A field on a component is back at 100 every round,
// which makes "no regeneration, ever" true inside one run and false across a
// campaign - and turns ECONOMY's Bandage into an item nobody would ever buy.
//
// So per-player state has to outlive the objects it describes. Static is not
// a shortcut here; it is the requirement.
//
// ====================================================================
// KEYED ON A SLOT, WHICH IS THE SEAM PHASE 4 REPLACES
//
// A player needs an identity that survives the scene being destroyed and
// rebuilt. The GameObject will not do it - the new one is a different object,
// and with two prefabs both named "Player" the name does not disambiguate
// either.
//
// So each body gets a SLOT, handed out by PlayerRegistry in registration
// order: 0, 1, 2, 3. Solo is always slot 0, so the same body gets its own HP
// back every round with no bookkeeping at all. With several bodies spawned in
// a deterministic order, the slots are stable for the same reason.
//
// It is deliberately the crudest thing that works, because Phase 4 throws it
// away: a network identity is the real answer, and it arrives with the
// network. What matters now is that every read and write goes through
// Crew.Of(slot), so swapping what "slot" means is a change to one method.
// ====================================================================

using System.Collections.Generic;
using UnityEngine;

public static class Crew
{
    /// <summary>Four in the demo. Slots beyond this are refused rather than
    /// silently growing, so a spawn bug is loud instead of invisible.</summary>
    public const int MaxMembers = 4;

    public const int MaxHealth = 100;
    public const int StartingBackpackSlots = 2;
    public const int MaxBackpackSlots = 6;

    public class Member
    {
        public int Health = MaxHealth;

        /// <summary>Seconds left on the bleed-out, 0 when not downed.</summary>
        public float BleedOutLeft;

        /// <summary>The bleed-out completed. NOT death - ECONOMY Part 7.</summary>
        public bool Lost;

        /// <summary>
        /// Bought FOR this person by the leader, not for the crew. Decided
        /// 21 Aug 2026: it makes pack capacity a role, so somebody is the
        /// mule, everyone knows who, and losing them loses the pack.
        /// </summary>
        public int BackpackSlots = StartingBackpackSlots;

        public bool IsDowned => Health <= 0;
    }

    static readonly List<Member> members = new List<Member>();

    /// <summary>
    /// The state for a slot, created on first use. Never returns null, so no
    /// caller has to guard - a missing member is an empty one, not a crash
    /// halfway through a run.
    /// </summary>
    public static Member Of(int slot)
    {
        if (slot < 0) slot = 0;

        if (slot >= MaxMembers)
        {
            Debug.LogError($"[Crew] Slot {slot} is beyond MaxMembers " +
                           $"({MaxMembers}). Something is spawning more " +
                           "players than the demo supports.");
            slot = MaxMembers - 1;
        }

        while (members.Count <= slot) members.Add(new Member());
        return members[slot];
    }

    /// <summary>The state of the body this component is attached to.</summary>
    public static Member Of(Component c)
    {
        var owner = PlayerRegistry.OwnerOf(c);
        return Of(owner != null ? owner.Slot : 0);
    }

    /// <summary>Everyone who has state, which is everyone who has existed.</summary>
    public static IReadOnlyList<Member> Members => members;

    public static void Reset() => members.Clear();
}
