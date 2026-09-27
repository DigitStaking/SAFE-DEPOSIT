using UnityEngine;

/// <summary>
/// A door standing in one doorway, and the lock on it.
///
/// ====================================================================
/// A DOOR IS A CONNECTION, NOT A ROOM
///
/// The floor is a tree rooted at the lift. Every door pair is an edge, so
/// locking one edge puts the WHOLE SUBTREE beyond it behind that lock. That
/// single fact drives everything here:
///
///   - one door object per connection, never two (both rooms' RoomExits sit
///     at the same point, so spawning per-exit would give you a door inside
///     a door)
///   - the key must live OUTSIDE the locked subtree, or the floor has locked
///     itself and the round is unwinnable
///
/// The second one is checked in FloorValidator. It is the kind of fault that
/// looks fine in the editor and ends a playtest twenty minutes in.
///
/// WHY A PHYSICAL SLAB AND NOT A UI PROMPT
///
/// Same reason ElevatorButton is a box with a collider rather than a canvas:
/// three other people are standing behind you. A locked door somebody else
/// can see, walk up to, and be handed a key for is a thing that happens in
/// the room. A screen-space "LOCKED" message is invisible to everyone but the
/// person looking at it.
/// ====================================================================
///
/// Phase 5, Step 7. See PHASE5_SPEC.md.
/// </summary>
public class RoomDoor : MonoBehaviour
{
    public enum State
    {
        /// <summary>Shut and impassable. Needs the matching key.</summary>
        Locked,

        /// <summary>Opened. The slab is gone and stays gone.</summary>
        Unlocked,

        /// <summary>Never locked in the first place - an ordinary doorway
        /// that happens to carry a door frame.</summary>
        Jammed
    }

    [Tooltip("Which key opens this. Matched by string against DoorKey.keyId, " +
             "so a floor can hold more than one locked door without them " +
             "sharing a key.")]
    public string keyId = "";

    [SerializeField] State state = State.Locked;

    public State Current => state;
    public bool IsLocked => state == State.Locked;

    /// <summary>The doors this sits between. Both are the same opening.</summary>
    public RoomExit sideA;
    public RoomExit sideB;

    Renderer slab;
    Collider block;

    void Awake()
    {
        slab = GetComponentInChildren<Renderer>();
        block = GetComponentInChildren<Collider>();

        Apply();
    }

    /// <summary>
    /// Open it, if this is the right key.
    ///
    /// Returns false when it is the wrong key or the door is already open, so
    /// the caller can say something useful rather than silently doing nothing.
    /// </summary>
    public bool TryUnlock(DoorKey key)
    {
        if (state != State.Locked) return false;
        if (key == null) return false;
        if (key.keyId != keyId) return false;

        state = State.Unlocked;
        Apply();
        return true;
    }

    /// <summary>Force it open. For the generator, and for a floor that has to
    /// give up on a lock it could not place a key for.</summary>
    public void ForceOpen()
    {
        state = State.Jammed;
        Apply();
    }

    /// <summary>
    /// Make the object match the state.
    ///
    /// Locked shows the slab and blocks; anything else removes both. The slab
    /// is disabled rather than destroyed so a door can be re-locked later
    /// without rebuilding it - Phase 6's puzzles will want exactly that.
    /// </summary>
    void Apply()
    {
        bool shut = state == State.Locked;

        if (slab != null) slab.enabled = shut;
        if (block != null) block.enabled = shut;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = IsLocked ? new Color(1f, 0.5f, 0.2f) : new Color(0.4f, 1f, 0.5f);

        Matrix4x4 was = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, 1.25f, 0f), new Vector3(0.2f, 2.5f, 2f));
        Gizmos.matrix = was;
    }
#endif
}
