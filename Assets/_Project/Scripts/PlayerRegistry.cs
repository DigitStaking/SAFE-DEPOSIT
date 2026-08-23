// PlayerRegistry.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerRegistry.cs
// Goes on: nothing. Static. PlayerMotor registers itself.
//
// ====================================================================
// PHASE 3 STEP 1 - WHO ARE THE PLAYERS?
//
// Five places in the codebase answer that question with
// FindFirstObjectByType, which means "whichever one Unity happened to return
// first". With one player that is always the right answer and never a lie.
// With two it is a coin flip, and the bug it produces is the worst kind: it
// works, most of the time, and picks the wrong person occasionally.
//
// So the question gets one answer, and the players supply it themselves.
//
// ====================================================================
// WHY PLAYERS REGISTER THEMSELVES RATHER THAN BEING FOUND
//
// A scan is a guess about the present. A registration is a fact recorded by
// the only object that knows it - and it is recorded at exactly the moment it
// becomes true, in OnEnable, rather than whenever somebody thought to look.
//
// It also survives the thing that breaks every other approach here:
// RunManager.ReloadScene destroys the whole scene between rounds. A cached
// list would hold Unity's fake-null husks of the old players forever. OnDisable
// fires on destruction, so the list empties itself as the scene comes down
// and refills as the new one comes up, with nothing to invalidate by hand.
//
// ====================================================================
// KEYED ON PlayerMotor, NOT ON A NEW "Player" COMPONENT
//
// Every player has one, and RunManager already types its crew list as
// PlayerMotor for exactly this reason - the only things it needs are the
// transform and the name. Inventing a marker component would mean two things
// to keep attached instead of one, and the fixer already attaches five.
// ====================================================================

using System.Collections.Generic;
using UnityEngine;

public static class PlayerRegistry
{
    static readonly List<PlayerMotor> all = new List<PlayerMotor>();

    /// <summary>Everyone currently in the scene, in registration order.</summary>
    public static IReadOnlyList<PlayerMotor> All => all;

    public static int Count => all.Count;

    /// <summary>
    /// The player at this keyboard.
    ///
    /// PROVISIONAL: it is whoever registered first, which is correct in solo
    /// and meaningless with two bodies. Step 2 adds a real IsLocal flag and
    /// this reads it instead. Everything that wants "me" should already be
    /// asking here rather than scanning, so Step 2 becomes a change to one
    /// property instead of a hunt through five files.
    /// </summary>
    public static PlayerMotor Local => all.Count > 0 ? all[0] : null;

    public static void Register(PlayerMotor p)
    {
        if (p == null || all.Contains(p)) return;
        all.Add(p);
    }

    public static void Unregister(PlayerMotor p)
    {
        if (p == null) return;
        all.Remove(p);
    }

    /// <summary>
    /// The component of type T belonging to the local player, or null.
    /// Saves every caller writing PlayerRegistry.Local?.GetComponent&lt;T&gt;()
    /// and getting the null check subtly wrong.
    /// </summary>
    public static T LocalComponent<T>() where T : Component
    {
        var p = Local;
        return p != null ? p.GetComponent<T>() : null;
    }

    /// <summary>
    /// The first player anywhere that has a T. For things that genuinely want
    /// "somebody, anybody" rather than "me" - and every one of those is a
    /// single-player shortcut that Phase 4 will have to look at again, so
    /// they are easier to find spelled like this than as a raw scan.
    /// </summary>
    public static T AnyComponent<T>() where T : Component
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] == null) continue;
            var c = all[i].GetComponent<T>();
            if (c != null) return c;
        }
        return null;
    }
}
