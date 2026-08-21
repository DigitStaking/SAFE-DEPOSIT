// FirstPersonFixer.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/FirstPersonFixer.cs
//
// Menu:  SAFE DEPOSIT -> Fix First Person Setup
//
// ========================================================================
// ONE BUTTON THAT PUTS THE PLAYER BACK IN THE KNOWN-GOOD STATE.
//
// We spent a long session tuning numbers by hand in the Inspector, and that
// is exactly where this kind of setup goes wrong: values typed into a scene
// instance are not the values in the prefab, a component dragged onto the
// wrong GameObject silently does nothing, and nobody can remember which of
// fifteen fields were changed.
//
// So the good configuration is written down HERE, in code, as the single
// source of truth. Run this and every one of those fields is set, on both
// the prefab asset AND the instance sitting in the open scene, in that
// order. Run it again any time something drifts.
//
// It is safe to run repeatedly and it does not touch gameplay tuning -
// tether length, arm poses, motor speeds and so on are all left alone.
// ========================================================================

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FirstPersonFixer
{
    const string PlayerPrefab   = "Assets/_Project/Prefabs/Player.prefab";
    const string ControllerPath = "Assets/_Project/Animation/AC_PlayerDiver.controller";
    const string PlayerFbx      = "Assets/_Project/Models/Player.fbx";

    // ---- THE KNOWN-GOOD NUMBERS -------------------------------------
    // EYE HEIGHT IS THE USER'S NUMBER. 1.25, not 1.65.
    //
    // This file exists to stop hand-tuned values drifting, and it did the
    // opposite here: it carried a 1.65 that was never asked for, so running
    // "Fix First Person Setup" to attach an unrelated component silently
    // raised the camera 40cm on both the prefab and the open scene. A tool
    // that resets everything must only hold values somebody actually chose.
    static readonly Vector3 EyeOffset = new Vector3( 0f, 1.25f, 0.12f);

    // HANDS ARE ANCHORED IN WORLD HEIGHT, NOT TO THE EYE.
    //
    // FirstPersonHands places its IK targets RELATIVE TO THE EYE, so a fixed
    // y offset moves the hands whenever the camera moves - which is how a
    // camera change mangled the arms. The hands do not care where the eye is;
    // they care where the SHOULDER is, and the shoulder is on the body at
    // about 1.42 no matter what the camera does.
    //
    // So the tuned quantity is the world height (1.35, ~7cm below the
    // shoulder, which is what makes the elbows hang instead of flaring), and
    // the eye-relative offset is DERIVED from it. Change EyeOffset.y now and
    // the hands stay exactly where they were.
    //
    // This is not a re-tune of the arms - they are still the Phase 1 interim
    // and still parked until Block 8. It restores the height they were tuned
    // at and stops the camera dragging them off it.
    const float HandWorldY = 1.35f;
    static float HandY => HandWorldY - EyeOffset.y;

    static Vector3 LeftHand  => new Vector3(-0.24f, HandY, 0.52f);
    static Vector3 RightHand => new Vector3( 0.24f, HandY, 0.52f);
    const float BaseFov  = 75f;
    const float NearClip = 0.3f;   // 0.05 exposes the inside of your own skull

    [MenuItem("SAFE DEPOSIT/Fix First Person Setup")]
    static void Fix()
    {
        var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (ac == null)
        {
            Debug.LogError($"[FPFix] {ControllerPath} not found. " +
                           "Run SAFE DEPOSIT -> Animation -> Build Full Animator first.");
            return;
        }

        var avatar = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx).OfType<Avatar>().FirstOrDefault();
        var log = new System.Text.StringBuilder();

        // ---- 1. the prefab asset (so it persists) --------------------
        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            log.AppendLine("<b>PREFAB</b>");
            Configure(contents, ac, avatar, log);
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FPFix] prefab step failed: {e}");
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
        }

        // ---- 2. every Player in the open scene (so Play works now) ---
        //
        // Done separately and not by reverting overrides: reverting would also
        // wipe the tether length, arm poses and everything else deliberately
        // tuned on the instance.
        var motors = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
        foreach (var m in motors)
        {
            log.AppendLine($"<b>SCENE</b> '{m.gameObject.name}'");
            Configure(m.gameObject, ac, avatar, log);
            EditorUtility.SetDirty(m.gameObject);
        }

        // ---- 3. the camera ------------------------------------------
        foreach (var fp in Object.FindObjectsByType<FirstPersonCamera>(FindObjectsSortMode.None))
        {
            fp.eyeOffset = EyeOffset;
            fp.baseFov   = BaseFov;

            var c = fp.GetComponent<Camera>();
            if (c != null)
            {
                c.nearClipPlane = NearClip;
                c.fieldOfView   = BaseFov;
                EditorUtility.SetDirty(c);
            }

            EditorUtility.SetDirty(fp);
            log.AppendLine($"<b>CAMERA</b> '{fp.name}' eyeOffset {EyeOffset}, " +
                           $"fov {BaseFov}, near {NearClip}");
        }

        if (motors.Length > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();

        Debug.Log("<b>[FPFix] DONE</b>\n" + log +
                  "\nSet the Game view to 16:9 and Scale 1x - that part cannot be done from script.");
    }

    static void Configure(GameObject root, RuntimeAnimatorController ac,
                          Avatar avatar, System.Text.StringBuilder log)
    {
        // ---- strip anything sitting on the wrong GameObject ----------
        //
        // FirstPersonHands has [RequireComponent(typeof(Animator))]. Drop it on
        // the Player root and Unity quietly adds a SECOND, empty Animator
        // there. That one has no avatar, so the script bails on its first line
        // and the IK never runs - while looking perfectly wired in the
        // Inspector. Remove the script first, or the Animator refuses to go.
        var strayHands = root.GetComponent<FirstPersonHands>();
        if (strayHands != null)
        {
            Object.DestroyImmediate(strayHands, true);
            log.AppendLine("  removed FirstPersonHands from the Player root (wrong object)");
        }

        var strayAnim = root.GetComponent<Animator>();
        if (strayAnim != null)
        {
            Object.DestroyImmediate(strayAnim, true);
            log.AppendLine("  removed the stray empty Animator from the Player root");
        }

        // ---- the real Animator, on the model -------------------------
        var anim = root.GetComponentInChildren<Animator>(true);
        if (anim == null)
        {
            log.AppendLine("  <color=#ff8855>NO ANIMATOR FOUND - is PlayerModel_FBX_VISUAL present?</color>");
            return;
        }

        anim.runtimeAnimatorController = ac;
        if (avatar != null) anim.avatar = avatar;
        anim.applyRootMotion = false;                          // PlayerMotor owns movement
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;  // your own body is often off-screen
        anim.enabled = true;
        log.AppendLine($"  Animator on '{anim.gameObject.name}': controller, avatar, " +
                       "rootMotion off, AlwaysAnimate");

        // ---- hand IK, on the SAME object as the Animator -------------
        var hands = anim.GetComponent<FirstPersonHands>();
        if (hands == null) hands = anim.gameObject.AddComponent<FirstPersonHands>();

        hands.enabled               = true;
        hands.leftHand              = LeftHand;
        hands.rightHand             = RightHand;
        hands.handWeight            = 1f;
        hands.keepInFrame           = true;
        hands.frameMargin           = 0.06f;
        hands.freeArmsDuringActions = false;
        hands.rotationWeight        = 0.7f;
        hands.followSpeed           = 16f;
        hands.weightSpeed           = 6f;
        hands.cameraTransform       = null;                    // resolves to Camera.main at runtime
        log.AppendLine($"  FirstPersonHands on '{anim.gameObject.name}': {LeftHand} / {RightHand}");

        // ---- head cull ----------------------------------------------
        var cull = root.GetComponent<LocalFirstPersonBodyCull>();
        if (cull == null) cull = root.AddComponent<LocalFirstPersonBodyCull>();

        cull.enabled        = true;
        cull.headShrink     = 0.0001f;
        cull.shrinkNeck     = false;   // floating collar, buys nothing
        cull.hideLegs       = false;   // torso floating in mid-air, alarming
        cull.hideOwnShadow  = true;    // kills the headlamp shadow blob
        log.AppendLine("  LocalFirstPersonBodyCull: head only, shadow off");

        // One shadow system, not two. LocalPlayerNoShadow and the cull both
        // capture and restore shadowCastingMode, and whichever runs second
        // captures a value the first one already changed.
        var noShadow = root.GetComponent<LocalPlayerNoShadow>();
        if (noShadow != null)
        {
            Object.DestroyImmediate(noShadow, true);
            log.AppendLine("  removed LocalPlayerNoShadow (the cull handles shadow)");
        }

        // ---- retire the placeholder capsule arms ---------------------
        // PlayerArms itself is gone; the leftover objects may still be in
        // older prefabs, so keep hiding them.
        foreach (var n in new[] { "Arm_L", "Arm_R" })
        {
            var t = FindDeep(root.transform, n);
            if (t != null && t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(false);
                log.AppendLine($"  hid placeholder {n}");
            }
        }

        // ---- health (Phase 2 Step 2) --------------------------------
        // Attached here rather than by hand for the reason at the top of this
        // file: a component dragged onto the scene instance only is a
        // component that vanishes the next time the prefab is applied.
        var health = root.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = root.AddComponent<PlayerHealth>();
            log.AppendLine("  added PlayerHealth");
        }
        health.enabled = true;

        // ---- fall damage (Phase 2 Step 3) ---------------------------
        var fall = root.GetComponent<PlayerFallDamage>();
        if (fall == null)
        {
            fall = root.AddComponent<PlayerFallDamage>();
            log.AppendLine("  added PlayerFallDamage");
        }
        fall.enabled = true;

        // ---- downed / bleed-out (Phase 2 Step 5) ---------------------
        var downed = root.GetComponent<DownedPlayer>();
        if (downed == null)
        {
            downed = root.AddComponent<DownedPlayer>();
            log.AppendLine("  added DownedPlayer");
        }
        downed.enabled = true;

        // ---- pickup reach must include PEOPLE (Phase 2 Step 6) -------
        //
        // pickupMask was Loot only (bit 8). A downed crewmate is 70kg of
        // Massive cargo in every respect that matters, but they are on the
        // PLAYER layer - moving them to Loot would break their ground check
        // and is exactly the layer-stomping PHASE2_SPEC warns against. So the
        // reach is widened instead of the body being relabelled.
        //
        // Built from layer NAMES rather than the literal 320, so renumbering
        // the layers cannot silently make crewmates unpickable.
        var carry2 = root.GetComponent<PlayerCarry>();
        if (carry2 != null)
        {
            int loot = LayerMask.NameToLayer("Loot");
            int player = LayerMask.NameToLayer("Player");
            int mask = 0;
            if (loot >= 0) mask |= 1 << loot;
            if (player >= 0) mask |= 1 << player;
            if (mask != 0)
            {
                carry2.pickupMask = mask;
                log.AppendLine($"  PlayerCarry.pickupMask = Loot + Player ({mask})");
            }
        }

        // ---- the driver ---------------------------------------------
        var drv = root.GetComponent<PlayerAnimatorDriver>();
        if (drv == null) drv = root.AddComponent<PlayerAnimatorDriver>();
        drv.animator = anim;
        drv.enabled = true;
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
