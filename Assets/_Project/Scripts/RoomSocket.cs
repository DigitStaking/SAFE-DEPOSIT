using UnityEngine;

/// <summary>
/// A named place in a room module where something MIGHT go.
///
/// ====================================================================
/// THE ONE RULE, AND EVERYTHING ELSE FOLLOWS FROM IT
///
/// A module declares its sockets. It never decides what fills them.
///
/// That sounds like a formality and it is the whole design. The moment a
/// module knows it contains "the good loot" or "the survivor", the generator
/// stops being able to arrange modules and starts having to understand them -
/// and every new inhabitant in Phases 6 and 7 becomes a change to every room
/// that might host it, rather than a change to the thing doing the placing.
///
/// So a socket is a transform and a label. It has no prefab reference, no
/// spawn logic, and no opinion. LootSpawner decides what loot is; the lock
/// system decides what a lock is; the survivor system decides what a survivor
/// is. Each of them asks "where may I put one of these", and the room answers
/// without knowing why it was asked.
/// ====================================================================
///
/// Phase 5, Step 4. See PHASE5_SPEC.md.
/// </summary>
public class RoomSocket : MonoBehaviour
{
    public enum Kind
    {
        /// <summary>An item may be spawned here. Filled in Phase 5.</summary>
        Loot,

        /// <summary>A door, keypad or shutter belongs here. Phase 5, Step 7.</summary>
        Lock,

        /// <summary>Somewhere a hazard or an inhabitant can live. Phase 6.</summary>
        Hazard,

        /// <summary>A downed body to be carried out. Phase 5, Step 8.</summary>
        Survivor,

        /// <summary>Half of a mechanism. See pairId below. Phase 6.</summary>
        Puzzle
    }

    [Tooltip("What KIND of thing may go here - never which thing. The room " +
             "offers a place; the system that owns that kind decides what to " +
             "put in it, or to leave it empty.")]
    public Kind kind = Kind.Loot;

    // ------------------------------------------------------------------
    // PAIRING, WHICH EXISTS FOR PUZZLES.md AND NOTHING ELSE YET
    //
    // Every puzzle in that document is split across TWO rooms: a button in A
    // that opens a shutter in B, two dials in two rooms, a symbol board and
    // the lock it describes. That is deliberate - it is what forces people to
    // talk, and voice chat is the mechanic the whole middle of this game is
    // built on.
    //
    // A generator that can only place things one room at a time cannot build
    // any of them. So the halves are labelled here, in Phase 5, while the
    // contract is cheap to change - even though nothing reads pairId until
    // Phase 6. Retrofitting a pairing concept into a socket system that
    // already has rooms authored against it is the expensive version of this
    // decision, and it is entirely avoidable by spending one field now.
    // ------------------------------------------------------------------

    [Tooltip("Blank for a socket that stands alone. Two sockets sharing a " +
             "non-blank id are two halves of one mechanism, and the generator " +
             "must place them in DIFFERENT rooms on the same floor - that " +
             "separation is the puzzle. Nothing reads this until Phase 6.")]
    public string pairId = "";

    /// <summary>Half of a two-room mechanism.</summary>
    public bool IsPaired => !string.IsNullOrWhiteSpace(pairId);

    // ------------------------------------------------------------------

    /// <summary>
    /// Where the thing actually goes, and which way it faces.
    ///
    /// The socket's own transform, deliberately - not a computed offset. If a
    /// crate is landing badly, the fix is to move the empty in the prefab and
    /// see it move, rather than to find the code that adds 0.05 to y.
    /// </summary>
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

#if UNITY_EDITOR
    // Scene view only - OnDrawGizmos does not run in a build, and this draws
    // nothing at play time even in the editor. Sockets are invisible empties
    // otherwise, and an invisible empty is one you place wrong.
    void OnDrawGizmos()
    {
        Gizmos.color = kind switch
        {
            Kind.Loot     => new Color(1f, 0.85f, 0.3f),
            Kind.Lock     => new Color(0.4f, 0.7f, 1f),
            Kind.Hazard   => new Color(1f, 0.35f, 0.3f),
            Kind.Survivor => new Color(0.5f, 1f, 0.6f),
            _             => new Color(0.8f, 0.5f, 1f)
        };

        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.6f);
    }
#endif
}
