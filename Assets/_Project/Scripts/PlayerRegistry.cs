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
    /// The player at this keyboard, or null if there is not one.
    ///
    /// Step 2 made this real: it reads PlayerMotor.IsLocal rather than
    /// returning whoever happened to register first. Everything that wanted
    /// "me" was already asking here after Step 1, so this was a change to one
    /// property instead of a hunt through six files - which was the point of
    /// doing them in that order.
    /// </summary>
    public static PlayerMotor Local
    {
        get
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsLocal) return all[i];
            return null;
        }
    }

    public static void Register(PlayerMotor p)
    {
        if (p == null || all.Contains(p)) return;

        // The slot is the index, so it is stable as long as bodies register
        // in the same order every round - which they do, because the scene
        // is rebuilt from the same prefabs in the same places.
        p.AssignSlot(all.Count);
        all.Add(p);

        // FIRST BODY IN CLAIMS LOCAL. Solo needs no setup, and a second
        // prefab dropped into the scene is automatically not local without
        // anybody remembering to tick anything - which is exactly the
        // condition Step 7's two-body test wants to create by accident.
        //
        // Phase 4 overrides this with MarkLocal when the network says who
        // owns whom.
        if (Local == null) p.MarkLocal(true);
    }

    public static void Unregister(PlayerMotor p)
    {
        if (p == null) return;
        all.Remove(p);
    }

    /// <summary>
    /// Is this component part of the local player's body?
    ///
    /// Walks up to the owning PlayerMotor, so it works from a script on the
    /// root, on the FBX child, or anywhere further down. Every "only do this
    /// for me" gate in the game is one call to this, which is what keeps the
    /// rule in one place instead of six slightly different reimplementations.
    ///
    /// Returns TRUE when there is no owner at all. A component that is not
    /// part of a player - a world HUD, the lift - should not be silenced by a
    /// player-ownership test it was never meant to answer.
    /// </summary>
    public static bool IsLocalFor(Component c)
    {
        if (c == null) return true;
        var owner = c.GetComponentInParent<PlayerMotor>();
        return owner == null || owner.IsLocal;
    }

    /// <summary>The PlayerMotor this component belongs to, or null.</summary>
    public static PlayerMotor OwnerOf(Component c) =>
        c != null ? c.GetComponentInParent<PlayerMotor>() : null;

    /// <summary>
    /// The eye transform of the player this component belongs to. Null if it
    /// belongs to nobody, or if no camera has claimed that body yet.
    /// </summary>
    public static Transform EyeOf(Component c)
    {
        var owner = OwnerOf(c);
        return owner != null ? owner.Eye : null;
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
