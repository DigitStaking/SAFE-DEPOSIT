// CampaignNet.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/CampaignNet.cs
// Goes on: the NETWORK object, next to NetworkManager and NetworkBootstrap.
//
// ====================================================================
// PHASE 4 STEP 3 - THE SHARED POT.
//
// THE PROBLEM, IN ONE SENTENCE
//
// A static field is one copy PER PROCESS. Two windows are two processes, so
// Campaign.Money is two different numbers that have never met. The host buys
// cable, the host's number changes, and the client's shop cheerfully shows the
// old one. Nothing errors. The two games simply disagree, quietly, forever.
//
// PHASE4_SPEC opens on that count - 59 public statics - and calls it "the
// whole phase in one number". This is the first and largest of them.
//
// WHY NOT REWRITE Campaign AS A NetworkBehaviour
//
// Because 97 places read it. Campaign.Money, Campaign.Quota, Campaign.Capacity
// are spelled that way in the shop, the HUD, the dashboard, the cable gauge,
// the run manager and the loot spawner. Turning the class into a component
// means touching all 97, and 97 edits is 97 chances to introduce a bug in
// working code to fix a bug that is not in any of them.
//
// So the static API stays EXACTLY as it is and this sits behind it. Campaign
// keeps every name it had; the fields simply stop being storage and become
// questions. Ninety-seven call sites do not change, and the eleven that write
// were already funnelled through BuyCable, Settle and AdvanceRun inside the
// class itself. The class was already a chokepoint. It just did not know it
// was going to need to be one.
//
// WHO IS ALLOWED TO WRITE
//
// The host, and only the host. Not because a co-op crew would cheat each
// other, but because two machines that can both change the money will
// eventually both change it in the same frame, and then there is no answer to
// "how much is in the bank" - only two answers. One writer means one answer.
//
// A client that presses BUY is asking. The request goes to the host, the host
// checks it against the same rules it always did, and the new number comes
// back to everybody. The client's shop updates because it is reading a
// replicated value, not because it did anything locally.
//
// OFFLINE IS UNTOUCHED
//
// With no session there is no CampaignNet, and every property falls back to a
// plain static field - which is precisely what it was before this file
// existed. Single player keeps working. That promise has held every step of
// this phase and it holds here.
// ====================================================================

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class CampaignNet : NetworkBehaviour
{
    /// <summary>
    /// The live one, or null when offline. Campaign asks this every time it
    /// is read - it does not cache the answer.
    ///
    /// Fifth time this pattern has been the right one today. A cached answer
    /// about somebody else goes stale; a live one cannot.
    /// </summary>
    public static CampaignNet Instance { get; private set; }

    // ---- THE POT ----
    //
    // WritePermission.Server on every one of them. A client that assigns to
    // these directly gets an exception rather than a desync, which is the
    // better failure by a wide margin: one is a stack trace with a line
    // number, the other is two crews arguing about the bank balance.

    static NetworkVariableWritePermission Host => NetworkVariableWritePermission.Server;

    public readonly NetworkVariable<int>   Money      = new NetworkVariable<int>(Campaign.StartingMoney, default, Host);
    public readonly NetworkVariable<float> Cable      = new NetworkVariable<float>(Campaign.StartingCable, default, Host);
    public readonly NetworkVariable<int>   RunNumber  = new NetworkVariable<int>(1, default, Host);
    public readonly NetworkVariable<int>   Capacity   = new NetworkVariable<int>(0, default, Host);
    public readonly NetworkVariable<int>   CableBought    = new NetworkVariable<int>(0, default, Host);
    public readonly NetworkVariable<int>   CapacityBought = new NetworkVariable<int>(0, default, Host);
    public readonly NetworkVariable<bool>  Over       = new NetworkVariable<bool>(false, default, Host);
    public readonly NetworkVariable<float> Strain     = new NetworkVariable<float>(0f, default, Host);
    public readonly NetworkVariable<bool>  Seeded     = new NetworkVariable<bool>(false, default, Host);

    /// <summary>
    /// Which rooms are rubble, one bit per floor.
    ///
    /// A BITMASK RATHER THAN A LIST, because there are twenty floors and a
    /// uint has thirty-two bits. The whole demolished state of the building
    /// fits in four bytes and arrives atomically - no list events, no
    /// half-applied set, no rebuilding the geometry once per entry as the
    /// entries trickle in.
    ///
    /// This is the collection Step 3 deferred, and the note there said it
    /// would move with the system that owns it. It does: rooms seal on a
    /// timer that RunManager runs and Campaign.SealRandomRooms rolls, and
    /// both of those were happening independently on every machine. Two crews
    /// in two buildings with two different sets of rubble - and the one
    /// standing in a doorway the other saw as sealed.
    /// </summary>
    public readonly NetworkVariable<uint>  Sealed     = new NetworkVariable<uint>(0u, default, Host);

    /// <summary>
    /// PHASE 4 STEP 9. Who the mafia is holding, and how much has been paid
    /// toward each of them.
    ///
    /// The LAST of the three collections Step 3 deferred, and the note there
    /// said each would move with the system that owns it. This one owns
    /// itself: the rescue contract is the system.
    ///
    /// A list rather than a bitmask, unlike the sealed rooms, because these
    /// entries carry data - a name, a floor, a round, a running total - and
    /// they change one at a time, rarely, at the shop. Nothing here needs to
    /// arrive atomically the way a demolished building did.
    /// </summary>
    public NetworkList<LostRec> Lost;

    public struct LostRec : INetworkSerializable, System.IEquatable<LostRec>
    {
        public FixedString32Bytes name;
        public int floor;
        public int runLost;
        public int paid;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref name);
            s.SerializeValue(ref floor);
            s.SerializeValue(ref runLost);
            s.SerializeValue(ref paid);
        }

        public bool Equals(LostRec o) =>
            name.Equals(o.name) && floor == o.floor &&
            runLost == o.runLost && paid == o.paid;
    }

    void Awake()
    {
        // In Awake, not a field initialiser: NGO needs the list to exist
        // before the object spawns, and one built any later throws instead of
        // replicating. Same rule LootNet's roster follows.
        Lost = new NetworkList<LostRec>(
            default, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public readonly NetworkVariable<FixedString128Bytes> Epitaph =
        new NetworkVariable<FixedString128Bytes>(default, default, Host);

    public override void OnNetworkSpawn()
    {
        Instance = this;

        // ---- THE HOST BRINGS ITS CAMPAIGN WITH IT ----
        //
        // Whoever hosts has been playing offline: their statics hold the real
        // save - money earned, cable bought, runs survived. Those numbers seed
        // the session so hosting does not silently wipe the campaign.
        //
        // Clients do the opposite and take whatever arrives. Their own statics
        // are their OWN save from their OWN sessions, and adopting the host's
        // is the entire point of joining somebody's game.
        if (IsServer)
        {
            Campaign.PushLocalStateToNetwork();
        }
        else
        {
            // The building's damage arrives as one number. Applying it needs
            // the geometry sealed too, not just the set updated, so it goes
            // through RunManager rather than straight into Campaign.
            Sealed.OnValueChanged += OnSealedChanged;
            if (Sealed.Value != 0u) OnSealedChanged(0u, Sealed.Value);

            Lost.OnListChanged += OnLostChanged;
            Campaign.ApplyLostCrew(Lost);
        }

        Debug.Log($"[Net] campaign is now {(IsServer ? "HOST-OWNED" : "replicated from the host")} " +
                  $"- bank {Money.Value}, cable {Cable.Value:0}m, run {RunNumber.Value}");
    }

    void OnLostChanged(NetworkListEvent<LostRec> _) => Campaign.ApplyLostCrew(Lost);

    void OnSealedChanged(uint _, uint now)
    {
        Campaign.ApplySealedMask(now);

        var run = SceneRefs.Run;
        if (run != null) run.RebuildRubbleFromCampaign();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            Sealed.OnValueChanged -= OnSealedChanged;
            Lost.OnListChanged -= OnLostChanged;
        }

        // Leave the last known numbers in the statics rather than snapping
        // back to whatever this machine had before it joined. You keep what
        // the run earned.
        Campaign.PullNetworkStateToLocal();
        if (Instance == this) Instance = null;
    }

    // ================================================================
    // ASKING TO SPEND
    //
    // RequireOwnership = false: the NETWORK object is owned by the host, and
    // it is a client that needs to call these. Without it NGO refuses the
    // call, which is correct behaviour for a thing nobody owns and wrong for
    // a shop counter.
    //
    // Every one of these re-runs the SAME rule the offline game runs. The
    // client already checked before asking - so the button greys out
    // correctly - but a check on the asking machine is a courtesy, not a
    // guarantee. The host checks because the host is the one that decides.
    // ================================================================

    [ServerRpc(RequireOwnership = false)]
    public void BuyCableServerRpc() => Campaign.BuyCableAuthoritative();

    [ServerRpc(RequireOwnership = false)]
    public void BuyCapacityServerRpc() => Campaign.BuyCapacityAuthoritative();

    [ServerRpc(RequireOwnership = false)]
    public void BuyBackpackSlotServerRpc(int slot) => Campaign.BuyBackpackSlotAuthoritative(slot);

    [ServerRpc(RequireOwnership = false)]
    public void BuyMedSprayServerRpc(int slot) => Campaign.BuyMedSprayAuthoritative(slot);

    /// <summary>
    /// Anybody at the shop can put money toward a rescue. Deliberately not
    /// restricted to whoever is nominally in charge - the whole argument the
    /// step exists to create is about the crew's money, so any of them must be
    /// able to reach for it and the others must see it happen.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void PayRescueServerRpc(int index, int amount) => Campaign.PayRescue(index, amount);

    /// <summary>
    /// Somebody has been bought back. Sent to everyone; only their own machine
    /// can actually write their health, and everyone else learns it from the
    /// replicated row a moment later.
    /// </summary>
    [ClientRpc]
    public void RescuedClientRpc(int slot) => Campaign.ApplyRescue(slot);

    // ================================================================
    // A REVIVE TAKES THREE MACHINES, AND EACH DOES ONLY WHAT IT OWNS.
    //
    //   THE SPRAYER asks. They aimed, they held R for two seconds, and that
    //   is the entire extent of what they are the authority on.
    //
    //   THE HOST spends. The kit is host-owned like the money, so it is the
    //   only machine that can honestly answer "is there a spray left" - and
    //   the check has to happen here, because two people spraying the same
    //   friend in the same second would otherwise cost two sprays and revive
    //   them once.
    //
    //   THE DOWNED PLAYER stands up. Step 4 made each Crew row owner-written,
    //   so nobody else CAN set their health. That is not an obstacle to work
    //   around, it is the rule working: the alternative is four machines each
    //   deciding somebody else's HP.
    //
    // Sent to everyone rather than just the target, because the target's
    // machine has to do the revive and every other machine has to stop drawing
    // them kneeling.
    // ================================================================

    /// <summary>
    /// PHASE 4 STEP 8. A client pressing "go back down".
    ///
    /// The host runs the same RunManager code it would have run for its own
    /// press, so the rules about what a new round costs live in exactly one
    /// place - the same arrangement as the lift in Step 5, where a client's
    /// button press re-enters ElevatorBridge on the host rather than
    /// shortcutting to the car.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void NextRoundServerRpc()
    {
        var run = SceneRefs.Run;
        if (run != null) run.GoBackDown();
        else Debug.LogWarning("[Net] a client asked for the next round and the " +
                              "host has no RunManager to ask.");
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReviveServerRpc(ulong target, int sprayerSlot)
    {
        // Spent from the SPRAYER'S own supply, which is the whole point of
        // making sprays personal: the person who runs in is the person who
        // pays, and if they had none, nothing happens no matter how many
        // somebody safe upstairs is carrying.
        if (!Campaign.ConsumeMedSpray(sprayerSlot))
        {
            Debug.Log($"[Crew] revive refused - slot {sprayerSlot} has no spray.");
            return;
        }

        ReviveClientRpc(target);
    }

    [ClientRpc]
    void ReviveClientRpc(ulong target)
    {
        foreach (var p in PlayerRegistry.All)
        {
            if (p == null) continue;

            var no = p.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned || no.OwnerClientId != target) continue;

            var downed = p.GetComponent<DownedPlayer>();
            if (downed != null) downed.Revive();
            return;
        }
    }
}
