using UnityEngine;

/// <summary>
/// A doorway another module can be attached to.
///
/// ====================================================================
/// THE FRAME, WHICH IS THE ENTIRE POINT
///
/// Every module is authored so its OWN entrance sits at the module origin,
/// facing +X into the room. An exit is the same frame pointing the other way:
/// stand on it, look down +X, and that is where the next room begins.
///
/// So attaching module B to an exit of module A is one line - put B's origin
/// on the exit, match its rotation - and it works for any A and any B without
/// either knowing about the other. That is what "any module can follow any
/// other" actually means in practice, and it is why the frame is a rule
/// rather than a convention.
///
/// Get this wrong and the generator in Step 6 becomes a table of special
/// cases about which rooms fit which, which is the version of this that never
/// finishes.
/// ====================================================================
///
/// Phase 5, Step 5. See PHASE5_SPEC.md.
/// </summary>
public class RoomExit : MonoBehaviour
{
    [Tooltip("Blank normally. A name only matters when a module has more than " +
             "one exit and the generator is told which is which - the main " +
             "room's 'side' and 'back' doors, for instance.")]
    public string label = "";

    [Tooltip("False while the generator has not attached anything here. A " +
             "module must be walkable with every exit sealed, exactly as it " +
             "must be walkable with every socket empty - the last room on a " +
             "floor has unused doors and cannot become a trap because of it.")]
    public bool used;

    /// <summary>Where the next module's origin goes, and how it is turned.</summary>
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

    /// <summary>Every exit under a transform, module or not. Empty, never
    /// null - same reason as RoomModule.SocketsUnder.</summary>
    public static RoomExit[] ExitsUnder(Transform root) =>
        root == null ? new RoomExit[0]
                     : root.GetComponentsInChildren<RoomExit>(false);

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = used ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.7f);

        // The doorway it represents - 2 x 2.5, the footprint Grayboxbuilder
        // already fixed - drawn so a misplaced exit is visible rather than
        // discovered when two rooms overlap.
        Matrix4x4 was = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, 1.25f, 0f), new Vector3(0.1f, 2.5f, 2f));
        Gizmos.DrawRay(Vector3.zero, Vector3.right * 1.2f);
        Gizmos.matrix = was;
    }
#endif
}
