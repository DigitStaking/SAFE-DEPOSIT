// AnimationDiagnose.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/AnimationDiagnose.cs
//
// Menu:  SAFE DEPOSIT -> Animation -> Diagnose And Repair
//
// ========================================================================
// WHY THIS EXISTS
//
// Every file on disk checks out: the avatar has all 52 human bones mapped,
// the four Player@*.fbx files copy that avatar, the controller has a clip
// in every state, and Player.prefab records m_Controller.
//
// And yet the character stands in a T-pose.
//
// A T-pose is the SKIN BIND POSE. It is what you see when the skinned mesh
// is drawn but no animation is writing to the bones. There is no error for
// this, because from Unity's point of view nothing went wrong - it simply
// had nothing to apply.
//
// Exactly two things cause it once the controller is wired, and NEITHER is
// visible in a text file, because both live inside the FBX's generated
// prefab rather than in the .meta:
//
//   A. The Animator's AVATAR is None.
//      Humanoid clips are not stored as "rotate mixamorig:LeftArm". They
//      are stored in a normalised, rig-independent MUSCLE SPACE. The Avatar
//      is the dictionary that converts muscle space back into your specific
//      bones. Controller with no Avatar = a translation with no dictionary.
//      It runs, it produces nothing, it does not complain.
//
//   B. The clips did not import as HUMAN motion.
//      If the animation FBX fell back to Generic, the clip holds raw bone
//      curves for a skeleton the Avatar does not describe, so retargeting
//      refuses. Same silent T-pose.
//
// This tool reads both, prints them, and repairs A in place.
// ========================================================================

using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AnimationDiagnose
{
    const string ModelDir     = "Assets/_Project/Models";
    const string PlayerFbx    = ModelDir + "/Player.fbx";
    const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";

    [MenuItem("SAFE DEPOSIT/Animation/Diagnose And Repair")]
    static void Run()
    {
        Debug.Log("<b>================ ANIMATION DIAGNOSE ================</b>");

        // ---- 1. the avatar asset itself --------------------------------
        var avatar = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx)
                                  .OfType<Avatar>().FirstOrDefault();

        if (avatar == null)
        {
            Debug.LogError(
                "<b>[1] FAIL</b> - Player.fbx contains no Avatar sub-asset.\n" +
                "Nothing downstream can work. Select Player.fbx -> Rig -> " +
                "Animation Type: Humanoid -> Apply.");
            return;
        }

        Debug.Log($"<b>[1] avatar asset</b>  name={avatar.name}  " +
                  $"isHuman={avatar.isHuman}  isValid={avatar.isValid}");

        if (!avatar.isValid || !avatar.isHuman)
        {
            Debug.LogError("<b>[1] FAIL</b> - avatar exists but is not a valid humanoid. " +
                           "Open Player.fbx -> Rig -> Configure and fix the red bones.");
            return;
        }

        // ---- 2. are the clips human motion? ----------------------------
        //
        // clip.isHumanMotion is the ground truth. The .meta can say
        // animationType: 3 and still produce a generic clip if the import
        // fell back - this is the only way to know.
        int humanClips = 0, genericClips = 0;

        foreach (var p in Directory.GetFiles(ModelDir, "Player@*.fbx")
                                   .Select(x => x.Replace('\\', '/')))
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(p)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => c != null &&
                                                         !c.name.StartsWith("__preview"));

            if (clip == null)
            {
                Debug.LogError($"<b>[2] FAIL</b> - no clip inside {Path.GetFileName(p)}");
                genericClips++;
                continue;
            }

            bool human = clip.isHumanMotion;
            if (human) humanClips++; else genericClips++;

            string verdict = human ? "HUMAN (good)" : "<b>GENERIC (BROKEN)</b>";
            Debug.Log($"<b>[2] clip</b> {clip.name,-18} len={clip.length:0.00}s  " +
                      $"fps={clip.frameRate:0}  loop={clip.isLooping}  {verdict}",
                      clip);

            if (clip.length < 0.01f)
                Debug.LogError($"<b>[2] FAIL</b> - {clip.name} has zero length. " +
                               "The FBX has no keyframes - re-download it from Mixamo.");
        }

        Debug.Log($"<b>[2] summary</b>  {humanClips} human, {genericClips} generic/missing");

        if (humanClips == 0)
        {
            Debug.LogError(
                "<b>[2] FAIL</b> - not one clip is human motion. Every animation FBX " +
                "needs Rig -> Animation Type: Humanoid, Avatar Definition: Copy From " +
                "Other Avatar, Source: the Player avatar.");
        }

        // ---- 3. the prefab's Animator ----------------------------------
        //
        // This is the value that is invisible on disk. m_Avatar only appears
        // in Player.prefab as a modification if it DIFFERS from the FBX's
        // own Animator - so its absence tells you nothing either way.
        GameObject root = null;
        bool repaired = false;

        try
        {
            root = PrefabUtility.LoadPrefabContents(PlayerPrefab);

            var anim = root.GetComponentInChildren<Animator>(true);
            if (anim == null)
            {
                Debug.LogError("<b>[3] FAIL</b> - no Animator anywhere in Player.prefab.");
                return;
            }

            Debug.Log($"<b>[3] Animator</b> on '{anim.gameObject.name}'\n" +
                      $"      enabled     = {anim.enabled}\n" +
                      $"      gameObject  = {(anim.gameObject.activeSelf ? "active" : "<b>INACTIVE</b>")}\n" +
                      $"      controller  = {(anim.runtimeAnimatorController == null ? "<b>NONE</b>" : anim.runtimeAnimatorController.name)}\n" +
                      $"      avatar      = {(anim.avatar == null ? "<b>NONE  <- this is the bug</b>" : anim.avatar.name)}\n" +
                      $"      rootMotion  = {anim.applyRootMotion}\n" +
                      $"      culling     = {anim.cullingMode}");

            // --- repair ---
            if (anim.avatar == null || anim.avatar != avatar)
            {
                anim.avatar = avatar;
                repaired = true;
                Debug.Log("<b>[3] REPAIRED</b> - assigned the humanoid avatar to the Animator.");
            }

            if (!anim.enabled)
            {
                anim.enabled = true;
                repaired = true;
                Debug.Log("<b>[3] REPAIRED</b> - the Animator was disabled. Enabled it.");
            }

            if (anim.applyRootMotion)
            {
                anim.applyRootMotion = false;
                repaired = true;
                Debug.Log("<b>[3] REPAIRED</b> - turned off root motion (PlayerMotor owns movement).");
            }

            if (anim.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                repaired = true;
            }

            // ---- 4. do the bones actually live under the Animator? -----
            //
            // The Animator drives its own children. If the SkinnedMeshRenderer's
            // bones sit somewhere else in the hierarchy, the Animator animates a
            // skeleton nobody is skinned to - and you get a T-pose that also
            // never moves no matter what you fix above.
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null)
            {
                Debug.LogWarning("<b>[4]</b> no SkinnedMeshRenderer found.");
            }
            else
            {
                bool rootBoneUnder = smr.rootBone != null &&
                                     smr.rootBone.IsChildOf(anim.transform);

                Debug.Log($"<b>[4] skin</b>  renderer='{smr.name}'  bones={smr.bones.Length}\n" +
                          $"      rootBone = {(smr.rootBone == null ? "<b>NULL</b>" : smr.rootBone.name)}\n" +
                          $"      under Animator = {(rootBoneUnder ? "yes (good)" : "<b>NO - skeleton is detached</b>")}",
                          smr);

                if (smr.bones.Length == 0)
                    Debug.LogError("<b>[4] FAIL</b> - the mesh has no bones bound. " +
                                   "The FBX was exported without skin weights.");
            }

            if (repaired)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                Debug.Log("<b>[3] prefab saved.</b>");
            }
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();

        Debug.Log(repaired
            ? "<b>=========== DONE - something was repaired. Press Play. ===========</b>"
            : "<b>=========== DONE - nothing needed repair. Read [2] and [4] above. ===========</b>");
    }
}
