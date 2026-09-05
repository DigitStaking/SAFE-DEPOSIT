using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One room, as a thing the floor generator can pick up and put down.
///
/// ====================================================================
/// WHAT THIS IS FOR
///
/// Phase 1 built one floor and repeated it twenty times, which was the right
/// call then and is why there is no reason to look at the fifth floor now.
/// The generator that replaces it needs a set of interchangeable pieces, and
/// this is the contract those pieces are built against.
///
/// Three rules, and the third is the one that gets broken:
///
///   1. A module declares its sockets and never decides what fills them.
///      See RoomSocket for why that is the whole design rather than a style
///      preference.
///
///   2. Every module meets the doorway at the same footprint, so any module
///      can follow any other. Grayboxbuilder already fixes that at 2 x 2.5m
///      and this validates against it rather than restating it.
///
///   3. A module must be navigable with ALL its sockets empty. A floor that
///      happens to spawn no loot must still be walkable - otherwise the
///      generator has a rare, unreproducible failure that only appears when
///      the budget runs out, which is the worst kind to own.
/// ====================================================================
///
/// Phase 5, Step 4. See PHASE5_SPEC.md.
/// </summary>
public class RoomModule : MonoBehaviour
{
    /// <summary>
    /// Landing to main to side to back is the fixed shape of a floor, so a
    /// module says which of those it can be. It is a slot in a sequence, not
    /// a description of the room's contents.
    /// </summary>
    public enum Role
    {
        /// <summary>Where you step out of the lift. Must read as "the way
        /// back" from anywhere on the floor.</summary>
        Landing,

        /// <summary>What the landing opens into. Largest, most loot.</summary>
        Main,

        /// <summary>Off the main, optional. Locks and survivors live here.</summary>
        Side,

        /// <summary>The dead end. Best loot, worst place to be caught.</summary>
        Back
    }

    [Tooltip("Which position in the landing -> main -> side -> back sequence " +
             "this module can occupy. The generator picks BY role, so a module " +
             "that could serve as either should be duplicated rather than made " +
             "clever.")]
    public Role role = Role.Main;

    [Tooltip("Only for reading logs and the validator's output. Never matched " +
             "on - two modules may share a label without consequence.")]
    public string label = "";

    // ------------------------------------------------------------------
    // SOCKETS
    //
    // Found by search rather than by a serialised list, deliberately. A list
    // has to be kept in step with the hierarchy by hand, and the failure when
    // it drifts is silent: a socket that exists in the scene, is invisible to
    // the code, and produces a room that is simply emptier than intended. A
    // search cannot drift.
    //
    // The cost is a GetComponentsInChildren, paid once per room at generation
    // time, which is not a budget anybody is fighting over.
    // ------------------------------------------------------------------

    /// <summary>Every socket in this module, whatever its kind.</summary>
    public RoomSocket[] AllSockets() =>
        GetComponentsInChildren<RoomSocket>(includeInactive: false);

    /// <summary>
    /// Every socket of one kind, in hierarchy order.
    ///
    /// Order is stable for a given prefab, which is what lets a seeded
    /// generator put the same thing in the same place on every machine
    /// without sending any of it - the same trick LootSpawner's roster
    /// already uses.
    /// </summary>
    public List<RoomSocket> Sockets(RoomSocket.Kind kind)
    {
        var found = new List<RoomSocket>();

        foreach (var s in AllSockets())
            if (s != null && s.kind == kind) found.Add(s);

        return found;
    }

    /// <summary>Half of a two-room mechanism, by id, or null.</summary>
    public RoomSocket PairedSocket(string pairId)
    {
        if (string.IsNullOrWhiteSpace(pairId)) return null;

        foreach (var s in AllSockets())
            if (s != null && s.pairId == pairId) return s;

        return null;
    }

    // ------------------------------------------------------------------
    // THE QUERY THE REST OF THE GAME ACTUALLY USES
    //
    // "A script can list a room's sockets without knowing what module it is"
    // is this phase's own done-when for Step 4, so it is worth being a single
    // static call rather than something each caller reassembles.
    //
    // It takes a plain Transform and not a RoomModule on purpose: the twenty
    // floors standing today are Level_NN objects built by Grayboxbuilder with
    // no module component on them at all. This returns nothing for those,
    // which is the correct answer and lets every caller carry a fallback
    // rather than a null check.
    // ------------------------------------------------------------------

    /// <summary>
    /// Every socket of one kind anywhere under <paramref name="root"/>,
    /// whether or not the thing is a module. Empty, never null.
    /// </summary>
    public static List<RoomSocket> SocketsUnder(Transform root, RoomSocket.Kind kind)
    {
        var found = new List<RoomSocket>();
        if (root == null) return found;

        foreach (var s in root.GetComponentsInChildren<RoomSocket>(false))
            if (s != null && s.kind == kind) found.Add(s);

        return found;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// What is wrong with this module, in plain sentences. Empty means it
    /// honours the contract.
    ///
    /// Rule 3 - navigable with every socket empty - is deliberately NOT
    /// checked here. It is a claim about pathing, a validator that guessed at
    /// it would be wrong in both directions, and a rule you cannot check is
    /// better stated honestly than approximated. It is on the human, and it
    /// is written down in this file so the human knows.
    /// </summary>
    public List<string> Problems()
    {
        var bad = new List<string>();
        var sockets = AllSockets();

        if (sockets.Length == 0)
            bad.Add("no sockets at all - a module with nothing in it can still " +
                    "be placed, but it will never hold loot, a lock or a survivor");

        // A paired socket with no partner ANYWHERE is a puzzle half that can
        // never be completed. Its partner living in another module is the
        // normal case and is fine - that separation is the point - so this
        // only reports ids that appear twice INSIDE one module, which is the
        // error: both halves in the same room is not a puzzle.
        var seen = new Dictionary<string, int>();

        foreach (var s in sockets)
        {
            if (s == null || !s.IsPaired) continue;
            seen.TryGetValue(s.pairId, out int n);
            seen[s.pairId] = n + 1;
        }

        foreach (var kv in seen)
            if (kv.Value > 1)
                bad.Add($"pairId '{kv.Key}' appears {kv.Value} times in this one " +
                        "module - a two-room mechanism with both halves in the " +
                        "same room is not a puzzle, it is a button next to a door");

        return bad;
    }
}
