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
        if (IsServer) Campaign.PushLocalStateToNetwork();

        Debug.Log($"[Net] campaign is now {(IsServer ? "HOST-OWNED" : "replicated from the host")} " +
                  $"- bank {Money.Value}, cable {Cable.Value:0}m, run {RunNumber.Value}");
    }

    public override void OnNetworkDespawn()
    {
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
}
