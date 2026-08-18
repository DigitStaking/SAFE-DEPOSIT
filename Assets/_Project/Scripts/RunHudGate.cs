// RunHudGate.cs - hide gameplay OnGUI when results/shop is up
using UnityEngine;

public static class RunHudGate
{
    public static bool ShouldDrawGameplayHud()
    {
        var run = Object.FindFirstObjectByType<RunManager>();
        if (run == null) return true;
        return run.IsRunActive;
    }
}
