using UnityEngine;

/// <summary>
/// Marks a carryable object as the key to one door.
///
/// ====================================================================
/// A KEY IS A CARRYABLE, AND THAT IS THE WHOLE DESIGN
///
/// It is not an inventory flag. It is an object with a mass, sitting on a
/// floor, that somebody has to pick up and carry in their hands.
///
/// Everything interesting falls out of that for free, because Steps 1 and 2
/// already built it:
///
///   - it occupies a hand, so carrying the key means not carrying loot
///   - it can be PUT DOWN (tap Q) and forgotten somewhere
///   - it can be THROWN (hold Q) to a crewmate across a room
///   - it can be dropped when you go down, and lost with you
///
/// "Throw the key to the person nearer the door" is not a feature anybody
/// implemented. It is what happens when a key is a thing rather than a flag.
///
/// Small on purpose: Carryable's weight classes make anything at or under 8kg
/// one-handed, jumpable and stowable in the backpack, which is the right
/// shape for a key. See docs/GAME_DESIGN.md.
/// ====================================================================
///
/// Phase 5, Step 7. See PHASE5_SPEC.md.
/// </summary>
[RequireComponent(typeof(Carryable))]
public class DoorKey : MonoBehaviour
{
    [Tooltip("Matched by string against RoomDoor.keyId. The generator writes " +
             "both ends, so a floor can hold several locked doors without one " +
             "key opening all of them.")]
    public string keyId = "";

    /// <summary>
    /// The key somebody is holding, or null.
    ///
    /// Asked of the carry system rather than tracked separately - a second
    /// record of "who has the key" is a second thing that can be wrong, and
    /// PlayerCarry already knows.
    /// </summary>
    public static DoorKey HeldBy(PlayerCarry hands)
    {
        if (hands == null || hands.Held == null) return null;

        return hands.Held.GetComponent<DoorKey>();
    }
}
