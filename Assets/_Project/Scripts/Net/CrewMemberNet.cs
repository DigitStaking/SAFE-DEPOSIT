// CrewMemberNet.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/CrewMemberNet.cs
// Goes on: the Player prefab.
//
// ====================================================================
// PHASE 4 STEP 4 - PER-PERSON STATE.
//
// Crew holds one row per player: health, bleed-out, lost, pack size. Statics,
// so one copy per process - which means four players had four private
// opinions about everybody's health, and each machine only ever updated its
// own. You would watch a teammate take a fall and their HP bar, on your
// screen, would not move.
//
// WHY THIS LIVES ON THE PLAYER AND NOT ON CAMPAIGN
//
// The money went host-owned in Step 3 because there is ONE pot and two
// machines writing it have no answer. Health is the opposite shape: there are
// four of them and each has an obvious author. Your machine already decides
// where your body is, whether it is grounded, and how far it just fell -
// asking the host to also decide what that fall cost would mean a round trip
// before your own screen turns red.
//
// So each row rides on its owner's player object, and the owner writes it.
// You decide what happens to you; everyone else is told. That is the same
// rule as NetworkTransform in Step 2 and OwnerNetworkAnimator in Step 6, and
// it is the third system in this phase to land on it.
//
// EXCEPT THE PACK, WHICH IS BOUGHT, NOT SUFFERED
//
// BackpackSlots is the one field the owner does NOT write. It is bought by
// the leader out of the shared pot - Campaign.BuyBackpackSlotAuthoritative
// runs on the host and increments somebody ELSE's row. So that single
// variable is server-write while the rest are owner-write.
//
// It reads like an inconsistency and it is exactly the opposite: the
// authority follows the money. Damage happens to you, so you report it. A
// pack is bought for you, so the machine holding the wallet reports that.
// ====================================================================

using Unity.Netcode;
using UnityEngine;

public class CrewMemberNet : NetworkBehaviour
{
    static NetworkVariableWritePermission Mine => NetworkVariableWritePermission.Owner;
    static NetworkVariableWritePermission Host => NetworkVariableWritePermission.Server;

    public readonly NetworkVariable<int> Health =
        new NetworkVariable<int>(Crew.MaxHealth, default, Mine);

    public readonly NetworkVariable<float> BleedOut =
        new NetworkVariable<float>(0f, default, Mine);

    public readonly NetworkVariable<bool> Lost =
        new NetworkVariable<bool>(false, default, Mine);

    public readonly NetworkVariable<int> Pack =
        new NetworkVariable<int>(Crew.StartingBackpackSlots, default, Host);

    static readonly CrewMemberNet[] bySlot = new CrewMemberNet[Crew.MaxMembers];

    /// <summary>
    /// The row for a slot, or null when nobody is filling it - offline, or a
    /// slot no player has joined into. Null is the signal that Crew should
    /// fall back to its own static, which is what keeps single player working.
    /// </summary>
    public static CrewMemberNet ForSlot(int slot) =>
        slot >= 0 && slot < bySlot.Length && bySlot[slot] != null &&
        bySlot[slot].IsSpawned ? bySlot[slot] : null;

    int slot = -1;

    public override void OnNetworkSpawn()
    {
        // Slot IS client id - NetworkPlayer assigns it that way, which is what
        // "Crew slots bind to client ids" means in the spec. It also means the
        // binding needs no message: both machines can work it out.
        slot = (int)OwnerClientId;
        if (slot < 0 || slot >= bySlot.Length) return;

        bySlot[slot] = this;

        // CARRY MY OFFLINE ROW IN WITH ME, the same way hosting carries the
        // campaign in. Somebody who was on 60 HP when they pressed HOST should
        // still be on 60 HP a moment later.
        //
        // Owner only - a client writing this would be writing somebody else's
        // health with its own numbers.
        if (IsOwner)
        {
            var mine = Crew.LocalRow(slot);
            Health.Value = mine.RawHealth;
            BleedOut.Value = mine.RawBleedOut;
            Lost.Value = mine.RawLost;
        }

        if (IsServer) Pack.Value = Crew.LocalRow(slot).RawPack;
    }

    public override void OnNetworkDespawn()
    {
        if (slot >= 0 && slot < bySlot.Length && bySlot[slot] == this)
            bySlot[slot] = null;
    }
}
