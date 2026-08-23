// SceneRefs.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/SceneRefs.cs
// Goes on: nothing. Static.
//
// ====================================================================
// PHASE 3 STEP 1 - THE THINGS THERE IS GENUINELY ONE OF.
//
// PHASE3_SPEC's survey found 18 scene searches and sorted them into three
// piles. Five hunt for a player and are wrong - PlayerRegistry answers those.
// Six are collection sweeps and are correct by nature.
//
// The remaining nine look for a RunManager, an Elevator, a SceneAtmosphere or
// a RealisticSmokeVolume, and those are NOT a design problem. There is one
// lift. There is one run. Turning them into per-player anything would be
// solving a problem the game does not have.
//
// What they are is SLOW, and one of them is embarrassing:
//
//   RunHudGate.ShouldDrawGameplayHud() calls FindFirstObjectByType<RunManager>
//   and is called from the top of all NINE gameplay OnGUI methods. OnGUI runs
//   at least twice a frame - once for Layout, once for Repaint - so that is
//   eighteen full scene searches per frame to answer a question whose value
//   changes about twice per run.
//
// ====================================================================
// WHY THE null CHECK IS THE WHOLE TRICK
//
// `cached != null` is not redundant with `cached == null`. Unity overloads
// those operators: a destroyed object compares equal to null while the C#
// reference is still perfectly non-null. RunManager.ReloadScene destroys the
// entire scene between rounds, so every cached reference here becomes one of
// those husks - and the overload is exactly what turns that into "go and find
// the new one" instead of a MissingReferenceException on the first access of
// round two.
//
// So there is nothing to invalidate by hand, no scene-load callback to
// register, and no way to forget one.
// ====================================================================

using UnityEngine;

public static class SceneRefs
{
    static RunManager run;
    static Elevator lift;
    static SceneAtmosphere atmosphere;

    public static RunManager Run =>
        run != null ? run : (run = Object.FindFirstObjectByType<RunManager>());

    public static Elevator Lift =>
        lift != null ? lift : (lift = Object.FindFirstObjectByType<Elevator>());

    public static SceneAtmosphere Atmosphere =>
        atmosphere != null
            ? atmosphere
            : (atmosphere = Object.FindFirstObjectByType<SceneAtmosphere>());
}
