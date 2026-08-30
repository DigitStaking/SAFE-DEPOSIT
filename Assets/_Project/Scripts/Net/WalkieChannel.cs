// WalkieChannel.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/WalkieChannel.cs
// Goes on: the CAMPAIGN object, beside CampaignNet.
//
// ====================================================================
// PHASE 4 STEP 10, PART 5 - ONE VOICE AT A TIME.
//
// PHASE4_SPEC, at the user's request on 21 Aug 2026: "when we use talky walky
// voice will be clear but one can talk at a time".
//
// Push to talk. The first press holds the channel; anybody else pressing
// while it is held gets a click and nothing else, and the crew hears only
// the person who got there first.
//
// WHY THIS IS THE BETTER GAME, NOT JUST THE REALISTIC ONE
//
// It makes the walkie-talkie a TRADE rather than an upgrade:
//
//                proximity            walkie-talkie
//   range        one room             the whole building
//   clarity      muffled by concrete  clear
//   who talks    everyone at once     EXACTLY ONE PERSON
//
// Four people panicking into one channel produces the thing this design keeps
// reaching for: somebody has to shut up so somebody else can be heard. "Get
// off the radio" is a sentence the mechanic writes by itself.
//
// THE HOST ARBITRATES, and this is one of the few places where that is not
// merely convenient - "who pressed first" has no answer at all unless one
// machine decides it. Two clients pressing in the same 50ms would each
// believe they won, on their own screen, and the crew would hear both.
// ====================================================================

using Unity.Netcode;
using UnityEngine;

public class WalkieChannel : NetworkBehaviour
{
    public static WalkieChannel Instance { get; private set; }

    [Tooltip("Hold to talk on the radio.")]
    public KeyCode key = KeyCode.V;

    /// <summary>
    /// Who is holding the channel, or ulong.MaxValue for nobody.
    ///
    /// Server-write: the whole point is that one machine decides, so a client
    /// that could write this could talk over anybody.
    /// </summary>
    public readonly NetworkVariable<ulong> Holder =
        new NetworkVariable<ulong>(Nobody, default, NetworkVariableWritePermission.Server);

    public const ulong Nobody = ulong.MaxValue;

    public bool Busy => Holder.Value != Nobody;
    public bool HeldByMe =>
        NetworkManager.Singleton != null &&
        Holder.Value == NetworkManager.Singleton.LocalClientId;

    bool pressed;
    float lastRefusedAt = -99f;

    public override void OnNetworkSpawn() => Instance = this;

    public override void OnNetworkDespawn()
    {
        // Let go of the channel on the way out. A client that disconnects
        // mid-transmission would otherwise hold the radio for everybody else
        // forever, and nobody could ever work out why.
        if (IsServer && Holder.Value != Nobody) Holder.Value = Nobody;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsSpawned) return;

        var me = PlayerRegistry.Local;
        if (me == null) return;

        var kb = PlayerRegistry.KeysOf(me);
        bool down = kb != null && kb[UnityEngine.InputSystem.Key.V].isPressed;

        if (down == pressed) return;
        pressed = down;

        if (down) RequestServerRpc(NetworkManager.Singleton.LocalClientId);
        else ReleaseServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestServerRpc(ulong who)
    {
        // FIRST PRESS WINS AND IS NOT INTERRUPTIBLE. A later press is refused
        // outright rather than queued - a queue would mean your voice arrives
        // seconds after the moment you needed it, which is worse than being
        // told no.
        if (Holder.Value != Nobody)
        {
            RefusedClientRpc(new ClientRpcParams {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { who } }
            });
            return;
        }

        Holder.Value = who;
    }

    [ServerRpc(RequireOwnership = false)]
    void ReleaseServerRpc(ulong who)
    {
        // Only the holder may release it. Without this, anybody letting go of
        // their own key would clear somebody else's transmission.
        if (Holder.Value == who) Holder.Value = Nobody;
    }

    /// <summary>The click you get for talking over somebody.</summary>
    [ClientRpc]
    void RefusedClientRpc(ClientRpcParams _ = default) => lastRefusedAt = Time.time;

    void OnGUI()
    {
        if (!IsSpawned || !RunHudGate.ShouldDrawGameplayHud()) return;

        string msg = null;
        Color colour = Color.white;

        if (HeldByMe)
        {
            msg = "● ON AIR";
            colour = new Color(1f, 0.35f, 0.3f);
        }
        else if (Time.time - lastRefusedAt < 1.2f)
        {
            // Named, not just refused. "Channel busy" tells you to try again;
            // knowing WHO has it tells you to shout at them.
            msg = $"CHANNEL BUSY - player {Holder.Value} is talking";
            colour = new Color(1f, 0.75f, 0.3f);
        }
        else if (Busy)
        {
            msg = $"player {Holder.Value} on the radio";
            colour = new Color(1f, 1f, 1f, 0.55f);
        }

        if (msg == null) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = colour;

        GUI.Label(new Rect(0f, 90f, Screen.width, 22f), msg, style);
    }
}
