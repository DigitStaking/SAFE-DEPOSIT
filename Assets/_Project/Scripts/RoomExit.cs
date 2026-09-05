using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A DOOR. Not an opening the generator may or may not use - a connection the
/// room is asking for.
///
/// ====================================================================
/// DOORS ARE NOT DECORATION. DOORS DEFINE THE CONNECTIONS.
///
/// A room with three doors must end up with three connections. Not two and a
/// wall, not two and a hole onto the skybox. If a room cannot have all of its
/// doors satisfied where the generator wants to put it, that room does not go
/// there - a different room does, or the generator backtracks.
///
/// The previous generator got this exactly backwards. It grew geometry first
/// and sealed whatever it could not connect, so a four-door crossroads
/// routinely became a two-door corridor with two walls in it. Every one of
/// those seals was the generator hiding a failure it should have retried.
///
/// The only door allowed to lead out of the floor is one marked isEntrance,
/// and there is exactly one of those per floor: where the lift arrives.
/// ====================================================================
///
/// THE FRAME
///
///   +X points OUT of the room, through the opening.
///
/// So connecting door A to door B means placing B's room such that B sits on
/// A and faces back along it: same position, opposite forward. That is the
/// whole of the connection maths, and it works for any two rooms at any
/// rotation because neither has to know the other's shape.
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public class RoomExit : MonoBehaviour
{
    [Tooltip("The one door per floor that is ALLOWED to lead out - where the " +
             "lift arrives. Every other door must connect to another room, " +
             "and a floor with anything other than exactly one entrance is a " +
             "floor that failed to generate.")]
    public bool isEntrance;

    [Tooltip("Set by the generator when this door has been connected to " +
             "another room's door. A normal door left false at the end of " +
             "generation is a bug, not a wall to be built.")]
    public bool connected;

    [Tooltip("Which door this is connected TO. Kept so validation can check " +
             "that connections are mutual - a door claiming a partner that " +
             "does not claim it back is exactly the kind of half-made link " +
             "that looks fine and plays wrong.")]
    public RoomExit partner;

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

    /// <summary>Straight out through the opening.</summary>
    public Vector3 Outward => transform.right;

    /// <summary>Every door under a transform. Empty, never null.</summary>
    public static RoomExit[] DoorsUnder(Transform root) =>
        root == null ? new RoomExit[0]
                     : root.GetComponentsInChildren<RoomExit>(false);

    /// <summary>Doors still needing a room on the other side.</summary>
    public static List<RoomExit> FreeDoors(Transform root)
    {
        var free = new List<RoomExit>();

        foreach (var d in DoorsUnder(root))
            if (d != null && !d.connected && !d.isEntrance) free.Add(d);

        return free;
    }

    /// <summary>Record a connection on both sides at once, because a
    /// one-sided link is the failure this field exists to catch.</summary>
    public static void Join(RoomExit a, RoomExit b)
    {
        if (a == null || b == null) return;

        a.connected = true;
        b.connected = true;
        a.partner = b;
        b.partner = a;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = isEntrance ? new Color(0.4f, 0.8f, 1f)
                     : connected  ? new Color(0.4f, 1f, 0.5f)
                                  : new Color(1f, 0.3f, 0.3f);   // unconnected = wrong

        Matrix4x4 was = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, 1.25f, 0f), new Vector3(0.1f, 2.5f, 2f));
        Gizmos.DrawRay(Vector3.zero, Vector3.right * 1.5f);
        Gizmos.matrix = was;
    }
#endif
}
