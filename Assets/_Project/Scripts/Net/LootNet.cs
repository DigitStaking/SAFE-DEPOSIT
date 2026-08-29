// LootNet.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/LootNet.cs
// Goes on: the CAMPAIGN object, beside CampaignNet.
//
// ====================================================================
// PHASE 4 STEP 6 - ONE BUILDING.
//
// WHAT WAS WRONG, AND HOW STRANGE IT ACTUALLY WAS
//
// LootSpawner runs on every machine, and it stocks the building with
// Random.Range and no shared seed. So each player was walking around their
// OWN private copy of the building, full of their own private crates, which
// nobody else could see or pick up. Reported as "each one have 3 items that
// he is the only one can see them".
//
// That is also the real reason the money never mixed. The pot has been shared
// since Step 3 and was working; what was not shared was what went INTO it.
// Settle counts the loot on the deck, and each machine counted its own pile -
// the host banked 235 while the client banked 99, from the same run.
//
// WHY THE ROSTER, AND NOT SIXTY NetworkObjects
//
// The obvious answer is to give every crate a NetworkObject and let the host
// spawn all sixty. That is sixty spawn messages on join, sixty replicated
// transforms for objects that spend the entire game lying perfectly still,
// and sixty things to go wrong.
//
// But Campaign.LootRoster ALREADY describes the building completely - tier,
// value, mass, name, position, rotation, per item - because Phase 2 needed
// the building to survive the scene reload between rounds. And LootSpawner
// ALREADY has RestoreRoster(), which rebuilds the whole floor from it exactly
// as it was.
//
// So the recipe travels and each machine cooks. One list instead of sixty
// spawns, and the code that rebuilds from it has been shipping since Phase 2.
//
// THIRD TIME THIS PHASE. Campaign was already a chokepoint in Step 3. The
// bridge was already the one way to command the car in Step 5. Now the roster
// is already the whole building. None of that was built for netcode - it was
// built because a scene reload is its own kind of disconnect, and a game that
// can rebuild itself from a description turns out to be a game that can tell
// somebody else what it looks like.
// ====================================================================

using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LootNet : NetworkBehaviour
{
    public static LootNet Instance { get; private set; }

    /// <summary>
    /// One crate, small enough to send. FixedString64Bytes rather than string
    /// because a NetworkList needs a size known before it is packed, and
    /// "Bottled_Water_Bulk" is nowhere near sixty-four bytes.
    /// </summary>
    public struct Rec : INetworkSerializable, IEquatable<Rec>
    {
        public int tier;
        public int value;
        public float mass;
        public FixedString64Bytes name;
        public Vector3 position;
        public Quaternion rotation;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref tier);
            s.SerializeValue(ref value);
            s.SerializeValue(ref mass);
            s.SerializeValue(ref name);
            s.SerializeValue(ref position);
            s.SerializeValue(ref rotation);
        }

        public bool Equals(Rec o) =>
            tier == o.tier && value == o.value &&
            Mathf.Approximately(mass, o.mass) && name.Equals(o.name) &&
            position == o.position && rotation == o.rotation;
    }

    public NetworkList<Rec> Roster;

    void Awake()
    {
        // In Awake, not a field initialiser: NGO needs the list to exist
        // before the object spawns, and a NetworkList built any later throws
        // rather than replicating.
        Roster = new NetworkList<Rec>(
            default, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer)
        {
            Publish();
            return;
        }

        // A client has ALREADY stocked its own building by the time it gets
        // here - LootSpawner.Start ran long before anything connected. So the
        // arriving roster does not add to what is there, it REPLACES it.
        Roster.OnListChanged += OnRosterChanged;
        if (Roster.Count > 0) Rebuild();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) Roster.OnListChanged -= OnRosterChanged;
        if (Instance == this) Instance = null;
    }

    void OnRosterChanged(NetworkListEvent<Rec> _)
    {
        // The list arrives an entry at a time. Rebuilding on each one would
        // tear the whole floor down sixty times, so this waits a frame and
        // does it once, after the last entry has landed.
        if (!rebuildQueued) StartCoroutine(RebuildNextFrame());
    }

    bool rebuildQueued;

    System.Collections.IEnumerator RebuildNextFrame()
    {
        rebuildQueued = true;
        yield return null;
        rebuildQueued = false;
        Rebuild();
    }

    /// <summary>
    /// HOST: describe the building as it stands right now and send it.
    ///
    /// CaptureRemaining first, not Campaign.LootRoster as-is. The roster is
    /// only refreshed at the end of a round, so mid-round it describes where
    /// everything was when the crew last surfaced - and hosting mid-round
    /// would hand the client a building from the past.
    /// </summary>
    public void Publish()
    {
        if (!IsServer) return;

        LootSpawner.CaptureRemaining(null);

        Roster.Clear();
        foreach (var r in Campaign.LootRoster)
            Roster.Add(new Rec {
                tier = r.tier,
                value = r.value,
                mass = r.mass,
                name = new FixedString64Bytes(
                           r.name.Length > 60 ? r.name.Substring(0, 60) : r.name),
                position = r.position,
                rotation = r.rotation,
            });

        Debug.Log($"[Loot] host published {Roster.Count} items - " +
                  "everyone now stocks the same building.");
    }

    // ================================================================
    // CARRYING, ON THE WIRE.
    //
    // The crates have no NetworkObject and no replicated transform, which is
    // right for sixty things lying still - and useless the moment somebody
    // picks one up. So the EVENTS travel instead of the positions: "client 2
    // took item 17", "item 17 is now on the floor here". Between events the
    // item is held by a body every machine is already tracking, so its
    // position comes free.
    //
    // Two messages per crate per trip, rather than a transform update every
    // tick for every crate in the building.
    //
    // The host decides, as everywhere else in this phase - two people
    // grabbing the same crate on the same frame is the whole reason it has
    // to. The first request wins; the second is answered with silence, and
    // the loser's hands simply stay empty.
    // ================================================================

    [ServerRpc(RequireOwnership = false)]
    public void RequestPickupServerRpc(int index, ulong who)
    {
        var item = LootItem.ByIndex(index);
        if (item == null) return;

        var carry = item.GetComponent<Carryable>();
        if (carry == null) return;

        // HELD BY SOMEBODY ELSE. Not merely "held".
        //
        // The first version tested State != Free and refused, which was right
        // for a client and silently broke the HOST. Everyone grabs optimistically
        // - your hands close before the message goes - so by the time this runs
        // the asker is already holding it. On a client that does not matter,
        // because the host's copy is still Free. On the host, the asker and the
        // arbiter are the same machine: it had just set the item to Held itself,
        // then read that back as "somebody has this" and refused its own
        // request. No ClientRpc, so nobody else ever saw it move.
        //
        // Reported as exactly that: he picked it up, and the crate stayed on
        // the floor for everyone else. Client pickups worked the whole time,
        // which is the tell I would have wanted and did not ask for.
        var holder = FindCarrier(who);
        var owner = FindOwnerMotor(who);

        // IN THEIR HANDS **OR IN THEIR BAG**.
        //
        // Taking something back out of your own pack is a pickup, and it would
        // have been refused: the item is Stowed, which is not Free, and it is
        // not in their hands either. So the one carry route the player uses
        // most - stash it, walk, pull it out at the lift - would have gone
        // through cleanly on the grabber's screen and nowhere else.
        //
        // Parented under their own body is the honest test, and it needs no
        // new bookkeeping: Carryable.Stow parents to that player's back.
        bool minesAlready =
            (holder != null && holder.Held == carry) ||
            (owner != null && carry.State == Carryable.CarryState.Stowed &&
             carry.transform.IsChildOf(owner.transform));

        if (carry.State != Carryable.CarryState.Free && !minesAlready)
        {
            Debug.Log($"[Loot] refused pickup of {index} for client {who} - " +
                      $"already {carry.State} and not theirs.");
            return;
        }

        PickupClientRpc(index, who);
    }

    [ClientRpc]
    void PickupClientRpc(int index, ulong who)
    {
        var item = LootItem.ByIndex(index);
        if (item == null)
        {
            // Every machine builds from the same roster in the same order, so
            // this should be impossible. If it ever prints, the buildings have
            // diverged and nothing below can be trusted.
            Debug.LogWarning($"[Loot] told about item {index} and I have no such " +
                             "item - my building does not match the host's.");
            return;
        }

        var carry = item.GetComponent<Carryable>();
        var hands = FindCarrier(who);
        if (carry == null || hands == null)
        {
            Debug.LogWarning($"[Loot] cannot give item {index} to client {who} - " +
                             $"{(carry == null ? "it is not carryable" : "I have no body for them")}.");
            return;
        }

        // The asker already has it in their hands - they picked it up locally
        // the instant they pressed E, because waiting on a round trip to feel
        // your own hands close is exactly the lag nobody should have to feel.
        // This is the confirmation for everybody else.
        if (hands.Held == carry) return;

        hands.ReceiveOverNetwork(carry);
        Debug.Log($"[Loot] item {index} is now in client {who}'s hands here.");
    }

    // ---- INTO THE BAG, AND BACK OUT ----
    //
    // Stowing was the one carry route that sent nothing, so a small item went
    // into somebody's pack and stayed lying on the floor for everyone else -
    // and came back out into hands nobody could see holding it.
    //
    // It is a third event rather than a variant of pickup because it means
    // something different on the receiving machine: a HELD item is carried in
    // front of a body and visible, a STOWED item is parented to that body's
    // back and hidden. Same journey, two different things to draw.
    //
    // Taking it back OUT needs no message of its own - that is a pickup, and
    // pickup already works. Only the direction into the bag was missing.

    [ServerRpc(RequireOwnership = false)]
    public void RequestStowServerRpc(int index, ulong who) => StowClientRpc(index, who);

    [ClientRpc]
    void StowClientRpc(int index, ulong who)
    {
        var item = LootItem.ByIndex(index);
        if (item == null) return;

        var carry = item.GetComponent<Carryable>();
        if (carry == null) return;

        var owner = FindOwnerMotor(who);
        var pack = owner != null ? owner.GetComponent<PlayerBackpack>() : null;
        if (pack == null) return;

        // Already in their bag - this is the stower's own echo.
        if (carry.State == Carryable.CarryState.Stowed) return;

        // Out of whatever hands are holding it first. On the stower's machine
        // PickUp ran a frame ago; on everyone else's, the pickup broadcast may
        // have arrived and put it in their hands, and a stow that left it
        // there would show the item in two places at once.
        var hands = owner.GetComponent<PlayerCarry>();
        if (hands != null && hands.Held == carry) hands.ForceDrop();

        pack.TryStow(carry);
    }

    static PlayerMotor FindOwnerMotor(ulong clientId)
    {
        foreach (var p in PlayerRegistry.All)
        {
            if (p == null) continue;
            var no = p.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned && no.OwnerClientId == clientId) return p;
        }
        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDropServerRpc(int index, Vector3 pos, Quaternion rot, ulong who)
        => DropClientRpc(index, pos, rot, who);

    [ClientRpc]
    void DropClientRpc(int index, Vector3 pos, Quaternion rot, ulong who)
    {
        var item = LootItem.ByIndex(index);
        if (item == null) return;

        var carry = item.GetComponent<Carryable>();
        if (carry == null) return;

        var hands = FindCarrier(who);
        if (hands != null && hands.Held == carry) hands.ForceDrop();
        else carry.Drop(Vector3.zero);

        // Placed where the DROPPER saw it land, not where this machine's
        // physics would have put it. Sixty crates settling independently on
        // four machines is sixty chances to disagree about what is inside the
        // car, and the load gauge reads exactly that.
        item.transform.position = pos;
        item.transform.rotation = rot;

        var body = item.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = pos;
            body.rotation = rot;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    static PlayerCarry FindCarrier(ulong clientId)
    {
        foreach (var p in PlayerRegistry.All)
        {
            if (p == null) continue;
            var no = p.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned && no.OwnerClientId == clientId)
                return p.GetComponent<PlayerCarry>();
        }
        return null;
    }

    /// <summary>CLIENT: throw away my building and build the host's.</summary>
    void Rebuild()
    {
        Campaign.LootRoster.Clear();
        foreach (var r in Roster)
            Campaign.LootRoster.Add(new Campaign.LootRecord {
                tier = r.tier,
                value = r.value,
                mass = r.mass,
                name = r.name.ToString(),
                position = r.position,
                rotation = r.rotation,
            });

        var spawner = FindFirstObjectByType<LootSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[Loot] roster arrived but there is no LootSpawner " +
                             "to build it.");
            return;
        }

        spawner.ClearAndRebuild();
        Debug.Log($"[Loot] rebuilt {Campaign.LootRoster.Count} items from the " +
                  "host's roster. My own randomly-stocked building is gone.");
    }
}
