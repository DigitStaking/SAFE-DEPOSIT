// ElbowAudit.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ElbowAudit.cs
// Goes on: nothing. Starts itself. Toggle with F9.
//
// ========================================================================
// WHY THE ELBOW IS NOT MOVING, ON SCREEN, IN NUMBERS.
//
// "I need to know why changing 91 to 30 or 91 to 150 produces no visible
//  difference. Add temporary debug information if necessary."
//
// Right, and this project has learned that lesson expensively enough that the
// diagnostic should have come first: the IK WEIGHT readout, ArmPoseAudit, and
// reading Editor.log each ended a multi-round guessing loop that code-reading
// had not. Reasoning about IK from the source has a bad record here.
//
// It answers the whole chain in one panel:
//
//   which Animator is being driven, and whether it is the one you can SEE
//   whether OnAnimatorIK ran this frame
//   the angle and weight actually read off the held item
//   the hint position, and HOW FAR IT MOVED since last frame
//   span vs reach - whether the arm has any bend left to steer
//
// The last line is the one that mattered. An IK goal further away than the arm
// is long makes the solver extend the limb fully; a fully extended arm has its
// elbow ON the shoulder-to-hand line; and an elbow on that line cannot be
// steered by anything. The elbow control was disabled by a grip bug two
// systems away, and nothing said so.
//
// DELETE THIS once elbows are dialled in. It is a diagnostic, not a feature.
// ========================================================================

using System.Text;
using UnityEngine;

public class ElbowAudit : MonoBehaviour
{
    public static bool Show = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("~ElbowAudit");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<ElbowAudit>();
    }

    PlayerCarryArms arms;
    Vector3 lastLeftHint, lastRightHint;
    float leftMoved, rightMoved;

    GUIStyle style;

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame) Show = !Show;

        if (!Show) return;

        if (arms == null) arms = FindLocalArms();
        if (arms == null) return;

        // How far the hint travelled since last frame. This is the number that
        // settles it: if it reads 0.000 while you drag the angle slider, the
        // hint is not moving and the reason is upstream of the solver.
        leftMoved = Vector3.Distance(arms.LeftElbowReport.hint, lastLeftHint);
        rightMoved = Vector3.Distance(arms.RightElbowReport.hint, lastRightHint);

        lastLeftHint = arms.LeftElbowReport.hint;
        lastRightHint = arms.RightElbowReport.hint;
    }

    static PlayerCarryArms FindLocalArms()
    {
        foreach (var a in FindObjectsByType<PlayerCarryArms>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var motor = a.GetComponentInParent<PlayerMotor>();
            if (motor != null && PlayerRegistry.IsLocalFor(motor)) return a;
        }

        return FindFirstObjectByType<PlayerCarryArms>();
    }

    void OnGUI()
    {
        if (!Show) return;

        if (style == null)
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
                normal = { textColor = Color.white }
            };

        var sb = new StringBuilder();

        sb.AppendLine("<b>ELBOW AUDIT</b>   F9 to hide");

        if (arms == null)
        {
            sb.AppendLine("no PlayerCarryArms in the scene");
            Draw(sb);
            return;
        }

        sb.AppendLine("animator on: " + arms.gameObject.name);

        // ---- WHICH ARMS ARE YOU ACTUALLY LOOKING AT ----
        //
        // PlayerCarryArms is stripped from the first-person viewmodel clone, so
        // in first person the elbow IK is being applied to a body you cannot
        // see. Worth saying out loud rather than leaving as a surprise.
        var vm = FindFirstObjectByType<FirstPersonViewmodel>();

        if (vm != null)
            sb.AppendLine("<color=#ffd479>a first-person viewmodel exists - it has NO " +
                          "PlayerCarryArms, so press P for third person to see " +
                          "elbow changes</color>");

        var item = arms.LiveItem;
        sb.AppendLine("holding: " + (item != null ? item.name : "nothing"));

        Hand(sb, "LEFT ", arms.LeftElbowReport, leftMoved);
        Hand(sb, "RIGHT", arms.RightElbowReport, rightMoved);

        Draw(sb);
    }

    static void Hand(StringBuilder sb, string label, PlayerCarryArms.ElbowReport r,
                     float moved)
    {
        if (!r.asked)
        {
            sb.AppendLine(label + "  elbow not steered (Use Elbow Hint off, or " +
                          "this hand unused, or weight 0)");
            return;
        }

        sb.AppendLine(label +
                      "  angle " + r.angle.ToString("0.0") +
                      "   weight " + r.weight.ToString("0.00") +
                      " -> applied " + r.appliedWeight.ToString("0.00"));

        sb.AppendLine("       hint " + r.hint.ToString("F3") +
                      "   moved " + moved.ToString("F4") + "m since last frame");

        sb.AppendLine("       span " + r.span.ToString("F3") +
                      "m  /  reach " + r.reach.ToString("F3") + "m");

        if (r.overstretched)
            sb.AppendLine("       <color=#ff6b6b>OVERSTRETCHED - the hand target is " +
                          "further than the arm is long, so the arm is fully " +
                          "extended and the elbow is ON the shoulder-to-hand " +
                          "line. Nothing can steer it. FIX THE GRIP, not the " +
                          "elbow: Seed From Bounds, then Save.</color>");
    }

    void Draw(StringBuilder sb)
    {
        var rect = new Rect(12f, 12f, 720f, 220f);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f,
                           rect.height - 12f),
                  sb.ToString(), style);
    }
}
