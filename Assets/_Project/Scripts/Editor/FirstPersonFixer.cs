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

    // HANDS SIT A FIXED DISTANCE BELOW THE EYE.
    //
    // This was a world height (1.35) for a while, on the reasoning that the
    // hands care where the SHOULDER is - which is true of the IK solver and
    // false of the person looking at the screen.
    //
    // At an eye of 1.65 the two agreed. At 1.25 they do not: the shoulder is
    // at about 1.42, so hands at their correct shoulder-relative height end
    // up ABOVE the camera and fill the top of the frame. Physically right,
    // visually wrong, and "hands too high" is the only report that matters.
    //
    // One number now, and it means what it says: how far below your eye your
    // hands hang. Change EyeOffset.y and the hands follow it instead of
    // staying put.
    //
    // THE HONEST CAVEAT: at an eye of 1.25 the camera sits BELOW this rig's
    // shoulder, so hands low enough to look right are also further from the
    // shoulder than the arm can reach (~0.5m) and the IK straightens them.
    // Raising the eye toward 1.55-1.65 makes both work at once. Until then
    // this is the interim the arms have been in since Phase 1, and Block 8
    // rebuilds them properly.
    //
    // 0.22, AND IT WAS MEASURED BY HAND, NOT DERIVED.
    //
    // This constant is the one that decides whether the hands read as hands
    // or as a clown holding its palms up next to its face. The prefab shipped
    // with +0.10 - hands anchored TEN CENTIMETRES ABOVE THE EYE - and nobody
    // saw it offline, because the scene body carried an Inspector override of
    // -0.22 that had been tuned by eye and never pushed back to the prefab.
    //
    // Offline plays the scene body and looked right. The network spawns the
    // PREFAB and looked wrong. Same game, same script, two different numbers,
    // and the only thing separating them was an override sitting in the
    // .unity file.
    //
    // That is the general shape of every remaining Phase 4 surprise: anything
    // the player prefab does not carry, a spawned player does not get. Worth
    // remembering at Step 5 when the loot starts spawning.
    //
    // -0.22 is the number that was tuned against this rig by looking at it.
    // Not 0.30, which was a guess in this file that the prefab never used.
    const float HandBelowEye = 0.22f;

    static Vector3 LeftHand  => new Vector3(-0.24f, -HandBelowEye, 0.52f);
    static Vector3 RightHand => new Vector3( 0.24f, -HandBelowEye, 0.52f);
    const float BaseFov  = 75f;
    const float NearClip = 0.3f;   // 0.05 exposes the inside of your own skull

    [MenuItem("SAFE DEPOSIT/Fix First Person Setup")]
    /// <summary>
    /// Public so PlayerFbxSetupTool can call it. That tool DESTROYS and
    /// rebuilds PlayerModel_FBX_VISUAL, and FirstPersonHands lives on that
    /// child - so rebuilding the model silently takes the hand IK with it.
    /// </summary>
    public static void Fix()
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

        // ---- hand IK -------------------------------------------------
        //
        // This used to ADD FirstPersonHands and configure it. It no longer
        // does, and it no longer will: re-adding a retired component from a
        // "fixer" is how it kept reappearing after being removed, with the
        // damage surfacing days later somewhere unrelated.
        //
        // The hands on the real body are PlayerCarryArms (places them on what
        // you are holding) and PlayerPushArms (the shove). First-person hands
        // are FirstPersonViewmodel's job now. An existing copy is only
        // reported, never installed - remove it with Retire Old Hand Scripts.
        var hands = anim.GetComponent<FirstPersonHands>();

        if (hands != null)
            log.AppendLine($"  FirstPersonHands still on '{anim.gameObject.name}' - it " +
                           "competes with PlayerCarryArms for the same IK goals. " +
                           "SAFE DEPOSIT / Player / Retire Old Hand Scripts.");
        else
            log.AppendLine("  FirstPersonHands: retired (correct).");

        // ---- head cull ----------------------------------------------
        var cull = root.GetComponent<LocalFirstPersonBodyCull>();
        if (cull == null) cull = root.AddComponent<LocalFirstPersonBodyCull>();

        cull.enabled        = true;
        cull.headShrink     = 0.0001f;
        cull.shrinkNeck     = false;   // floating collar, buys nothing
        cull.hideLegs       = false;   // torso floating in mid-air, alarming
        cull.hideOwnShadow  = true;    // kills the headlamp shadow blob
        log.AppendLine("  LocalFirstPersonBodyCull: head only, shadow off");

        // LocalPlayerNoShadow used to be stripped here - one shadow system,
        // not two, because both captured and restored shadowCastingMode and
        // whichever ran second captured a value the first had already changed.
        // The class is deleted now, so there is nothing left to strip.

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

        // PHASE 4 STEP 7. On the person who might USE one, not on the person
        // who needs one - reviving is something you do TO somebody, so every
        // player carries the ability to do it and the crew shares the stock.
        if (root.GetComponent<MedSpray>() == null)
        {
            root.AddComponent<MedSpray>();
            log.AppendLine("  added MedSpray (hold R over a downed crewmate)");
        }

        // PHASE 4 STEP 9. Being held by the mafia has to look like something,
        // or the ransom is a formality nobody argues about.
        if (root.GetComponent<LostSpectator>() == null)
        {
            root.AddComponent<LostSpectator>();
            log.AppendLine("  added LostSpectator (TAB to change who you watch)");
        }

        // PHASE 4 STEP 10. Every crewmate has a mouth; what fills it comes
        // later. The AudioSource is required by VoiceMouth and added with it.
        if (root.GetComponent<VoiceMouth>() == null)
        {
            root.AddComponent<VoiceMouth>();
            log.AppendLine("  added VoiceMouth (positional voice + occlusion)");
        }

        // V speaks, U is the radio. The rule lives apart from the audio and
        // apart from the capture, because it is the only one of the three that
        // is a game rule rather than plumbing.
        if (root.GetComponent<VoiceTransmit>() == null)
        {
            root.AddComponent<VoiceTransmit>();
            log.AppendLine("  added VoiceTransmit (V speak, U radio)");
        }
        downed.enabled = true;

        // ---- headlamp (re-attached, Phase 3 Step 2) ------------------
        //
        // PlayerHeadlamp was attached to NOTHING - not the prefab, not the
        // scene - while every other player component was on the prefab. The
        // only light in the whole project was the scene's directional, so the
        // lamp we built and tuned had simply not been running.
        //
        // It belongs here for the same reason the other five do: this file is
        // the single source of truth for what a player is made of, and a
        // component that has to be dragged on by hand is a component that
        // will go missing again the next time a prefab is rebuilt.
        var lamp = root.GetComponent<PlayerHeadlamp>();
        if (lamp == null)
        {
            lamp = root.AddComponent<PlayerHeadlamp>();
            log.AppendLine("  added PlayerHeadlamp (was attached to nothing)");
        }
        lamp.enabled = true;

        // The visible cone stays OFF. A serialized `true` on the prefab from
        // before this was decided would otherwise put it straight back, and
        // this file exists so that cannot happen quietly.
        lamp.visibleBeam = false;

        // ---- input pairing (Phase 3 Step 6) --------------------------
        //
        // neverAutoSwitchControlSchemes = true.
        //
        // Left off, Unity's PlayerInput hands EVERY device to whichever
        // player last touched one, and re-hands them on every keypress. With
        // two bodies that is not a bug you notice as "wrong device" - it is
        // one body twitching while the other is driven, swapping unpredictably
        // as you play. Off, each PlayerInput keeps the devices it was paired
        // with, which is what PlayerMotor.Keys reads to decide whose
        // keypresses these are.
        //
        // Solo is unaffected: one player, paired with the keyboard, forever.
        var pin = root.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (pin != null)
        {
            pin.neverAutoSwitchControlSchemes = true;
            log.AppendLine("  PlayerInput: never auto-switch control schemes");
        }

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
