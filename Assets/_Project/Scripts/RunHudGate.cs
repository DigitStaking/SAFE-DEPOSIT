// RunHudGate.cs - hide gameplay OnGUI when results/shop is up
using UnityEngine;

public static class RunHudGate
{
    public static bool ShouldDrawGameplayHud()
    {
        // THE LOBBY OWNS THE SCREEN. Every HUD element in the game was
        // drawing itself over the top of it - the quota, the load gauge, the
        // pack slots, the elevator readout - which is why it read as a
        // transparent mess rather than as a menu.
        if (CrewLobby.PanelUp) return false;

        var run = SceneRefs.Run;
        if (run == null) return true;
        return run.IsRunActive;
    }
}
