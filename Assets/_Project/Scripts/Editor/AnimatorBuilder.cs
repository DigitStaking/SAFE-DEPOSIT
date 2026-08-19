// AnimatorBuilder.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/AnimatorBuilder.cs
//
// Menu:  SAFE DEPOSIT -> Animation -> Build Full Animator
//
// Supersedes "Fix Everything". Safe to run any number of times: download more
// Mixamo clips, run it again, they get wired. Missing clips are reported and
// skipped, never fatal.
//
// See ANIMATIONS.md in the project root for the clip shopping list and the
// reasoning behind the layer split.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimatorBuilder
{
    const string ModelDir      = "Assets/_Project/Models";
    const string PlayerFbx     = ModelDir + "/Player.fbx";
    const string AnimDir       = "Assets/_Project/Animation";
    const string ControllerPath= AnimDir + "/AC_PlayerDiver.controller";
    const string MaskPath      = AnimDir + "/Mask_UpperBody.mask";
    const string PlayerPrefab  = "Assets/_Project/Prefabs/Player.prefab";

    // Movement speed the blend tree is calibrated against. MoveZ = 1 means
    // "walking at this speed". Must match PlayerMotor.moveSpeed or the walk
    // cycle plays at the wrong rate and the feet slide.
    public const float WalkSpeed = 4.5f;

    // Lifts the kneel clip so it sits ON the floor instead of through it.
    // See the note where it is used. Set from PlayerHealth's measured
    // "sink" readout; 0 until that number is known.
    public const float KneelYOffset = 0f;

    // ------------------------------------------------------------------
    // CLIP SLOTS
    //
    // Each slot lists keywords tried in order, plus keywords that DISQUALIFY
    // a file. The exclusions matter more than the matches: "idle" alone would
    // happily grab "Box Idle", "Hanging Idle" and "Falling Idle" and put a
    // carry pose in your locomotion tree.
    // ------------------------------------------------------------------
    class Slot
    {
        public string Key;
        public string[] Match;
        public string[] Not = new string[0];
        public bool Required;
    }

    static readonly Slot[] Slots =
    {
        // ---- base layer, locomotion ----
        new Slot { Key="Idle",      Match=new[]{"breathing idle","standing idle","happy idle","idle"},
                                    Not=new[]{"box","hang","fall","climb","carry"}, Required=true },
        new Slot { Key="WalkF",     Match=new[]{"walking","walk"},
                                    Not=new[]{"backward","strafe","box","carry","left","right"}, Required=true },
        new Slot { Key="WalkB",     Match=new[]{"walking backwards","walking backward","walk backward"} },
        new Slot { Key="StrafeL",   Match=new[]{"left strafe walking","left strafe"} },
        new Slot { Key="StrafeR",   Match=new[]{"right strafe walking","right strafe"} },
        new Slot { Key="Run",       Match=new[]{"standard run","running","run"}, Not=new[]{"strafe","backward"} },

        // ---- base layer, air ----
        new Slot { Key="JumpUp",    Match=new[]{"jumping up","jump up","jumping","jump"}, Not=new[]{"down"} },
        new Slot { Key="Fall",      Match=new[]{"falling idle","falling"}, Not=new[]{"landing","death","back"} },
        // Land / Hard Landing removed - the landing pose is gone, JumpUp and
        // Falling both return straight to locomotion now. The clip itself is
        // still on disk (Player@Hard Landing.fbx) and still gets imported
        // with correct settings by the pass below, it is just never wired
        // into a state, so nothing ever plays it.

        // ---- base layer, rope ----

        // ---- base layer, states ----
        // "Downed", not "Death". Your call, and it is the better one - see the
        // note on the Downed state below.
        new Slot { Key="Downed",    Match=new[]{"kneeling","kneel","injured","wounded",
                                                "falling back death","dying","death"} },
        new Slot { Key="Stun",      Match=new[]{"stunned","standing react","hit reaction","react"} },

        // ---- arms layer ----
        new Slot { Key="CarryIdle", Match=new[]{"box idle","carry idle","holding"} },
        new Slot { Key="CarryWalk", Match=new[]{"box walk","walking with box","carry walk"} },
        new Slot { Key="PickUp",    Match=new[]{"picking up","pick up","pickup"} },
        new Slot { Key="Stow",      Match=new[]{"putting down","put down","box put"} },
        new Slot { Key="Use",       Match=new[]{"button pushing","pressing","pushing button"} },
        new Slot { Key="Pull",      Match=new[]{"pulling","pull rope"} },

        // ---- emotes (arms layer) ----
        new Slot { Key="Wave",      Match=new[]{"waving","wave"} },
        new Slot { Key="Point",     Match=new[]{"pointing","point"} },
        new Slot { Key="Dance",     Match=new[]{"hip hop dancing","dancing","dance"}, Not=new[]{"silly"} },
        new Slot { Key="Clap",      Match=new[]{"clapping","clap"} },
        new Slot { Key="Salute",    Match=new[]{"salute"} },
        new Slot { Key="Dance2",    Match=new[]{"silly dancing","silly"} },
    };

    // Checked FIRST. "Falling To Landing" contains both "falling" and
    // "landing" - it must not loop, so non-loop wins ties.
    static readonly string[] NoLoop =
    { "jump", "land", "picking", "putting", "wav", "point", "dying", "death",
      "react", "stunned", "button", "salute", "shrug", "pick", "put" };

    static readonly string[] DoLoop =
    { "idle", "walk", "run", "strafe", "climb", "hang", "falling", "dancing",
      "dance", "clapping", "pulling", "treading", "kneel", "injured" };

    [MenuItem("SAFE DEPOSIT/Animation/Build Full Animator")]
    static void Build()
    {
        if (!File.Exists(PlayerFbx)) { Debug.LogError($"[Anim] {PlayerFbx} not found."); return; }

        // ================================================================
        // 1. AVATAR
        // ================================================================
        var pi = (ModelImporter)AssetImporter.GetAtPath(PlayerFbx);
        if (pi.animationType != ModelImporterAnimationType.Human ||
            pi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            pi.animationType = ModelImporterAnimationType.Human;
            pi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            pi.importAnimation = false;
            pi.SaveAndReimport();
        }

        var avatar = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogError("[Anim] Player.fbx has no valid Humanoid avatar. Rig -> Configure.");
            return;
        }

        // ================================================================
        // 2. IMPORT EVERY Player@*.fbx AS HUMANOID
        //
        // Two passes. Never hold an object reference across SaveAndReimport -
        // the reimport destroys and rebuilds it, and the stale reference
        // throws MissingReferenceException.
        // ================================================================
        var paths = Directory.GetFiles(ModelDir, "Player@*.fbx")
                             .Select(x => x.Replace('\\', '/')).ToArray();

        foreach (var p in paths)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(p);
            if (imp == null) continue;

            var av = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx).OfType<Avatar>().FirstOrDefault();
            if (av == null) { Debug.LogError("[Anim] avatar vanished mid-import."); return; }

            string lower = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            bool loop = !NoLoop.Any(lower.Contains) && DoLoop.Any(lower.Contains);

            // ---- IS THIS CLIP'S BODY WEIGHT ON THE FLOOR? ----
            //
            // This decides "Root Transform Position (Y) -> Based Upon", and it
            // is why the Hard Landing clip floats.
            //
            // "Original" keeps the height the animator authored. Hard Landing
            // was authored as the tail of a fall, so its original height starts
            // metres up - bake that in and the feet never reach the floor.
            //
            // "Feet" re-bases the clip so the lowest foot sits on y = 0. That
            // is right for anything standing on its feet, and wrong for
            // anything whose feet are not what is touching the floor -
            // airborne clips, and kneeling. ("airborne" below is really "do
            // not measure from the feet"; it kept its name because renaming it
            // would churn the file for nothing.)
            // WHY JUMP AND FALL MUST STAY ON "Original".
            //
            // "Feet" does not measure once - it re-bases EVERY FRAME so the
            // lowest foot sits on y = 0. On a walk that is correct, because
            // a foot really is on the floor. In an airborne clip the legs are
            // swinging through empty air, so the reference point keeps moving
            // and the entire body gets shoved up and down chasing it. That
            // reads as the character bouncing mid-fall and sinking through
            // the floor.
            //
            // "Original" keeps the authored height, which is stable for the
            // whole clip. The Rigidbody owns world position anyway, so a
            // constant offset is harmless where a per-frame one is not.
            //
            // KNEELING NEEDS THE SAME TREATMENT, FOR THE SAME REASON.
            //
            // "Feet" re-bases so the LOWEST FOOT sits on y = 0. On a kneel the
            // character is on their knees and the feet are tucked up behind
            // them - often HIGHER than the knees. Unity dutifully shoves the
            // whole body down to bring those raised feet to the floor, and the
            // character sinks through it to the waist. Exactly the airborne
            // problem wearing different clothes: the feet are not what is
            // touching the ground, so they are the wrong thing to measure.
            //
            // The knees are on the floor and "Original" keeps the authored
            // height, which is where the animator put them.
            //
            // climb / hang / rope are gone with the rope.
            bool airborne = lower.Contains("jump") || lower.Contains("falling")
                         || lower.Contains("kneel");

            bool kneel = lower.Contains("kneel");

            // ROOT HEIGHT OFFSET FOR THE KNEEL (the "Offset" field under Root
            // Transform Position (Y) in the import inspector).
            //
            // Neither Feet nor Original lands this clip on the floor on its
            // own, and the reason is retargeting: this is a STOCKY rig with
            // short legs - FirstPersonHands measures its arm reach at 0.5m -
            // and the clip was authored on standard proportions. Unity
            // retargets the POSE faithfully and the absolute height comes out
            // wrong for the shorter skeleton.
            //
            // So the last correction is an explicit offset, and it is a
            // MEASURED number rather than another guess: PlayerHealth's debug
            // HUD prints "sink 0.xxx m" while downed, which is exactly how far
            // the lowest foot is under the floor. Put that number here.
            float yOffset = kneel ? KneelYOffset : 0f;

            // Compare against the settings actually APPLIED, not Unity's
            // defaults - defaultClipAnimations always reports the defaults, so
            // testing those would reimport every file on every run.
            var applied = imp.clipAnimations;
            bool settingsOk = applied.Length > 0 &&
                              applied.All(x => x.loopTime == loop &&
                                               x.heightFromFeet == !airborne &&
                                               x.keepOriginalPositionY == airborne &&
                                               Mathf.Approximately(x.level, yOffset));

            bool needsWork = imp.animationType != ModelImporterAnimationType.Human ||
                             imp.avatarSetup   != ModelImporterAvatarSetup.CopyFromOther ||
                             imp.sourceAvatar  != av ||
                             !imp.importAnimation ||
                             !settingsOk;

            if (!needsWork) continue;   // already correct - skip the slow reimport

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar  = av;
            imp.importAnimation = true;

            // Bake root motion into the pose: the clip animates in place and
            // the Rigidbody does the travelling. Otherwise the two fight.
            imp.clipAnimations = imp.defaultClipAnimations.Select(c =>
            {
                c.loopTime = loop;

                // Rotation: bake in, keep original facing. The Rigidbody turns
                // the character; the clip must not.
                c.lockRootRotation   = true;   c.keepOriginalOrientation = true;

                // Height: bake in, based upon Feet for grounded clips so the
                // soles land on y = 0, Original for airborne ones.
                c.lockRootHeightY    = true;
                c.keepOriginalPositionY = airborne;
                c.heightFromFeet        = !airborne;

                // "level" is the Offset field under Root Transform Position (Y).
                c.level = yOffset;

                // Horizontal: bake in and re-centre, so the clip plays in place
                // and the Rigidbody does all the travelling.
                c.lockRootPositionXZ = true;   c.keepOriginalPositionXZ  = false;
                return c;
            }).ToArray();

            imp.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        // ---- pass two: collect ----
        var files = new Dictionary<string, AnimationClip>();
        foreach (var p in paths)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>()
                                    .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview"));
            if (clip == null) { Debug.LogWarning($"[Anim] no clip in {Path.GetFileName(p)}"); continue; }
            if (!clip.isHumanMotion)
                Debug.LogError($"[Anim] {clip.name} imported as GENERIC, not human. It will not play.");
            files[Path.GetFileNameWithoutExtension(p).ToLowerInvariant()] = clip;
        }

        // ---- resolve slots ----
        var clips = new Dictionary<string, AnimationClip>();
        var report = new List<string>();
        foreach (var slot in Slots)
        {
            var c = Resolve(files, slot);
            if (c != null) { clips[slot.Key] = c; report.Add($"  {slot.Key,-10} = {c.name}"); }
            else
            {
                report.Add($"  {slot.Key,-10} = <color=#ff9955>missing</color>");
                if (slot.Required)
                    Debug.LogError($"[Anim] REQUIRED clip missing for '{slot.Key}'. " +
                                   $"Download one matching: {string.Join(" / ", slot.Match)}");
            }
        }
        Debug.Log($"<b>[Anim] clip map</b> ({clips.Count}/{Slots.Length} filled)\n" +
                  string.Join("\n", report));

        if (!clips.ContainsKey("Idle")) return;

        // ================================================================
        // 3. AVATAR MASK
        //
        // Defines which bones the arms layer is allowed to write. Legs and
        // root stay off so locomotion always survives underneath.
        // ================================================================
        Directory.CreateDirectory(AnimDir);
        var mask = new AvatarMask();
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root,        false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body,        true);  // spine/chest lean
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head,        true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg,     false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg,    false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm,     true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,    true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK,  false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK,  false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);

        AssetDatabase.DeleteAsset(MaskPath);
        AssetDatabase.CreateAsset(mask, MaskPath);

        // ================================================================
        // 4. CONTROLLER
        // ================================================================
        // REBUILT IN PLACE, NOT DELETED AND RECREATED.
        //
        // Deleting the asset destroys it, and every reference to it elsewhere -
        // the Animator on Player.prefab above all - goes null the instant it
        // happens. If anything later in this method then fails, the prefab is
        // left pointing at nothing and the character T-poses with no error,
        // because an Animator with no controller is a legal, silent state.
        //
        // Clearing it keeps the same asset and the same GUID, so existing
        // references survive no matter what happens after this line.
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ac == null) ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else ClearController(ac);

        P(ac, "MoveX",      AnimatorControllerParameterType.Float);
        P(ac, "MoveZ",      AnimatorControllerParameterType.Float);
        P(ac, "Speed",      AnimatorControllerParameterType.Float);
        P(ac, "VelY",       AnimatorControllerParameterType.Float);
        P(ac, "Grounded",   AnimatorControllerParameterType.Bool, true);
        P(ac, "Jump",       AnimatorControllerParameterType.Trigger);
        P(ac, "Carry",      AnimatorControllerParameterType.Int);
        P(ac, "DoPickUp",   AnimatorControllerParameterType.Trigger);
        P(ac, "DoStow",     AnimatorControllerParameterType.Trigger);
        P(ac, "DoUse",      AnimatorControllerParameterType.Trigger);
        P(ac, "Emote",      AnimatorControllerParameterType.Int);
        P(ac, "DoEmote",    AnimatorControllerParameterType.Trigger);
        P(ac, "Downed",     AnimatorControllerParameterType.Bool);
        P(ac, "DoStun",     AnimatorControllerParameterType.Trigger);

        BuildBaseLayer(ac, clips);
        BuildArmsLayer(ac, clips, mask);

        // IK PASS. Without this, Unity never calls OnAnimatorIK and
        // FirstPersonHands does nothing at all - silently, as usual.
        // ac.layers returns a COPY, so it must be written back.
        var layers = ac.layers;
        for (int i = 0; i < layers.Length; i++) layers[i].iKPass = true;
        ac.layers = layers;

        EditorUtility.SetDirty(ac);

        // ================================================================
        // 5. PREFAB
        // ================================================================
        WirePrefab(ac, avatar);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<b>[Anim] DONE.</b> {clips.Count} clips, 2 layers. " +
                  "Press Play, then V for third person.");
    }

    // ------------------------------------------------------------------
    // BASE LAYER - the whole body
    // ------------------------------------------------------------------
    static void BuildBaseLayer(AnimatorController ac, Dictionary<string, AnimationClip> c)
    {
        var sm = ac.layers[0].stateMachine;

        // ---- Locomotion: 2D Freeform Directional ----
        //
        // Five separate states would need 20 transitions and would pop on every
        // direction change. A blend tree interpolates in muscle space, so a
        // diagonal walk exists without anyone animating one.
        var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter  = "MoveX";
        tree.blendParameterY = "MoveZ";
        tree.useAutomaticThresholds = false;

        tree.AddChild(c["Idle"], new Vector2(0f, 0f));
        tree.AddChild(Get(c, "WalkF", "Idle"),  new Vector2( 0f,  1f));
        tree.AddChild(Get(c, "WalkB", "WalkF"), new Vector2( 0f, -1f));
        tree.AddChild(Get(c, "StrafeL","WalkF"),new Vector2(-1f,  0f));
        tree.AddChild(Get(c, "StrafeR","WalkF"),new Vector2( 1f,  0f));
        if (c.ContainsKey("Run")) tree.AddChild(c["Run"], new Vector2(0f, 2f));

        sm.defaultState = loco;

        // ---- Air: two states, because a jump is not a fixed length ----
        AnimatorState jump = null, fall = null;

        if (c.ContainsKey("JumpUp")) jump = sm.AddState("JumpUp", new Vector3(400, -100));
        if (jump != null) jump.motion = c["JumpUp"];

        if (c.ContainsKey("Fall"))   fall = sm.AddState("Falling", new Vector3(650, -100));
        if (fall != null) fall.motion = c["Fall"];

        if (jump != null)
        {
            var t = sm.AddAnyStateTransition(jump);
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            t.duration = 0.05f; t.hasExitTime = false; t.canTransitionToSelf = false;
        }

        // ---- WHY "Falling" IS GATED ON SPEED, NOT ON TIME ----
        //
        // Mixamo's Falling Idle is a SKYDIVING pose: face down, arms and legs
        // spread. It is authored for a long drop.
        //
        // This used to be Exit(jump, fall, 0.7f, ...) - 70% of the way through
        // the jump clip, unconditionally. A 1.1m hop is airborne for under a
        // second, so the character adopted a skydiver's belly-flop about
        // 30cm above the floor, which reads exactly like sinking through it.
        // Nothing was ever below the ground; the pose was just horizontal.
        //
        // Gate it on actually falling fast instead. A 1.1m jump tops out
        // around -5.9 m/s, so at -8 a normal jump NEVER reaches this state -
        // JumpUp covers the whole hop - and a genuine multi-storey drop
        // still gets the free-fall pose it was drawn for.
        const float FreeFallSpeed = -8f;

        if (jump != null && fall != null)
        {
            var t = jump.AddTransition(fall);
            t.AddCondition(AnimatorConditionMode.Less, FreeFallSpeed, "VelY");
            t.duration = 0.2f; t.hasExitTime = false;
        }

        if (fall != null)
        {
            // Walk off a ledge without jumping. Same speed gate, so stepping
            // off a crate keeps the walk cycle instead of snapping to a
            // skydive for a 40cm drop.
            var t = loco.AddTransition(fall);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            t.AddCondition(AnimatorConditionMode.Less, FreeFallSpeed, "VelY");
            t.duration = 0.2f; t.hasExitTime = false;
        }

        // Straight back to locomotion - there is no Landing state to route
        // through any more.
        if (fall != null)
        {
            var t = fall.AddTransition(loco);
            t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            t.duration = 0.1f; t.hasExitTime = false;
        }
        // ---- JUMP MUST HAVE AN EXIT THAT A NORMAL HOP ACTUALLY TAKES ----
        //
        // This block used to be guarded by `fall == null`: it was the fallback
        // for a project with no Falling clip, because the real exit from
        // JumpUp was the unconditional 70%-exit-time hop into Falling.
        //
        // Gating that hop on VelY turned JumpUp into a DEAD END for every jump
        // that never reaches -8 m/s - which is every normal jump. The
        // character froze mid-air in the tuck pose. An Animator dead end is
        // silent: nothing logs, the character simply stops.
        //
        // The VelY guard is not optional. Coyote time holds Grounded true for
        // 0.15s after the feet leave the floor, so a bare Grounded check would
        // fire on the takeoff frame and cancel the jump before it started. At
        // takeoff VelY is about +4.6, so this waits until you are coming down.
        if (jump != null)
        {
            var t = jump.AddTransition(loco);
            t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            t.AddCondition(AnimatorConditionMode.Less, 1f, "VelY");
            t.duration = 0.1f; t.hasExitTime = false;

            // Belt and braces: whatever the conditions do, the clip running
            // out always leaves the state. Never leave an air state without an
            // unconditional way home.
            Exit(jump, loco, 0.95f, 0.15f);
        }

        // ---- Downed ----
        //
        // Kneeling rather than dying, and it changes the state machine in a way
        // worth being explicit about: a DEATH state is a dead end - you enter
        // and never leave, so the transition out does not exist. A DOWNED state
        // is a loop with a way back, so it needs BOTH edges.
        //
        // That is not a technical detail, it is the whole design. A dead player
        // is a spectator with nothing to do. A downed player is a timer on the
        // other three, and something they have to walk toward while the floor
        // is collapsing. Much better game.
        if (c.ContainsKey("Downed"))
        {
            var downed = sm.AddState("Downed", new Vector3(400, 250));
            downed.motion = c["Downed"];

            var inD = sm.AddAnyStateTransition(downed);
            inD.AddCondition(AnimatorConditionMode.If, 0, "Downed");
            inD.duration = 0.25f; inD.canTransitionToSelf = false;

            var outD = downed.AddTransition(loco);          // revived
            outD.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed");
            outD.duration = 0.3f; outD.hasExitTime = false;
        }

        // ---- Stun: trap hits, falling debris ----
        //
        // Half a second where the player can see they are not in control. Cheap
        // to add and it is the difference between damage being a number that
        // changed and damage being an event that happened.
        if (c.ContainsKey("Stun"))
        {
            var stun = sm.AddState("Stun", new Vector3(150, 250));
            stun.motion = c["Stun"];

            var inS = sm.AddAnyStateTransition(stun);
            inS.AddCondition(AnimatorConditionMode.If, 0, "DoStun");
            inS.duration = 0.08f; inS.canTransitionToSelf = false;

            Exit(stun, loco, 0.85f, 0.2f);
        }

        // ---- Emotes: full body, on THIS layer ----
        EmoteState(sm, loco, c, "Wave",   1, new Vector3(-150,   0));
        EmoteState(sm, loco, c, "Point",  2, new Vector3(-150,  90));
        EmoteState(sm, loco, c, "Dance",  3, new Vector3(-150, 180));
        EmoteState(sm, loco, c, "Clap",   4, new Vector3(-150, 270));
        EmoteState(sm, loco, c, "Salute", 5, new Vector3(-150, 360));
        EmoteState(sm, loco, c, "Dance2", 6, new Vector3(-150, 450));
    }

    // ------------------------------------------------------------------
    // ARMS LAYER - masked to chest, arms, hands, head
    // ------------------------------------------------------------------
    static void BuildArmsLayer(AnimatorController ac,
                               Dictionary<string, AnimationClip> c,
                               AvatarMask mask)
    {
        var sm = new AnimatorStateMachine
        {
            name = "Arms",
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(sm, ac);

        ac.AddLayer(new AnimatorControllerLayer
        {
            name          = "Arms",
            stateMachine  = sm,
            defaultWeight = 1f,
            avatarMask    = mask,
            blendingMode  = AnimatorLayerBlendingMode.Override,
        });

        // The default state is EMPTY with Write Defaults OFF. An empty state
        // that writes nothing lets the base layer through untouched - that is
        // how the layer switches itself off without any script babysitting it.
        var none = sm.AddState("None", new Vector3(250, 0));
        none.writeDefaultValues = false;
        sm.defaultState = none;

        // ---- Carry ----
        if (c.ContainsKey("CarryIdle"))
        {
            AnimatorState carry;
            if (c.ContainsKey("CarryWalk"))
            {
                carry = ac.CreateBlendTreeInController("Carry", out BlendTree bt, 1);
                bt.blendType = BlendTreeType.Simple1D;
                bt.blendParameter = "Speed";
                bt.useAutomaticThresholds = false;
                bt.AddChild(c["CarryIdle"], 0f);
                bt.AddChild(c["CarryWalk"], 2.5f);
            }
            else
            {
                // No carry-walk clip? Does not matter. The mask means the legs
                // keep walking underneath the carry pose regardless.
                carry = sm.AddState("Carry", new Vector3(550, 0));
                carry.motion = c["CarryIdle"];
            }
            carry.writeDefaultValues = false;

            // NOT from AnyState. An AnyState transition re-evaluates every
            // frame, so "Carry > 0" would fire continuously and cut the pickup
            // animation off after one frame. From None it only fires on the
            // way back, which is exactly once per pickup.
            var inC = none.AddTransition(carry);
            inC.AddCondition(AnimatorConditionMode.Greater, 0, "Carry");
            inC.duration = 0.2f; inC.hasExitTime = false;

            var outC = carry.AddTransition(none);
            outC.AddCondition(AnimatorConditionMode.Equals, 0, "Carry");
            outC.duration = 0.2f; outC.hasExitTime = false;
        }

        // ---- One-shot actions: play, then fall back to None ----
        OneShot(sm, none, c, "PickUp", "DoPickUp", new Vector3(550, 120));
        OneShot(sm, none, c, "Stow",   "DoStow",   new Vector3(550, 200));
        OneShot(sm, none, c, "Use",    "DoUse",    new Vector3(550, 280));

        // Emotes are NOT here. They live on the base layer - see BuildBaseLayer.
        // A dance masked to the upper body is a person shuffling their arms
        // while their legs stand to attention. Emotes need the whole body.
    }

    static void OneShot(AnimatorStateMachine sm, AnimatorState none,
                        Dictionary<string, AnimationClip> c,
                        string key, string trigger, Vector3 pos)
    {
        if (!c.ContainsKey(key)) return;

        var st = sm.AddState(key, pos);
        st.motion = c[key];
        st.writeDefaultValues = false;

        // Tagged so FirstPersonHands can find it. Gameplay actions keep the
        // hands locked to the camera by default - flip freeArmsDuringActions
        // on the component if you would rather see the reach.
        st.tag = "ArmAction";

        var t = sm.AddAnyStateTransition(st);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.duration = 0.1f; t.canTransitionToSelf = false;

        Exit(st, none, 0.8f, 0.2f);
    }

    /// <summary>
    /// Full-body emote on the BASE layer, cancelled by movement.
    ///
    /// These were on the masked arms layer and that was wrong: a mask only
    /// writes arms, chest and head, so a dance came out as a person waving
    /// their arms while their legs stood perfectly still. Anything involving
    /// the hips or feet has to own the whole body.
    ///
    /// The cost of moving it here is that an emote now competes with walking
    /// instead of layering over it - which is exactly the behaviour wanted:
    /// you emote when you are standing still, and moving cancels it.
    /// </summary>
    static void EmoteState(AnimatorStateMachine sm, AnimatorState loco,
                           Dictionary<string, AnimationClip> c,
                           string key, int id, Vector3 pos)
    {
        if (!c.ContainsKey(key)) return;

        var clip = c[key];
        var st = sm.AddState("Emote_" + key, pos);
        st.motion = clip;

        // FirstPersonHands reads this tag and releases the hand IK, so the
        // clip is actually seen instead of being overridden.
        st.tag = "FreeArms";

        // ---- entry ----
        // Grounded and near-stationary. Without those two conditions, pressing
        // an emote while walking would start it and the cancel rule below would
        // kill it on the same frame - a visible flicker and no emote.
        var t = sm.AddAnyStateTransition(st);
        t.AddCondition(AnimatorConditionMode.If, 0, "DoEmote");
        t.AddCondition(AnimatorConditionMode.Equals, id, "Emote");
        t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        t.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");
        t.duration = 0.2f; t.canTransitionToSelf = false;

        // ---- cancel on movement ----
        var cancel = st.AddTransition(loco);
        cancel.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
        cancel.duration = 0.15f; cancel.hasExitTime = false;

        var cancelAir = st.AddTransition(loco);
        cancelAir.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
        cancelAir.duration = 0.15f; cancelAir.hasExitTime = false;

        // Jump, Climbing, DoStun and Downed all cancel it too, for free -
        // they are AnyState transitions, and AnyState outranks a state's own.

        // ---- natural end ----
        // A looping clip (the dances) keeps going until you move. A one-shot
        // (wave, point, salute) plays once and returns by itself.
        if (!clip.isLooping) Exit(st, loco, 0.92f, 0.25f);
    }

    /// <summary>Transition on exit time - "when this clip is n% done, move on".</summary>
    static void Exit(AnimatorState from, AnimatorState to, float exitTime, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.duration = dur;
    }

    static void WirePrefab(AnimatorController ac, Avatar avatar)
    {
        if (!File.Exists(PlayerPrefab))
        {
            Debug.LogWarning($"[Anim] {PlayerPrefab} not found - assign AC_PlayerDiver by hand.");
            return;
        }

        // ---- PHASE 1: the controller. Nothing optional in here. ----------
        //
        // Split into two saves deliberately. Last run, one of the cosmetic
        // steps below threw, the whole block was abandoned, and the prefab was
        // never given its controller - which an Animator accepts in complete
        // silence and renders as a T-pose. The critical assignment now lands
        // and is saved before anything that is allowed to fail runs at all.
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            var anim = root.GetComponentInChildren<Animator>(true);
            if (anim == null) { Debug.LogError("[Anim] no Animator in the prefab."); return; }

            anim.runtimeAnimatorController = ac;
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.enabled = true;

            var drv = root.GetComponent<PlayerAnimatorDriver>();
            if (drv == null) drv = root.AddComponent<PlayerAnimatorDriver>();
            drv.animator = anim;

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            Debug.Log($"[Anim] <b>controller wired</b> to Animator on '{anim.gameObject.name}'");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Anim] CONTROLLER WIRING FAILED: {e}");
            return;
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }

        // ---- PHASE 2: the nice-to-haves. Allowed to fail. ----------------
        root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            var anim = root.GetComponentInChildren<Animator>(true);
            bool changed = false;

            // Hand IK must live on the SAME GameObject as the Animator -
            // OnAnimatorIK is only delivered there.
            if (anim != null && anim.GetComponent<FirstPersonHands>() == null)
            {
                anim.gameObject.AddComponent<FirstPersonHands>();
                changed = true;
                Debug.Log("[Anim] added FirstPersonHands to the model.");
            }

            // Shadow is handled by LocalFirstPersonBodyCull.hideOwnShadow.
            // Two components both capturing and restoring shadowCastingMode
            // fight each other, so only one owns it.

            // The old capsule arms were a stand-in for exactly this. The
            // PlayerArms component is gone, but leftover objects may still be
            // in older prefabs - leave them on and you get four arms.
            foreach (var n in new[] { "Arm_L", "Arm_R" })
            {
                var t = root.transform.Find("ChestPivot/" + n) ?? root.transform.Find(n);
                if (t != null && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false); changed = true;
                    Debug.Log($"[Anim] hid placeholder {n}.");
                }
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Anim] optional prefab step skipped: {e.Message}\n" +
                             "The controller is wired and animations will play.");
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Empty an existing controller so it can be rebuilt without destroying
    /// the asset. Order matters: transitions before states, states before
    /// layers, or you leave dangling references behind.
    /// </summary>
    static void ClearController(AnimatorController ac)
    {
        for (int i = ac.layers.Length - 1; i >= 1; i--) ac.RemoveLayer(i);

        var ps = ac.parameters;
        for (int i = ps.Length - 1; i >= 0; i--) ac.RemoveParameter(i);

        var sm = ac.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
        foreach (var t in sm.entryTransitions.ToArray())    sm.RemoveEntryTransition(t);
        foreach (var s in sm.states.ToArray())              sm.RemoveState(s.state);
        foreach (var m in sm.stateMachines.ToArray())       sm.RemoveStateMachine(m.stateMachine);

        // Blend trees and the arms state machine are stored as sub-assets.
        // Removing the state that used them does not delete them, so without
        // this the file grows a little more orphaned junk on every run.
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (o is BlendTree) Object.DestroyImmediate(o, true);
            else if (o is AnimatorStateMachine asm && asm != sm) Object.DestroyImmediate(o, true);
        }
    }

    // ---- helpers -----------------------------------------------------
    static void P(AnimatorController ac, string name,
                  AnimatorControllerParameterType type, bool defaultBool = false)
    {
        ac.AddParameter(new AnimatorControllerParameter
        { name = name, type = type, defaultBool = defaultBool, defaultFloat = 0f, defaultInt = 0 });
    }

    static AnimationClip Get(Dictionary<string, AnimationClip> c, string key, string fallback)
        => c.ContainsKey(key) ? c[key] : c[fallback];

    static AnimationClip Resolve(Dictionary<string, AnimationClip> files, Slot slot)
    {
        foreach (var m in slot.Match)
            foreach (var kv in files)
            {
                if (!kv.Key.Contains(m)) continue;
                if (slot.Not.Any(kv.Key.Contains)) continue;
                return kv.Value;
            }
        return null;
    }
}
