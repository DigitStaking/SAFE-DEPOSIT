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

    /// <summary>
    /// Med sprays THIS person is carrying. Server-write for the same reason
    /// as Pack: they are bought out of the shared pot, and spent by the host
    /// when a revive is granted. Both ends of a spray's life are host
    /// decisions, so the owner never writes it.
    /// </summary>
    public readonly NetworkVariable<int> Sprays = new NetworkVariable<int>(0, default, Host);

    /// <summary>Carrying a walkie-talkie. Bought in PAIRS, so this is set on
    /// two people at once. Server-write, like everything bought.</summary>
    public readonly NetworkVariable<bool> Walkie = new NetworkVariable<bool>(false, default, Host);

    /// <summary>
    /// Where this player is LOOKING, in degrees of pitch. Owner-written, like
    /// their health, because only their machine knows it.
    ///
    /// PHASE 4 STEP 11. NetworkTransform replicates the BODY, which yaws but
    /// never pitches - a first-person player looks up by moving the camera,
    /// and the camera does not exist on anybody else's machine. So a teammate
    /// looking at the ceiling had a headlamp pointing flat down the corridor,
    /// and nobody could tell where they were looking.
    ///
    /// One float. The alternative is replicating the whole camera, which is a
    /// transform per player for one number nobody else can see anyway.
    /// </summary>
    public readonly NetworkVariable<float> LookPitch =
        new NetworkVariable<float>(0f, default, NetworkVariableWritePermission.Owner);

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        var motor = GetComponent<PlayerMotor>();
        var eye = motor != null ? motor.Eye : null;
        if (eye == null) return;

        // Signed, so it reads as "up is negative" the way Unity pitch does
        // everywhere else in this project rather than jumping to 350 degrees.
        float pitch = eye.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        // Only when it has actually moved. A float that resends every frame is
        // four players' worth of traffic for a number that is usually still.
        if (Mathf.Abs(pitch - LookPitch.Value) > 0.75f) LookPitch.Value = pitch;
    }

    static readonly CrewMemberNet[] bySlot = new CrewMemberNet[Crew.MaxMembers];

    /// <summary>
    /// The row for a slot, or null when nobody is filling it - offline, or a
    /// slot no player has joined into. Null is the signal that Crew should
    /// fall back to its own static, which is what keeps single player working.
    /// </summary>
    public static CrewMemberNet ForSlot(int slot)
    {
        if (slot < 0 || slot >= bySlot.Length) return null;

        var row = bySlot[slot];
        if (row != null && row.IsSpawned) return row;

        // ---- LOST IT? FIND IT AGAIN. ----
        //
        // The table is a cache and the players are the truth, so a missing
        // entry is a question this can answer for itself rather than a reason
        // to fall back on stale local numbers - which is what it used to do,
        // silently, by reporting full health for somebody bleeding out.
        foreach (var p in PlayerRegistry.All)
        {
            if (p == null) continue;

            var candidate = p.GetComponent<CrewMemberNet>();
            if (candidate == null || !candidate.IsSpawned) continue;
            if (!candidate.NetworkObject.IsPlayerObject) continue;
            if ((int)candidate.OwnerClientId != slot) continue;

            bySlot[slot] = candidate;
            return candidate;
        }

        return null;
    }

    int slot = -1;

    public override void OnNetworkSpawn()
    {
        // Slot IS client id - NetworkPlayer assigns it that way, which is what
        // "Crew slots bind to client ids" means in the spec. It also means the
        // binding needs no message: both machines can work it out.
        slot = (int)OwnerClientId;
        if (slot < 0 || slot >= bySlot.Length) return;

        // ==============================================================
        // ONLY A REAL PLAYER MAY CLAIM A CREW ROW.
        //
        // The scene contains a placeholder Player so the game runs offline,
        // and a server AUTO-SPAWNS in-scene NetworkObjects - so that
        // placeholder spawned, ran this method, and registered itself as the
        // crew row for OwnerClientId 0. Which is the HOST.
        //
        // NetworkBootstrap then removed it, correctly, and OnNetworkDespawn
        // cleared the entry it had taken - leaving slot 0 pointing at nothing
        // while the host's real row sat there unregistered. Every machine then
        // fell back to its own local statics for the host, and reported full
        // health for a man bleeding out on the floor.
        //
        // Slot 0 ONLY, because a scene object is owned by the server and the
        // server is client 0. That asymmetry is what named it: the host read
        // the client's 50 correctly and the client read the host's 0 as 100.
        //
        // IsPlayerObject again - third time this exact test has been the
        // difference between a real player and the placeholder, and the third
        // time everything else about them was identical.
        // ==============================================================
        if (!NetworkObject.IsPlayerObject)
        {
            Debug.Log("[Crew] a non-player body spawned and was NOT given a " +
                      "crew row - that is the offline placeholder.");
            return;
        }

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

        if (IsServer)
        {
            Pack.Value = Crew.LocalRow(slot).RawPack;
            Sprays.Value = Crew.LocalRow(slot).RawSprays;
            Walkie.Value = Crew.LocalRow(slot).RawWalkie;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (slot >= 0 && slot < bySlot.Length && bySlot[slot] == this)
            bySlot[slot] = null;
    }
}
