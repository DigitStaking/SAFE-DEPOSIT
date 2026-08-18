// AnimationSetupTool.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/AnimationSetupTool.cs
//
// Menu:  SAFE DEPOSIT -> Animation -> Fix Everything
//
// ========================================================================
// WHAT WAS BROKEN, AND WHY NOTHING ANIMATED
//
// 1. THE ANIMATOR CONTROLLER STATES WERE EMPTY.
//    Idle, Walk, CarryIdle and Climb all had m_Motion: {fileID: 0} - four
//    named boxes with no animation clip in any of them. The controller ran
//    perfectly and played nothing, which is why there was no error.
//
// 2. PARAMETER TYPO. The controller had "Climbring"; the driver script sets
//    "Climbing". Unity does not warn about a parameter that is set but never
//    read, so this fails in total silence.
//
// 3. THE ANIMATOR HAD NO CONTROLLER AND NO AVATAR. The prefab contains an
//    Animator component, but m_Controller and m_Avatar were both unset.
//
// 4. THE HUMANOID AVATAR WAS NEVER BUILT. Player.fbx has animationType 3
//    (Humanoid) but human: [] and skeleton: [] - the bone mapping is empty.
//    Without it, humanoid retargeting has nothing to retarget ONTO, so even
//    correctly imported clips cannot drive the rig.
//
// This tool fixes all four, in the order they depend on each other. The
// avatar has to exist before clips can copy it, and clips have to exist
// before the controller can reference them.
// ========================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimationSetupTool
{
    const string ModelDir      = "Assets/_Project/Models";
    const string PlayerFbx     = ModelDir + "/Player.fbx";
    const string ControllerDir = "Assets/_Project/Animation";
    const string ControllerPath= ControllerDir + "/AC_PlayerDiver.controller";
    const string PlayerPrefab  = "Assets/_Project/Prefabs/Player.prefab";

    // Which Mixamo clip drives which state. Matching is by substring, case
    // insensitive, so "Player@Happy Idle.fbx" satisfies "idle".
    static readonly (string state, string[] match)[] StateMap =
    {
        ("Idle",      new[]{ "happy idle", "idle" }),
        ("Walk",      new[]{ "walking", "walk" }),
        ("CarryIdle", new[]{ "box idle", "carry", "holding" }),
        ("Climb",     new[]{ "climbing a rope", "climb" }),
    };

    // SUPERSEDED by SAFE DEPOSIT -> Animation -> Build Full Animator
    // (AnimatorBuilder.cs). This builds the old single-layer, four-state
    // controller and will overwrite the good one. Kept only as a fallback.
    [MenuItem("SAFE DEPOSIT/Animation/Legacy - Simple 4-State Controller")]
    static void FixEverything()
    {
        if (!File.Exists(PlayerFbx))
        {
            Debug.LogError($"[Anim] {PlayerFbx} not found.");
            return;
        }

        // ---- 1. build the humanoid avatar on the character ---------------
        var playerImporter = (ModelImporter)AssetImporter.GetAtPath(PlayerFbx);
        playerImporter.animationType = ModelImporterAnimationType.Human;
        playerImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        // Off on the character itself - the clips live in the @ files. Leaving
        // it on makes Unity generate a junk "Take 001" clip that shows up in
        // the controller list and confuses everything.
        playerImporter.importAnimation = false;

        playerImporter.SaveAndReimport();

        var avatar = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx)
                                  .OfType<Avatar>().FirstOrDefault();

        if (avatar == null || !avatar.isValid)
        {
            Debug.LogError(
                "[Anim] Unity could not build a valid Humanoid avatar from Player.fbx.\n" +
                "Open the FBX -> Rig tab -> Configure and check which bones are missing. " +
                "Everything else is blocked until this works.");
            return;
        }
        Debug.Log($"[Anim] avatar OK: {avatar.name}  (isHuman={avatar.isHuman})");

        // ---- 2. point every animation FBX at that avatar -----------------
        //
        // PASS ONE: reimport. PASS TWO: collect the clips.
        //
        // Split deliberately. Every SaveAndReimport can invalidate object
        // references held across it - that is the MissingReferenceException:
        // an Avatar or AnimationClip captured before a reimport points at an
        // object Unity has since destroyed and rebuilt.
        //
        // So nothing is cached across a reimport. The avatar is re-fetched by
        // path inside the loop, and clips are gathered only once every import
        // has finished.
        var animPaths = Directory.GetFiles(ModelDir, "Player@*.fbx")
                                 .Select(x => x.Replace('\\', '/'))
                                 .ToArray();

        foreach (var p in animPaths)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(p);
            if (imp == null) continue;

            // Re-fetch, do not reuse. See above.
            var av = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx)
                                  .OfType<Avatar>().FirstOrDefault();
            if (av == null) { Debug.LogError("[Anim] avatar vanished mid-import."); return; }

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar = av;
            imp.importAnimation = true;

            // Bake root motion into the pose. The player is a Rigidbody driven
            // by PlayerMotor - if the clip also moves the root, the character
            // fights its own animation and slides around.
            var name = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            bool looping = name.Contains("idle") || name.Contains("walk")
                        || name.Contains("climb") || name.Contains("run");

            imp.clipAnimations = imp.defaultClipAnimations.Select(c =>
            {
                c.loopTime = looping;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = false;
                return c;
            }).ToArray();

            imp.SaveAndReimport();
        }

        // PASS TWO - now that every reimport is done, nothing else will
        // invalidate these references before we use them.
        AssetDatabase.Refresh();

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var p in animPaths)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(p)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview"));

            if (clip != null)
            {
                clips[Path.GetFileNameWithoutExtension(p).ToLowerInvariant()] = clip;
                Debug.Log($"[Anim] clip: {clip.name}   from {Path.GetFileName(p)}");
            }
            else
            {
                Debug.LogWarning($"[Anim] no clip inside {Path.GetFileName(p)}");
            }
        }

        if (clips.Count == 0)
        {
            Debug.LogError("[Anim] no animation clips found. Are the Player@*.fbx files present?");
            return;
        }

        // ---- 3. rebuild the controller from scratch ----------------------
        // Rebuilt rather than patched: the existing one has empty states and
        // a misspelled parameter, and repairing YAML by hand is how you get a
        // controller that looks right and behaves oddly.
        Directory.CreateDirectory(ControllerDir);
        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        ac.AddParameter("Speed",    AnimatorControllerParameterType.Float);
        ac.AddParameter("Moving",   AnimatorControllerParameterType.Bool);
        ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Climbing", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Carry",    AnimatorControllerParameterType.Int);

        var sm = ac.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();

        foreach (var (stateName, keys) in StateMap)
        {
            var clip = FindClip(clips, keys);
            var st = sm.AddState(stateName);
            st.motion = clip;
            states[stateName] = st;

            if (clip == null)
                Debug.LogWarning($"[Anim] state {stateName} has no matching clip - " +
                                 $"looked for: {string.Join(", ", keys)}");
        }

        sm.defaultState = states["Idle"];

        // Idle <-> Walk on Moving.
        Link(states["Idle"], states["Walk"], "Moving", true);
        Link(states["Walk"], states["Idle"], "Moving", false);

        // Carry overrides locomotion from anywhere. AnyState keeps this simple
        // while there are only four states; with more it would need a layer.
        var toCarry = sm.AddAnyStateTransition(states["CarryIdle"]);
        toCarry.AddCondition(AnimatorConditionMode.Greater, 0, "Carry");
        toCarry.duration = 0.15f;
        toCarry.canTransitionToSelf = false;

        var carryOut = states["CarryIdle"].AddTransition(states["Idle"]);
        carryOut.AddCondition(AnimatorConditionMode.Equals, 0, "Carry");
        carryOut.duration = 0.15f;

        // Climbing overrides everything.
        var toClimb = sm.AddAnyStateTransition(states["Climb"]);
        toClimb.AddCondition(AnimatorConditionMode.If, 0, "Climbing");
        toClimb.duration = 0.12f;
        toClimb.canTransitionToSelf = false;

        var climbOut = states["Climb"].AddTransition(states["Idle"]);
        climbOut.AddCondition(AnimatorConditionMode.IfNot, 0, "Climbing");
        climbOut.duration = 0.15f;

        EditorUtility.SetDirty(ac);

        // ---- 4. wire the prefab -----------------------------------------
        if (!File.Exists(PlayerPrefab))
        {
            Debug.LogWarning($"[Anim] {PlayerPrefab} not found - assign the controller by hand.");
        }
        else
        {
            // Re-fetch the avatar one final time. Same reason as before: the
            // reimports and the controller write both sit between here and
            // where it was first loaded.
            var av = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx)
                                  .OfType<Avatar>().FirstOrDefault();

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PlayerPrefab);

                var visual = FindDeep(root.transform, "PlayerModel_FBX_VISUAL");
                var target = visual != null ? visual.gameObject : root;

                var anim = target.GetComponent<Animator>();
                if (anim == null) anim = target.AddComponent<Animator>();

                anim.runtimeAnimatorController = ac;
                if (av != null) anim.avatar = av;

                // PlayerMotor owns movement. Root motion would fight it.
                anim.applyRootMotion = false;

                // Keep animating when the renderer is culled - first person
                // culls the local head, and without this the rig can freeze.
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                Debug.Log($"[Anim] prefab wired: Animator on '{target.name}'");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Anim] prefab step failed: {e.Message}\n" +
                               "Everything else succeeded - just drag AC_PlayerDiver onto the " +
                               "Animator on PlayerModel_FBX_VISUAL by hand.");
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Anim] DONE. {clips.Count} clips, 4 states, 5 parameters.");
    }

    static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, string[] keys)
    {
        foreach (var key in keys)
            foreach (var kv in clips)
                if (kv.Key.Contains(key)) return kv.Value;
        return null;
    }

    static void Link(AnimatorState from, AnimatorState to, string param, bool value)
    {
        var t = from.AddTransition(to);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
        t.duration = 0.12f;
        t.hasExitTime = false;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }
}
