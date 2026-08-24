// TwoBodyAudit.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/TwoBodyAudit.cs
// Goes on: nothing. Runs itself on load.
//
// ====================================================================
// PHASE 3 STEP 7 - PROVE IT, DO NOT SQUINT AT IT.
//
// The loot bug cost three wrong fixes and was solved in one run by an audit
// that printed world positions. ROADMAP's KNOWN ISSUES records the lesson:
// when you are about to reason about whether something is right, log it
// instead.
//
// Phase 3 is six steps of claims - one registry, one local player, one camera
// each, one row of state each, one crew list, one keyboard each. Every one of
// them is a number that can be counted at runtime, so this counts them.
//
// It stays silent with one player, so it costs nothing in normal play. It
// only speaks when there are two bodies, which is exactly when it is being
// asked a question.
// ====================================================================

using System.Text;
using UnityEngine;

public static class TwoBodyAudit
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        var go = new GameObject("~TwoBodyAudit");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        float t;
        bool done;

        void Update()
        {
            // ---- F9: ASK AGAIN, NOW ----
            //
            // This audit used to run once, a second after load, and then
            // delete itself. Which meant it had NEVER SEEN A NETWORKED
            // SESSION: nobody has pressed HOST one second into the scene, so
            // every run it ever made was of an offline game with one body.
            //
            // The bug it was built to catch - "one press moves two bodies" -
            // only exists after a client connects. The audit was asleep for
            // the only minute that mattered.
            //
            // F9 now asks at any moment. Press it after joining.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) { Report(); return; }

            if (done) return;

            // A moment after load, so every OnEnable, Awake and Start has run
            // and the registry, the slots and the camera bindings are settled.
            t += Time.deltaTime;
            if (t < 1f) return;

            done = true;

            // Stays resident now instead of destroying itself - it is the F9
            // handler. Silent with one body, as before.
            if (PlayerRegistry.Count < 2) return;

            Report();
        }

        static void Report()
        {
            var sb = new StringBuilder();
            int problems = 0;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            string session = nm == null || !nm.IsListening
                ? "OFFLINE"
                : (nm.IsHost ? $"HOST (my id {nm.LocalClientId})"
                             : $"CLIENT (my id {nm.LocalClientId})");

            sb.AppendLine($"[TwoBody audit] {session} - " +
                          $"{PlayerRegistry.Count} players registered.");
            sb.AppendLine();

            // ---- one local player ----
            int locals = 0;
            foreach (var p in PlayerRegistry.All) if (p != null && p.IsLocal) locals++;

            sb.AppendLine(locals == 1
                ? "  OK    exactly one local player"
                : $"  WRONG {locals} local players - HUD and input will double up");
            if (locals != 1) problems++;

            // ---- per body ----
            foreach (var p in PlayerRegistry.All)
            {
                if (p == null) continue;

                var health = p.GetComponent<PlayerHealth>();
                var cull = p.GetComponent<LocalFirstPersonBodyCull>();
                var lamp = p.GetComponent<PlayerHeadlamp>();
                var member = Crew.Of(p.Slot);

                sb.AppendLine();
                sb.AppendLine($"  {p.gameObject.name}   slot {p.Slot}" +
                              (p.IsLocal ? "   [LOCAL]" : ""));

                // ---- WHAT THE NETWORK THINKS THIS BODY IS ----
                //
                // "The host can control two bodies" has two completely
                // different causes and no way to tell them apart by looking:
                //
                //   a NOT-SPAWNED body  = a leftover ClearScenePlayer missed.
                //                         Nothing owns it, the client cannot
                //                         see it, and the host drives it
                //                         smoothly like a second character.
                //
                //   a spawned body that is NOT MINE but says LOCAL = the
                //                         input gate failed. It fights its
                //                         real owner and jitters.
                //
                // Both look like "two bodies". The fix for one does nothing
                // for the other, which is how this ends up costing three
                // attempts. So print it.
                var no = p.GetComponent<Unity.Netcode.NetworkObject>();
                if (no == null)
                    sb.AppendLine("      net      no NetworkObject (offline body)");
                else if (!no.IsSpawned)
                {
                    sb.AppendLine("      net      NOT SPAWNED - a leftover scene body. " +
                                  "ClearScenePlayer missed it.");
                    problems++;
                }
                else
                    sb.AppendLine($"      net      spawned   owner {no.OwnerClientId}" +
                                  $"   IsOwner {no.IsOwner}");

                if (no != null && no.IsSpawned && no.IsOwner != p.IsLocal)
                {
                    sb.AppendLine($"      WRONG network says IsOwner={no.IsOwner} but the " +
                                  $"body says IsLocal={p.IsLocal} - they must agree");
                    problems++;
                }
                sb.AppendLine($"      camera   {(p.View != null ? p.View.gameObject.name : "NONE")}");
                sb.AppendLine($"      keyboard {(p.Keys != null ? "held" : "none")}");
                sb.AppendLine($"      HP       {member.Health}/{Crew.MaxHealth}" +
                              $"   pack {member.BackpackSlots}");
                sb.AppendLine($"      cull     {(cull != null && cull.enabled ? "ACTIVE" : "off")}" +
                              $"   lamp {(lamp != null && lamp.enabled ? "on" : "off")}");

                if (p.View == null)
                {
                    sb.AppendLine("      WRONG no camera has claimed this body");
                    problems++;
                }

                // Caught by reading a passing run rather than by a check,
                // which is why it is a check now.
                if (!p.IsLocal && p.Keys != null)
                {
                    sb.AppendLine("      WRONG a remote body is holding the keyboard " +
                                  "- one press would drive both");
                    problems++;
                }

                // The headline failure PHASE3_SPEC predicted. The cull shrinks
                // a Head bone to nothing; on a body that is not yours, it
                // takes your teammate's head off.
                if (cull != null && cull.enabled && !p.IsLocal)
                {
                    sb.AppendLine("      WRONG body cull is running on a REMOTE body " +
                                  "- their head is being hidden from everyone");
                    problems++;
                }
            }

            // ---- two bodies must not share a camera ----
            var seen = new System.Collections.Generic.HashSet<FirstPersonCamera>();
            foreach (var p in PlayerRegistry.All)
            {
                if (p == null || p.View == null) continue;
                if (!seen.Add(p.View))
                {
                    sb.AppendLine();
                    sb.AppendLine($"  WRONG two bodies share the camera " +
                                  $"'{p.View.gameObject.name}'");
                    problems++;
                }
            }

            // ---- two bodies must not share a Crew row ----
            var slots = new System.Collections.Generic.HashSet<int>();
            foreach (var p in PlayerRegistry.All)
            {
                if (p == null) continue;
                if (!slots.Add(p.Slot))
                {
                    sb.AppendLine();
                    sb.AppendLine($"  WRONG two bodies share crew slot {p.Slot} " +
                                  "- they will share hit points");
                    problems++;
                }
            }

            sb.AppendLine();
            sb.AppendLine("  Still to check by eye, because no counter can:");
            sb.AppendLine("    - their head is visible to you (main view)");
            sb.AppendLine("    - your head is NOT visible to you (corner view)");
            sb.AppendLine("    - one HUD on screen, not two");
            sb.AppendLine("    - the deck gauge reads 140kg with both aboard");

            if (problems == 0) Debug.Log(sb.ToString());
            else Debug.LogError($"{sb}\n  {problems} problem(s).");
        }
    }
}
