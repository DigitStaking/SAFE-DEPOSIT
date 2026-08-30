// SteamBoot.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/SteamBoot.cs
// Goes on: nothing. Starts itself before the first scene loads.
//
// ====================================================================
// PHASE 4 STEP 11 - TURNING STEAM ON.
//
// Everything Steam does for this game - your friends joining without an IP
// address, the relay that means there is no server to rent, and the voice
// codec - needs exactly one thing first: SteamAPI.Init succeeding.
//
// IT RUNS BEFORE THE SCENE, AND IT NEVER STOPS THE GAME
//
// Steam not running is not an error here. It is Tuesday: the editor is often
// open without it, and a solo player testing a build should not be blocked by
// a login. So a failed Init is reported once, plainly, and the game continues
// on Unity Transport exactly as it has all phase.
//
// That is the same promise every step of this phase has kept - press Play and
// do nothing, and the solo game works - extended to the one dependency that
// is genuinely outside the project.
//
// THE APP ID IS 480 AND THAT IS DELIBERATE
//
// Valve's public test app. It is how you develop before you have your own,
// and it is what lets a friend connect over Steam's relay today rather than
// after a store page exists. steam_appid.txt at the project root says so; the
// package wrote it on install.
//
// One caveat worth knowing before it wastes an afternoon: 480 is shared by
// every developer doing this, so Steam's lobby LIST for it is full of
// strangers. Joining by FRIEND is unaffected and is what this game uses.
// ====================================================================

using UnityEngine;
using Steamworks;

public static class SteamBoot
{
    public static bool Running { get; private set; }

    /// <summary>Your own Steam id, or 0 when Steam is not running.</summary>
    public static ulong MySteamId => Running ? (ulong)SteamUser.GetSteamID() : 0UL;

    public static string MyName => Running ? SteamFriends.GetPersonaName() : "offline";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        // Packsize/interop mismatches are the classic cause of a silent crash
        // rather than a clean failure, and the package ships a check for it.
        if (!Packsize.Test())
        {
            Debug.LogError("[Steam] Packsize test failed - the native library " +
                           "does not match this platform. Steam features off.");
            return;
        }

        try
        {
            Running = SteamAPI.Init();
        }
        catch (System.DllNotFoundException e)
        {
            Debug.LogError("[Steam] steam_api64.dll could not be loaded. " +
                           "Steam features off, game continues.\n" + e.Message);
            return;
        }

        if (!Running)
        {
            // NOT an error. The likeliest cause by far is "Steam is not open",
            // and the second likeliest is "this is the editor and I have not
            // restarted it since steam_appid.txt appeared".
            // NAMING BOTH CAUSES, because the first version asked "is Steam
            // running?" to somebody whose Steam was plainly running - and the
            // real cause was that this BUILD had no steam_appid.txt beside the
            // exe, so Steam had no idea which game was asking.
            //
            // A diagnostic that suggests the wrong cause is worse than one
            // that suggests none: it sends somebody to check the thing that
            // was never wrong.
            bool haveAppId = System.IO.File.Exists(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.dataPath) ?? "",
                    "steam_appid.txt"));

            Debug.LogWarning("[Steam] not initialised. Two things it needs, and " +
                             "one of them is usually the answer.  " +
                             "1: Steam open and logged in.  " +
                             "2: steam_appid.txt beside the executable - " +
                             (haveAppId
                                 ? "FOUND, so it is probably the first one."
                                 : "NOT FOUND, and that is almost certainly it.") +
                             "  Playing on Unity Transport instead, which is " +
                             "local-only. Everything else works normally.");
            return;
        }

        var pump = new GameObject("~SteamCallbacks");
        pump.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(pump);
        pump.AddComponent<Pump>();

        Debug.Log($"[Steam] running as {MyName} ({MySteamId}). " +
                  "Friends can join without an IP address.");
    }

    /// <summary>
    /// Steam hands back its answers on a callback queue that something has to
    /// drain. Miss this and lobby invites, join requests and voice all simply
    /// never arrive - with no error, because nothing went wrong, nobody just
    /// ever asked.
    ///
    /// DontDestroyOnLoad because the round change reloads the scene, and a
    /// pump that dies between rounds takes every Steam callback with it.
    /// </summary>
    class Pump : MonoBehaviour
    {
        void Update()
        {
            if (Running) SteamAPI.RunCallbacks();
        }

        void OnApplicationQuit()
        {
            if (!Running) return;

            Running = false;
            SteamAPI.Shutdown();
        }
    }
}
