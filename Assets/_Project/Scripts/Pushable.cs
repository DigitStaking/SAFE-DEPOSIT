// Pushable.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Pushable.cs
// Goes on: anything that should be shoved with a particular gesture.
//
// ========================================================================
// THIS OBJECT -> THAT PUSH PROFILE.
//
// "when the player interacts with that object, the game should know: This
//  object -> Heavy Door Push Profile -> load these hand settings -> play push"
//
// This is the arrow in the middle. One field.
//
// ---- OPTIONAL, ON PURPOSE ----
//
// Nothing has to carry this. An object with no Pushable is shoved with the
// default profile, exactly as everything is shoved today, and adding one is
// how you say "this one is different".
//
// That mirrors the decision Auto mode made for grips, and it is the same
// reasoning: a system where every object must be registered before it works is
// a system where the twentieth prop somebody drops in is silently broken, and
// nobody finds out until a playtest. Make the common case need no setup.
//
// ---- LOOKED UP AT THE MOMENT OF THE SHOVE, NOT CACHED ----
//
// PlayerPush already resolves its target by spherecast at contact time rather
// than at keypress, so somebody who steps aside during the wind-up gets away
// with it. The profile is read from whatever that cast actually hit, at the
// moment the swing starts - so pushing a door and pushing a crate two seconds
// apart use different gestures without anything having to be told about the
// change.
// ========================================================================

using UnityEngine;

public class Pushable : MonoBehaviour
{
    [Tooltip("Can this be shoved at all. " +
             "An explicit answer here always wins over the default rule, in " +
             "either direction - tick it on a crate you DO want shovable, " +
             "untick it on a door that should not budge.")]
    public bool canBePushed = true;

    [Tooltip("How this object is shoved. Empty means the default profile - " +
             "which is a perfectly good answer for most things, and the reason " +
             "this component is optional rather than required.")]
    public PushProfile profile;

    [Tooltip("Say in the log which profile was used when this is shoved. For " +
             "when a gesture looks wrong and you want to know which asset " +
             "produced it rather than guessing.")]
    public bool logOnPush = false;

    /// <summary>
    /// Whether this thing may be shoved.
    ///
    /// PEOPLE YES, LOOT NO, and everything else yes. A crate you can pick up
    /// is something you CARRY - shoving the quota across the floor was never
    /// the verb, and a crew that can scatter its own haul by walking into it
    /// is a crew that will.
    ///
    /// A downed crewmate is a Carryable too, and is still a person, so they
    /// stay pushable - the rule asks what something IS, not which components
    /// happen to be on it.
    ///
    /// A Pushable with an explicit answer overrides all of that, both ways.
    /// </summary>
    public static bool Allows(Component hit)
    {
        if (hit == null) return false;

        var p = hit.GetComponentInParent<Pushable>();
        if (p != null) return p.canBePushed;

        var carry = hit.GetComponentInParent<Carryable>();
        return carry == null || carry.IsPerson;
    }

    /// <summary>
    /// The profile for whatever was hit, or null to mean "use the default".
    ///
    /// Searched UPWARD from the collider, because a spherecast hits a child
    /// collider on a door leaf while the component that knows what kind of
    /// door it is sits on the root. Looking only at the hit object is the
    /// classic version of this that works in testing and fails on the first
    /// prop with a nested collider.
    /// </summary>
    public static PushProfile For(Component hit)
    {
        if (hit == null) return null;

        var p = hit.GetComponentInParent<Pushable>();
        if (p == null || p.profile == null) return null;

        if (p.logOnPush)
            Debug.Log("[Push] " + p.name + " -> profile '" +
                      p.profile.displayName + "' (" + p.profile.name + ")");

        return p.profile;
    }
}
