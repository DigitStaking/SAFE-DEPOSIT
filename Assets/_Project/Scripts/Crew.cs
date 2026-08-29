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

    // ================================================================
    // PHASE 4 STEP 4 - ONE ROW PER PERSON, AND EACH PERSON OWNS THEIRS.
    //
    // These were plain fields on a static list, so four players had four
    // private opinions about everybody's health and each machine only ever
    // updated its own. You could watch a teammate take a fall and their HP,
    // on your screen, would not move.
    //
    // Now each field asks that slot's CrewMemberNet - which rides on that
    // player's own object, and which that player writes. The names do not
    // change, so PlayerHealth, DownedPlayer, the HUD and the shop all go on
    // reading exactly what they read before. Same trick as Campaign in Step 3
    // and Elevator in Step 5; third time it has cost nothing but the class it
    // was applied to.
    //
    // Offline there is no CrewMemberNet, the private fields answer, and single
    // player is untouched.
    // ================================================================

    public class Member
    {
        public Member(int slot) { this.slot = slot; }

        readonly int slot;

        CrewMemberNet Net => CrewMemberNet.ForSlot(slot);

        /// <summary>True when this row is mine to write. Damage that happens
        /// to somebody else is reported BY them, never guessed at here.</summary>
        bool Mine => Net == null || Net.IsOwner;

        int localHealth = MaxHealth;
        float localBleed;
        bool localLost;
        int localPack = StartingBackpackSlots;
        int localSprays;

        // Raw access for CrewMemberNet, which has to seed the network from
        // whatever this machine was holding when it connected.
        internal int RawHealth => localHealth;
        internal float RawBleedOut => localBleed;
        internal bool RawLost => localLost;
        internal int RawPack => localPack;
        internal int RawSprays => localSprays;

        public int Health
        {
            get => Net != null ? Net.Health.Value : localHealth;
            set { if (Net != null) { if (Mine) Net.Health.Value = value; } else localHealth = value; }
        }

        /// <summary>Seconds left on the bleed-out, 0 when not downed.</summary>
        public float BleedOutLeft
        {
            get => Net != null ? Net.BleedOut.Value : localBleed;
            set { if (Net != null) { if (Mine) Net.BleedOut.Value = value; } else localBleed = value; }
        }

        /// <summary>The bleed-out completed. NOT death - ECONOMY Part 7.</summary>
        public bool Lost
        {
            get => Net != null ? Net.Lost.Value : localLost;
            set { if (Net != null) { if (Mine) Net.Lost.Value = value; } else localLost = value; }
        }

        /// <summary>
        /// Bought FOR this person by the leader, not for the crew. Decided
        /// 21 Aug 2026: it makes pack capacity a role, so somebody is the
        /// mule, everyone knows who, and losing them loses the pack.
        ///
        /// THE ONE FIELD ITS OWNER DOES NOT WRITE. It is bought out of the
        /// shared pot by whoever is at the shop, so the host writes it - the
        /// authority follows the money. Damage happens to you, so you report
        /// it; a pack is bought for you, so the wallet reports that.
        /// </summary>
        public int BackpackSlots
        {
            get => Net != null ? Net.Pack.Value : localPack;
            set
            {
                if (Net == null) { localPack = value; return; }
                if (Net.IsServer) Net.Pack.Value = value;
            }
        }

        /// <summary>
        /// Med sprays THIS person is carrying.
        ///
        /// PER PERSON, NOT PER CREW - changed 26 Aug 2026, and it is the
        /// better call. A crew-wide kit is a number that follows everyone
        /// around and cannot be lost; sprays on a PERSON make somebody the
        /// medic, and make losing them cost the crew its rescues.
        ///
        /// The one carrying them has a reason to play safe, and that reason
        /// is not their own life - it is everybody else's. That is a far more
        /// interesting thing to argue about in a lift than a shared counter.
        ///
        /// Host-written, like BackpackSlots beside it: bought out of the
        /// shared pot, spent by the host when it grants a revive. Both ends
        /// of a spray's life are host decisions.
        /// </summary>
        public int MedSprays
        {
            get => Net != null ? Net.Sprays.Value : localSprays;
            set
            {
                if (Net == null) { localSprays = Mathf.Max(0, value); return; }
                if (Net.IsServer) Net.Sprays.Value = Mathf.Max(0, value);
            }
        }

        public bool IsDowned => Health <= 0;
    }

    /// <summary>
    /// The row as this machine holds it, ignoring the network entirely.
    /// CrewMemberNet uses it to seed itself on spawn, so joining does not wipe
    /// what you walked in with.
    /// </summary>
    internal static Member LocalRow(int slot) => Of(slot);

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

        while (members.Count <= slot) members.Add(new Member(members.Count));
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
