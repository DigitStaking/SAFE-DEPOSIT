using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks room modules against the contract, and says what a floor is
/// actually offering.
///
/// This exists because Step 4 has nothing to look at. The sockets are
/// invisible empties, the rules they obey are in a comment, and a module that
/// silently declares no LootAnchors looks exactly like one that declares
/// three - right up until a generated floor is mysteriously empty and the
/// cause is four steps behind you.
///
/// So: one menu item that reads the hierarchy and reports. It validates what
/// can be validated and says plainly which rule it is not checking, rather
/// than approximating it and being wrong in both directions.
///
/// Phase 5, Step 4. See PHASE5_SPEC.md.
/// </summary>
public static class RoomModuleValidator
{
    [MenuItem("Tools/Rooms/Validate Modules In Scene")]
    public static void Validate()
    {
        var modules = Object.FindObjectsByType<RoomModule>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var loose = Object.FindObjectsByType<RoomSocket>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var sb = new StringBuilder();
        sb.AppendLine("ROOM MODULE CONTRACT");
        sb.AppendLine();

        if (modules.Length == 0)
        {
            sb.AppendLine("No RoomModule in the scene.");
            sb.AppendLine();
            sb.AppendLine("That is the expected answer until Step 5 builds the six");
            sb.AppendLine("modules. The twenty Level_NN floors standing today are");
            sb.AppendLine("Grayboxbuilder output with no module component, and");
            sb.AppendLine("LootSpawner falls back to its hardcoded slots for them.");
        }

        int problems = 0;

        foreach (var m in modules)
        {
            var bad = m.Problems();
            problems += bad.Count;

            sb.AppendLine($"{m.name}   role={m.role}" +
                          (string.IsNullOrWhiteSpace(m.label) ? "" : $"   \"{m.label}\""));

            foreach (RoomSocket.Kind k in System.Enum.GetValues(typeof(RoomSocket.Kind)))
            {
                int n = m.Sockets(k).Count;
                if (n > 0) sb.AppendLine($"    {k,-9} x{n}");
            }

            foreach (var b in bad) sb.AppendLine($"    PROBLEM: {b}");
            sb.AppendLine();
        }

        // A socket outside any module is not automatically wrong - SocketsUnder
        // takes a plain Transform precisely so a hand-built floor can carry
        // sockets without being a module - but it is worth naming, because the
        // usual cause is a prefab that lost its RoomModule component.
        int orphans = 0;
        foreach (var s in loose)
            if (s != null && s.GetComponentInParent<RoomModule>() == null) orphans++;

        if (orphans > 0)
        {
            sb.AppendLine($"{orphans} socket(s) with no RoomModule above them.");
            sb.AppendLine("Legal - SocketsUnder works on any Transform - but usually");
            sb.AppendLine("means a module lost its component.");
            sb.AppendLine();
        }

        sb.AppendLine("NOT CHECKED, and on you:");
        sb.AppendLine("  \"navigable with every socket empty\". It is a claim about");
        sb.AppendLine("  pathing; a validator guessing at it would be wrong in both");
        sb.AppendLine("  directions, and a rule you cannot check is better stated");
        sb.AppendLine("  than approximated. Walk each module with nothing in it.");
        sb.AppendLine();
        sb.AppendLine(problems == 0
            ? $"{modules.Length} module(s), no contract problems."
            : $"{modules.Length} module(s), {problems} problem(s) above.");

        Debug.Log(sb.ToString());
    }
}
