// SteamBuildStep.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/SteamBuildStep.cs
//
// ====================================================================
// PUT steam_appid.txt NEXT TO THE EXE, EVERY BUILD, WITHOUT BEING ASKED.
//
// Steam will not initialise for an unpublished game unless it can find a
// steam_appid.txt beside the executable. The package writes one into the
// PROJECT ROOT on install, which is what makes the editor work - and a build
// is a different folder, so it gets nothing.
//
// The symptom is perfectly misleading: the editor says
//
//     [Steam] running as Digitstak (76561199554308332)
//
// and the build, at the same moment, on the same machine, with Steam plainly
// open, says
//
//     [Steam] not initialised - is Steam running?
//
// Which is a question with an obvious and wrong answer. Steam was running.
// The build simply had no idea which game it was.
//
// A MANUAL STEP WOULD HAVE BEEN FORGOTTEN, and specifically it would have been
// forgotten on the build handed to a friend - the one where nobody is around
// to read a log. So it is a build step. Anything a person has to remember for
// a shipped artefact belongs in the tool that makes the artefact.
// ====================================================================

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class SteamBuildStep : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string exe = report.summary.outputPath;
        if (string.IsNullOrEmpty(exe)) return;

        string folder = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(folder)) return;

        // Read the project's own file rather than hardcoding 480, so the day
        // this game has a real app id the build follows automatically and
        // nobody has to remember there are two copies of the number.
        string source = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");

        string appId = File.Exists(source)
            ? File.ReadAllText(source).Trim()
            : "480";

        string target = Path.Combine(folder, "steam_appid.txt");

        try
        {
            // ASCII with no BOM, no trailing newline. Steam's parser is
            // fussier than it looks and a BOM makes the id unreadable, which
            // fails exactly like the file being absent.
            File.WriteAllText(target, appId, new System.Text.UTF8Encoding(false));

            Debug.Log($"[Steam] wrote steam_appid.txt ({appId}) beside the build. " +
                      "Steam will recognise it on this machine and on anybody " +
                      "else's.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Steam] could not write steam_appid.txt beside the " +
                             "build - Steam features will be off in it.\n" + e.Message);
        }
    }
}
