// ArmPoseAudit.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ArmPoseAudit.cs
// Goes on: nothing. Starts itself, like VoiceMic.
//
// ========================================================================
// WHAT IS ACTUALLY HOLDING THE ARMS UP.
//
// Four rounds of fixing IK writers have not moved those hands, which means
// the assumption behind all four - that some IK goal is still pinned - may
// simply be wrong. Every one of those fixes was reasoned from the code and
// none of them was ever CHECKED against what the Animator is really doing.
//
// So this asks the Animator directly and prints the answer:
//
//   which CLIPS are playing, on which layer, at what weight
//   what the masked Arms layer is contributing
//   whether the two IK writers are alive or switched off
//
// If a clip is posing the arms, its name appears here and no amount of IK
// bookkeeping was ever going to help. If the Arms layer is sitting at weight
// 1 on an empty state, that shows up as a layer with weight and no clips.
// Either way the guessing ends with one line of output.
//
// Prints for a few seconds and stops - long enough to read, short enough not
// to bury the console.
// ========================================================================

using System.Text;
using UnityEngine;

public class ArmPoseAudit : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindFirstObjectByType<ArmPoseAudit>() != null) return;

        var go = new GameObject("~ArmPoseAudit");
        go.hideFlags = HideFlags.DontSave;
        go.AddComponent<ArmPoseAudit>();
    }

    [Tooltip("Seconds to keep reporting after a body is found.")]
    public float reportFor = 8f;

    [Tooltip("Seconds between lines.")]
    public float every = 1.5f;

    Animator anim;
    Transform body;
    float next;
    float started = -1f;

    void Update()
    {
        if (anim == null)
        {
            var motor = PlayerRegistry.Local;
            if (motor == null) return;

            body = motor.transform;
            anim = body.GetComponentInChildren<Animator>();
            if (anim == null) return;

            started = Time.time;
        }

        if (Time.time - started > reportFor) return;
        if (Time.time < next) return;
        next = Time.time + every;

        Report();
    }

    void Report()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;

        var sb = new StringBuilder("[ArmAudit] ");

        for (int layer = 0; layer < anim.layerCount; layer++)
        {
            sb.Append('\n')
              .Append("  layer ").Append(layer)
              .Append(" '").Append(anim.GetLayerName(layer)).Append("'")
              .Append("  weight ").Append(anim.GetLayerWeight(layer).ToString("0.00"));

            // The CLIPS actually contributing to the pose right now, with the
            // share each one has. This is the line that names the culprit if a
            // clip is posing the arms.
            var clips = anim.GetCurrentAnimatorClipInfo(layer);

            if (clips.Length == 0)
            {
                sb.Append("  -> NO CLIPS (empty state)");
            }
            else
            {
                sb.Append("  -> ");
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].clip == null) continue;
                    if (i > 0) sb.Append(" + ");
                    sb.Append(clips[i].clip.name)
                      .Append(' ')
                      .Append(clips[i].weight.ToString("0.00"));
                }
            }

            var st = anim.GetCurrentAnimatorStateInfo(layer);
            sb.Append("   [tagHash ").Append(st.tagHash).Append("]");
        }

        // ---- WHO IS STILL ALLOWED TO WRITE HAND IK ----
        //
        // Unity has no getter for an IK weight, so the best that can be done
        // is report whether each writer is alive. If both say OFF and the arms
        // are still wrong, IK is not the cause and the clips above are.
        var hands = body.GetComponentInChildren<FirstPersonHands>(true);
        var pushArms = body.GetComponentInChildren<PlayerPushArms>(true);

        sb.Append("\n  FirstPersonHands: ")
          .Append(hands == null ? "MISSING"
                                : (hands.enabled ? "ENABLED (still driving hands)" : "off"));

        sb.Append("   PlayerPushArms: ")
          .Append(pushArms == null ? "MISSING"
                                   : (pushArms.enabled ? "enabled" : "off"));

        Debug.Log(sb.ToString());
    }
}
