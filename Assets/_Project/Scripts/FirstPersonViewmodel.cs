// FirstPersonViewmodel.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/FirstPersonViewmodel.cs
// Goes on: nothing. Starts itself, like VoiceMic.
//
// ========================================================================
// STAGE 2 - THE REAL ARMS, ON THE REAL VIEWMODEL CAMERA.
//
// "I don't want a duplicated player model, body, character, or world object
// in front of my camera... The only thing we duplicate is: ARM + HAND MESH."
//
// Stage 1 (FirstPersonArmsMeshBuilder, Editor-only) already answered whether
// that mesh could exist: geometry_001 is one SkinnedMeshRenderer with no
// separate arms piece, so it built one - filtering every vertex by its
// DOMINANT bone weight, keeping only the ones bound to the arm/hand/finger
// chain, and saving the result as Assets/_Project/Resources/
// PlayerArmsViewmodel.asset. That file is not optional scenery; if it is
// missing, this whole script backs off and leaves your real hands alone
// rather than showing nothing or something wrong. See TryLoadArmsMesh.
//
// This file is what WEARS that mesh. It still clones the skeleton - see
// below for why that is not the "duplicated body" that was rejected - but it
// no longer clones or renders the body geometry at all. What is instantiated
// and immediately stripped down to a skeleton with the arms mesh reattached.
//
// ------------------------------------------------------------------------
// WHY A SKELETON IS STILL CLONED WHEN THE BODY IS NOT
//
// This is not a shortcut kept out of laziness - a version with NO clone at
// all was considered and rejected. Skinning the trimmed mesh straight onto
// the REAL body's live bones would work today, would be simpler, and would
// break the entire point of this system the moment it was used for
// anything: FirstPersonHands already bends those same real bones toward the
// camera, and a teammate watching your THIRD-PERSON body would see that same
// bend, because it is the same skeleton. That is the exact "clutching my own
// face" bug this project has been trying to get away from.
//
// A clone is what lets the viewmodel arms hold a pose the real skeleton does
// not have to share. It costs one lightweight, mesh-less GameObject
// hierarchy - Transforms only, once the trimmed mesh replaces the original -
// not a second character.
//
// ------------------------------------------------------------------------
// WHY A SECOND CAMERA, NOT JUST A CHILD OF THE MAIN ONE
//
// Two problems, one fix.
//
//   1. WALLS. This building is rubble and cramped rooms. Stand close to a
//      wall and a viewmodel parented to the main camera, on the main
//      camera's own culling mask, gets clipped by that wall exactly like any
//      other piece of geometry a few centimetres from the lens. A dedicated
//      camera whose culling mask contains ONLY the viewmodel layer never
//      sees the wall at all - there is nothing in its frustum TO clip
//      against.
//
//   2. IT MUST STAY OFF EVERY OTHER SCREEN. Rendering is per-camera. A layer
//      that only this one local camera's culling mask includes cannot appear
//      on a teammate's screen even in principle - there is no "IsLocal"
//      check to get wrong, because their camera was never told the layer
//      exists.
//
// This project renders through URP, which has its own layering system and
// ignores the legacy Camera.clearFlags/depth fields entirely. The viewmodel
// camera is registered as a URP OVERLAY camera in the main camera's own
// stack - see the long comment at BuildViewmodelCamera() for what went wrong
// the first time and why.
//
// ------------------------------------------------------------------------
// WHY FirstPersonHands GETS TURNED OFF, AND ONLY WHEN THIS ACTUALLY WORKS
//
// FirstPersonHands' whole job was faking first-person hands by bending the
// REAL skeleton toward the camera - the very trick this file replaces. Once
// a real viewmodel exists, leaving that running underneath it does nothing
// useful and keeps the old "teammate sees you clutching your own face" bug
// alive for no reason.
//
// So it is disabled, but only AFTER the trimmed mesh has actually loaded and
// a clone has actually been built - never unconditionally. If the mesh asset
// is missing (Stage 1 was never run, or its output was deleted),
// FirstPersonHands is left exactly as it was. A missing viewmodel and a
// disabled fallback at the same time would mean no hands at all, which is a
// worse failure than the one being fixed.
// ========================================================================

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FirstPersonViewmodel : MonoBehaviour
{
    [Header("Viewmodel - drag these while playing")]
    [Tooltip("Show the arms at all. Quickest A/B against no viewmodel.")]
    public bool visible = true;

    [Tooltip("Position of the arms relative to the camera. X right, Y up, " +
             "Z forward, in metres. " +
             "Y IS THE ONE THAT MATTERS AND IT IS VERY NEGATIVE ON PURPOSE. " +
             "The clone's origin is the CHARACTER'S ROOT - between the feet - " +
             "not its shoulders. The arms sit about 1.4m above that origin " +
             "inside the model, so placing the root at the camera puts the " +
             "hands a metre and a half over your head, which is exactly why " +
             "they were hanging down from the top of the screen. Dropping the " +
             "root by roughly that much brings them back down into frame.")]
    public Vector3 localPosition = new Vector3(0f, -1.05f, 0.35f);

    [Tooltip("Rotation of the arms relative to the camera, in degrees. " +
             "X tips the hands up or down, Y swings them left or right, Z " +
             "rolls them.")]
    public Vector3 localEulerAngles = Vector3.zero;

    [Tooltip("Uniform scale of the whole arm rig. Below 1 pulls the hands " +
             "in and makes them read as further away.")]
    [Range(0.1f, 2f)] public float localScale = 0.7f;

    [Header("Per hand - fine placement")]
    [Tooltip("Extra offset for the LEFT hand only, in metres, applied after " +
             "the animation has posed it. For nudging one hand into frame " +
             "without moving the whole rig.")]
    public Vector3 leftHandOffset = Vector3.zero;

    [Tooltip("Extra offset for the RIGHT hand only, in metres.")]
    public Vector3 rightHandOffset = Vector3.zero;

    [Tooltip("Pushes BOTH hands apart along the camera's right axis, in " +
             "metres. Positive widens the stance, negative brings them " +
             "together in front of you.")]
    public float handSpread = 0f;

    [Tooltip("Pushes BOTH hands forward along the camera's forward axis, in " +
             "metres. The single knob for 'reaching further out' without " +
             "touching the rig's own position.")]
    public float handReach = 0f;

    [Header("Only show the hands when they are doing something")]
    [Tooltip("Keep the arms out of sight until you actually use them, the way " +
             "We Were Here Together does it - hands are an interaction, not " +
             "scenery you stare at for the whole game. " +
             "OFF BY DEFAULT while the placement is being dialled in, because " +
             "you cannot position something that is only on screen for half a " +
             "second. The hide behaviour is built and waiting; switch it on " +
             "once the arms sit where you want them.")]
    public bool showOnlyWhenBusy = false;

    [Tooltip("Where the arms rest while idle, as an offset from their normal " +
             "position. Straight down by default, so they lower out of frame " +
             "and rise back into it rather than blinking on and off.")]
    public Vector3 hiddenOffset = new Vector3(0f, -0.45f, 0f);

    [Tooltip("Seconds for the arms to rise into view or lower back out.")]
    public float raiseTime = 0.22f;

    [Tooltip("Seconds the hands stay up after an action finishes. Stops them " +
             "dropping between two quick actions - grabbing one thing and then " +
             "another should not lower and raise them twice.")]
    public float holdAfter = 0.6f;

    [Header("Camera")]
    [Tooltip("Field of view of the dedicated viewmodel camera, in degrees.")]
    public float fieldOfView = 60f;

    [Tooltip("Near clip of the viewmodel camera. Can be very small - its " +
             "culling mask contains nothing but the clone, so there is no " +
             "level geometry to clip against.")]
    public float nearClip = 0.01f;

    [Header("Settings asset")]
    [Tooltip("The values above are only a FALLBACK. When a " +
             "FirstPersonViewmodelSettings asset exists in a Resources folder " +
             "it wins, and that is the one to edit - it survives leaving Play " +
             "mode, which this runtime object cannot.")]
    public FirstPersonViewmodelSettings settings;

    bool settingsChecked;

    /// <summary>
    /// Pull the asset's values in, every frame.
    ///
    /// EVERY FRAME so that dragging a slider on the asset during play still
    /// updates the game instantly - the live tuning that made Step 4 worth
    /// doing - while the value itself lives in a file that Unity saves.
    /// Reading once at startup would give persistence and lose the immediacy.
    /// </summary>
    void PullSettings()
    {
        if (settings == null && !settingsChecked)
        {
            settingsChecked = true;
            settings = Resources.Load<FirstPersonViewmodelSettings>(
                FirstPersonViewmodelSettings.ResourceName);

            Report(settings != null
                ? "using settings asset - edit it in the Project window, it persists."
                : "no FirstPersonViewmodelSettings asset found. Run SAFE DEPOSIT > " +
                  "Player > Create Viewmodel Settings Asset, or values will be lost " +
                  "every time you leave Play mode.", settings == null);
        }

        if (settings == null) return;

        visible = settings.visible;
        localPosition = settings.localPosition;
        deriveHeightFromEye = settings.deriveHeightFromEye;
        handsBelowEye = settings.handsBelowEye;
        localEulerAngles = settings.localEulerAngles;
        localScale = settings.localScale;
        leftHandOffset = settings.leftHandOffset;
        rightHandOffset = settings.rightHandOffset;
        handSpread = settings.handSpread;
        handReach = settings.handReach;
        pushReach = settings.pushReach;
        pushWindBack = settings.pushWindBack;
        pushSpread = settings.pushSpread;
        pushDrop = settings.pushDrop;
        showOnlyWhenBusy = settings.showOnlyWhenBusy;
        hiddenOffset = settings.hiddenOffset;
        raiseTime = settings.raiseTime;
        holdAfter = settings.holdAfter;
        followBodyAnimation = settings.followBodyAnimation;

        if (vmCam != null)
        {
            vmCam.fieldOfView = settings.fieldOfView;
            vmCam.nearClipPlane = settings.nearClip;
        }
    }

    FirstPersonCamera fpCam;
    Camera mainCam;
    Camera vmCam;
    Transform anchor;
    Transform clone;
    LocalFirstPersonBodyCull cull;   // on the REAL body, to ask about third person
    FirstPersonHands realHands;      // on the REAL body, turned off once we work
    PlayerCarry realCarry;           // holding something
    PlayerPush realPush;             // mid-shove
    PlayerPushArms realPushArms;     // the world-space shove, read for its TIMING only
    ViewmodelArmsIK armsIK;          // on the CLONE, for per-hand offsets

    [Header("Height")]
    public bool deriveHeightFromEye = true;
    public float handsBelowEye = 0.42f;

    /// <summary>
    /// How far the hands sit above the rig's own origin, at scale 1, measured
    /// off the actual skeleton once when the clone is built.
    ///
    /// Measured rather than assumed, because "the arms are about 1.4m up" was
    /// a guess about THIS model that would silently become wrong the day it is
    /// replaced - and it is already being multiplied by a scale that changes.
    /// </summary>
    float rigArmHeight = -1f;

    [Header("Push")]
    public float pushReach = 0.38f;
    public float pushWindBack = 0.12f;
    public float pushSpread = 0.09f;
    public float pushDrop = 0.05f;

    /// <summary>0 hidden, 1 fully raised. Eased, never snapped.</summary>
    float raised;

    /// <summary>When the hands were last doing something, for holdAfter.</summary>
    float lastBusy = -999f;

    [Header("Animation")]
    [Tooltip("Drive the viewmodel arms from the SAME animator parameters as " +
             "your real body, so they swing when you walk and breathe when you " +
             "stand still instead of holding one frozen pose. " +
             "The two skeletons stay separate - this copies the INPUTS, not the " +
             "pose, so the arms can still be given their own reach and grab " +
             "poses later without the real body having to share them.")]
    public bool followBodyAnimation = true;

    Animator realAnim;    // on the REAL body
    Animator cloneAnim;   // on the viewmodel arms

    // Cached because Animator.parameters ALLOCATES a fresh array every time it
    // is read - fine once, garbage every frame.
    AnimatorControllerParameter[] cloneParams;

    Mesh armsMesh;
    bool armsMeshChecked;

    float waiting;
    bool warnedNoTarget;

    /// <summary>
    /// One line, prefixed, so this system can be found in the Editor log
    /// without a screenshot. Everything here is once-per-event, never
    /// per-frame.
    /// </summary>
    static void Report(string what, bool bad = false)
    {
        if (bad) Debug.LogWarning("[Viewmodel] " + what);
        else Debug.Log("[Viewmodel] " + what);
    }

    Transform target;   // the body we last cloned, so a respawn re-clones

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // AfterSceneLoad fires on the very first load and on every reload
        // between rounds - RunManager rebuilds this same scene rather than
        // loading a different one. Re-running each time is correct: the old
        // camera and the old clone died with the scene, and a fresh one is
        // needed for whatever body spawns next.
        // Asked of the SCENE rather than remembered in a static. A static
        // "booted" flag survives entering Play mode when Reload Domain is
        // switched off, so a second session would find it already true and
        // destroy its own instance on the first frame - a viewmodel that
        // works once and never again, with nothing logged to say why.
        if (Object.FindFirstObjectByType<FirstPersonViewmodel>() != null) return;

        var go = new GameObject("~FirstPersonViewmodel");

        // DontSave, not HideAndDontSave - visible and selectable in the
        // Hierarchy while playing, because every number on it is meant to be
        // dragged and watched, not guessed blind. Still never saved into the
        // scene file.
        go.hideFlags = HideFlags.DontSave;
        go.AddComponent<FirstPersonViewmodel>();
    }


    void Update()
    {
        PullSettings();

        // ---- FIND THE CAMERA, THEN FIND ITS TARGET. NEITHER IS CACHED PAST
        //      A FAILED ATTEMPT. ----
        if (fpCam == null)
        {
            fpCam = Object.FindFirstObjectByType<FirstPersonCamera>();
            if (fpCam == null) return;

            mainCam = fpCam.GetComponent<Camera>();

            if (mainCam == null)
            {
                Report("FirstPersonCamera has no Camera component beside it - cannot " +
                       "build a viewmodel camera.", true);
                enabled = false;
                return;
            }

            BuildViewmodelCamera();
        }

        // ---- SILENCE WAS THE BUG IN THE DEBUGGING, NOT IN THE CODE ----
        //
        // Every failure path here used to log and the SUCCESS path used to
        // say nothing, so "it worked" and "it returned early on frame one and
        // never spoke again" produced identical console output: none. That is
        // exactly the state this was found in - mesh present, layer present,
        // no errors, no viewmodel.
        //
        // Waiting for a body is normal for a few frames while the local
        // player spawns, so it is only worth complaining about once it has
        // gone on long enough to mean something is actually wrong.
        if (fpCam.target == null)
        {
            waiting += Time.deltaTime;

            if (waiting > 5f && !warnedNoTarget)
            {
                warnedNoTarget = true;
                Report("FirstPersonCamera has had no target for 5 seconds - no local " +
                       "player body to clone arms from, so nothing will appear.", true);
            }

            return;
        }

        if (fpCam.target != target)
        {
            target = fpCam.target;
            cull = target.GetComponent<LocalFirstPersonBodyCull>();
            realHands = target.GetComponentInChildren<FirstPersonHands>(true);
            realCarry = target.GetComponent<PlayerCarry>();
            realPush = target.GetComponent<PlayerPush>();
            realPushArms = target.GetComponentInChildren<PlayerPushArms>(true);
            Rebuild();
        }

        if (clone == null) return;

        // Hidden in third person - the viewmodel camera sits at the MAIN
        // camera's position, and in third person that is three metres behind
        // the character, which would show the arms floating in mid-air for
        // no reason.
        bool firstPerson = cull == null || !cull.ThirdPerson;

        if (vmCam != null) vmCam.enabled = firstPerson && visible;

        // Third person exists to LOOK at your own character, so the body has
        // to come back the moment the camera pulls away from it - and the
        // arms have to go, since they are a first-person illusion sitting at
        // a camera that is now three metres behind you.
        if (cull != null) cull.HideBodyFromOwnCamera(firstPerson);

        if (followBodyAnimation) MirrorAnimation();

        ApplyPlacement();
    }

    /// <summary>
    /// Push the inspector values onto the rig EVERY FRAME rather than once at
    /// build time.
    ///
    /// That is the whole point of this step: these numbers were guessed blind
    /// twice and both guesses were wrong, so they have to be draggable while
    /// the game is running and answer immediately. Assigning once in Rebuild
    /// would mean re-entering Play mode to see every change - which is how the
    /// first two guesses survived as long as they did.
    /// </summary>
    void ApplyPlacement()
    {
        if (clone == null) return;

        // ---- RAISE THEM ONLY WHEN THEY ARE DOING SOMETHING ----
        float want = !showOnlyWhenBusy || HandsBusy() ? 1f : 0f;

        if (want > 0.5f) lastBusy = Time.time;
        else if (Time.time - lastBusy < holdAfter) want = 1f;   // still in the hold

        raised = Mathf.MoveTowards(raised, want,
                                   raiseTime <= 0f ? 1f : Time.deltaTime / raiseTime);

        // Eased so they arrive and leave softly rather than sliding linearly.
        float k = raised * raised * (3f - 2f * raised);

        // Y solved from the measurement rather than taken from the slider,
        // so changing scale or the camera's eye height corrects itself.
        Vector3 place = localPosition;

        if (deriveHeightFromEye && rigArmHeight > 0f)
            place.y = -handsBelowEye - rigArmHeight * localScale;

        // ---- THE SHOVE MOVES THE WHOLE RIG, NOT THE HAND BONES ----
        //
        // This was setting hand bone POSITIONS directly, which is the classic
        // skinned-mesh mistake: a hand bone is a child of the forearm, so
        // moving it alone does not move the arm - it drags the hand away from
        // the wrist and the mesh stretches to span the gap. That is the
        // "weird animation like stretching", and it is the reason the hands
        // appeared to fly upward too: a stretched limb has to go somewhere.
        //
        // Moving a hand properly in a skinned rig means IK - solving the
        // shoulder and elbow so the whole arm follows. But for a shove seen
        // from your own eyes there is nothing to solve FOR: no target, no
        // contact point, no reach problem. The whole viewmodel lunging forward
        // reads as a push and cannot stretch anything, because every bone
        // keeps its relationship to every other bone.
        float t = realPush != null ? realPush.PushProgress : -1f;

        if (t >= 0f)
        {
            float windPart = realPushArms != null ? realPushArms.windPart : 0.3f;
            float thrustPart = realPushArms != null ? realPushArms.thrustPart : 0.52f;

            float p = PushCurve(t, windPart, thrustPart);

            place += Vector3.forward * (pushReach * p)
                   + Vector3.down * (pushDrop * Mathf.Max(0f, p));
        }

        clone.localPosition = place + hiddenOffset * (1f - k);

        // Per-hand nudges go through IK so the whole arm follows the hand
        // rather than the wrist tearing away from it.
        if (armsIK != null)
        {
            armsIK.leftOffset = leftHandOffset;
            armsIK.rightOffset = rightHandOffset;
            armsIK.spread = handSpread;
            armsIK.reach = handReach;
            armsIK.space = anchor;
        }
        clone.localRotation = Quaternion.Euler(localEulerAngles);
        clone.localScale = Vector3.one * Mathf.Max(0.01f, localScale);

        if (vmCam != null && !visible) vmCam.enabled = false;
    }

    /// <summary>
    /// Is the player using their hands for anything right now?
    ///
    /// Every hand action in the game resolves to one of four signals, and
    /// three of the four are read from the ANIMATOR rather than from a list of
    /// keypresses - so an action added later is covered without touching this.
    ///
    ///   arms layer has a clip   pick up, stow, use, and the carry pose. That
    ///                           masked layer exists precisely to mean "the
    ///                           arms are busy", so asking whether it has
    ///                           anything to play IS the question.
    ///   carrying                belt and braces for the sustained hold, in
    ///                           case the carry pose is ever moved off that
    ///                           layer.
    ///   mid-shove               push is IK, not a clip, so it has no layer to
    ///                           show up on and has to be asked directly.
    ///   emote playing           wave, point, dance, clap, salute. Those are
    ///                           FULL-BODY on the base layer, tagged FreeArms -
    ///                           the same tag FirstPersonHands and
    ///                           ProceduralLegsIK already read, so all three
    ///                           agree about what an emote is.
    /// </summary>
    bool HandsBusy()
    {
        if (realCarry != null && realCarry.IsCarrying) return true;

        if (realPush != null && realPush.PushProgress >= 0f) return true;

        if (realAnim == null || realAnim.runtimeAnimatorController == null) return false;

        const int ArmsLayer = 1;

        if (realAnim.layerCount > ArmsLayer &&
            (realAnim.GetCurrentAnimatorClipInfoCount(ArmsLayer) > 0 ||
             realAnim.GetNextAnimatorClipInfoCount(ArmsLayer) > 0))
            return true;

        if (realAnim.GetCurrentAnimatorStateInfo(0).IsTag("FreeArms") ||
            realAnim.GetNextAnimatorStateInfo(0).IsTag("FreeArms"))
            return true;

        return false;
    }

    /// <summary>
    /// Copy the real body's animator parameters AND layer weights onto the
    /// arms.
    ///
    /// Floats, ints and bools only. TRIGGERS are deliberately not forwarded:
    /// there is no way to read whether one is currently set, and a one-shot
    /// fired twice - once on each animator - is not the same as one fired on
    /// both. Pickup, stow, use and emote are all triggers, so those arrive
    /// with the interaction work rather than here, where they would be
    /// guesswork.
    ///
    /// This copies INPUTS, never the pose. The two skeletons stay free to
    /// differ, which is the entire reason there are two of them.
    /// </summary>
    void MirrorAnimation()
    {
        if (realAnim == null || cloneAnim == null || cloneParams == null) return;
        if (realAnim.runtimeAnimatorController == null) return;
        if (cloneAnim.runtimeAnimatorController == null) return;

        // ---- LAYER WEIGHTS TOO, NOT JUST PARAMETERS ----
        //
        // The clone carries the same controller as the real body but NOT
        // PlayerAnimatorDriver - that lives on the Player root, and the clone
        // is parented to a camera. So nothing was setting the clone's LAYER
        // WEIGHTS, and the masked Arms layer sat at its authored default of 1
        // over an empty state: the exact bind-pose override that was fixed on
        // the real body, reproduced perfectly on the copy.
        int layers = Mathf.Min(cloneAnim.layerCount, realAnim.layerCount);

        for (int i = 0; i < layers; i++)
            cloneAnim.SetLayerWeight(i, realAnim.GetLayerWeight(i));

        for (int i = 0; i < cloneParams.Length; i++)
        {
            var prm = cloneParams[i];

            switch (prm.type)
            {
                case AnimatorControllerParameterType.Float:
                    cloneAnim.SetFloat(prm.nameHash, realAnim.GetFloat(prm.nameHash));
                    break;

                case AnimatorControllerParameterType.Int:
                    cloneAnim.SetInteger(prm.nameHash, realAnim.GetInteger(prm.nameHash));
                    break;

                case AnimatorControllerParameterType.Bool:
                    cloneAnim.SetBool(prm.nameHash, realAnim.GetBool(prm.nameHash));
                    break;
            }
        }
    }

    /// <summary>
    /// How far out the shove is, 0 to 1, dipping negative during the wind-up.
    ///
    /// Zero at both ends, which is the contract that keeps the hands starting
    /// and finishing exactly where they rest - the same shape PlayerPushArms
    /// uses, and the same reason: anything else leaves a step at one end.
    /// </summary>
    float PushCurve(float t, float windPart, float thrustPart)
    {
        float back = -Mathf.Abs(pushWindBack) / Mathf.Max(0.01f, pushReach);

        if (t < windPart)
            return Mathf.Lerp(0f, back, Smooth(t / Mathf.Max(0.001f, windPart)));

        float rest = (t - windPart) / Mathf.Max(0.001f, 1f - windPart);

        if (rest < thrustPart)
            return Mathf.Lerp(back, 1f, Smooth(rest / thrustPart));

        return Mathf.Lerp(1f, 0f, Smooth((rest - thrustPart) / (1f - thrustPart)));
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    // --------------------------------------------------------------------
    // THE SECOND CAMERA
    // --------------------------------------------------------------------

    void BuildViewmodelCamera()
    {
        int layer = LayerMask.NameToLayer(ViewmodelLayerName);

        if (layer < 0)
        {
            Debug.LogError("[Viewmodel] Layer '" + ViewmodelLayerName + "' does not " +
                           "exist. Run SAFE DEPOSIT > Player > Setup First-Person " +
                           "Viewmodel Layer once from the editor menu, then re-enter " +
                           "Play mode. The viewmodel cannot show without it.");
            return;
        }

        Report("layer '" + ViewmodelLayerName + "' is index " + layer +
               ", building the overlay camera.");

        var camGo = new GameObject("~ViewmodelCamera");
        camGo.transform.SetParent(mainCam.transform, false);

        vmCam = camGo.AddComponent<Camera>();
        vmCam.cullingMask = 1 << layer;
        vmCam.nearClipPlane = nearClip;
        vmCam.fieldOfView = fieldOfView;

        // ---- THIS PROJECT RENDERS THROUGH URP, WHICH IGNORES ALL OF THAT
        //      UNLESS THE CAMERA IS REGISTERED INTO A STACK ----
        //
        // A camera in the Editor gets a UniversalAdditionalCameraData
        // attached automatically the moment URP is the active pipeline. One
        // created purely in code does not, and URP's fallback for a camera in
        // that state is to render it as its own independent BASE camera -
        // clearing to Unity's default camera blue rather than drawing on top
        // of anything. That was the flat blue screen from the previous round.
        //
        // The real mechanism: a camera is either Base (clears the screen -
        // your Main Camera already is one) or Overlay (draws on top of a Base
        // camera's result, never clears anything itself). Overlay cameras do
        // not free-float - they are added to a Base camera's OWN stack.
        var vmData = camGo.AddComponent<UniversalAdditionalCameraData>();
        vmData.renderType = CameraRenderType.Overlay;

        var baseData = mainCam.GetComponent<UniversalAdditionalCameraData>();
        if (baseData == null) baseData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        baseData.cameraStack.Add(vmCam);

        // The main camera must NOT also draw this layer - an Overlay camera
        // still only draws what its OWN culling mask names, but the Base
        // camera underneath it would otherwise draw the same clone a second
        // time, at whatever size it happens to be relative to the WORLD
        // camera rather than the viewmodel one.
        mainCam.cullingMask &= ~(1 << layer);

        // ---- AND STOP DRAWING YOUR OWN BODY ----
        //
        // The other half of the same idea. The viewmodel camera draws ONLY
        // the Viewmodel layer; the main camera draws everything EXCEPT
        // LocalBody. Between them: world and loot from the main camera, arms
        // from the overlay, and none of your own character.
        //
        // Only this machine's main camera is touched, and only the local
        // body's renderers are ever put on that layer - so a teammate's
        // camera, which was never told the layer means anything, keeps
        // drawing your full character normally.
        int bodyLayer = LayerMask.NameToLayer(LocalFirstPersonBodyCull.BodyLayerName);

        if (bodyLayer >= 0) mainCam.cullingMask &= ~(1 << bodyLayer);
        else Report("layer '" + LocalFirstPersonBodyCull.BodyLayerName + "' does not exist " +
                    "- run the layer setup menu item. Your own body will stay visible.", true);

        anchor = camGo.transform;
    }

    // MUST MATCH ViewmodelLayerSetup.LayerName exactly. They cannot share one
    // constant - that editor tool lives in an Editor-only assembly, which is
    // stripped from the actual game and cannot be referenced from here.
    public const string ViewmodelLayerName = "Viewmodel";

    // --------------------------------------------------------------------
    // THE TRIMMED MESH
    // --------------------------------------------------------------------

    /// <summary>
    /// Loads Stage 1's output once, remembers whether it worked, and never
    /// spams the console retrying a load that already failed this session.
    /// </summary>
    bool TryLoadArmsMesh()
    {
        if (armsMeshChecked) return armsMesh != null;
        armsMeshChecked = true;

        armsMesh = Resources.Load<Mesh>("PlayerArmsViewmodel");

        if (armsMesh != null)
        {
            Report("arms mesh loaded: " + armsMesh.vertexCount + " verts, " +
                   armsMesh.subMeshCount + " submeshes, " +
                   armsMesh.bindposes.Length + " bindposes.");
        }

        if (armsMesh == null)
        {
            Debug.LogWarning("[Viewmodel] Assets/_Project/Resources/PlayerArmsViewmodel.asset " +
                             "not found. Run SAFE DEPOSIT > Player > Build First-Person Arms " +
                             "Mesh once. Your real hands (FirstPersonHands) are left running " +
                             "until this exists.");
        }

        return armsMesh != null;
    }

    // --------------------------------------------------------------------
    // THE CLONE
    // --------------------------------------------------------------------

    void Rebuild()
    {
        if (clone != null) Destroy(clone.gameObject);
        clone = null;

        // Real hands are the fallback until proven otherwise on THIS body.
        // A respawn re-runs Rebuild with a fresh target, so this has to be
        // re-decided every time rather than trusted from the last body.
        if (realHands != null) realHands.enabled = true;

        if (anchor == null)
        {
            Report("no viewmodel camera was built, so there is nothing to hang arms " +
                   "on - the layer step above failed.", true);
            return;
        }

        if (!TryLoadArmsMesh()) return;      // Stage 1 not run; leave real hands alone

        var visual = target.Find("PlayerModel_FBX_VISUAL");
        if (visual == null)
        {
            Debug.LogWarning("[Viewmodel] Local player has no " +
                             "PlayerModel_FBX_VISUAL child - nothing to clone.");
            return;
        }

        // ---- CLONE THE SKELETON, NOT THE CHARACTER ----
        //
        // Instantiate still copies the whole hierarchy - bones, Animator,
        // the original SkinnedMeshRenderer - because that is the only way to
        // get a skeleton whose bone indices line up with the trimmed mesh's
        // bindposes (see FirstPersonArmsMeshBuilder's header for why that
        // alignment is what makes the swap below safe). What makes this NOT
        // a duplicated body is the very next line: the renderer's mesh is
        // replaced before this is ever shown.
        var go = Instantiate(visual.gameObject, anchor);
        go.name = "ArmsViewmodel";

        // Placement is applied every frame by ApplyPlacement so the sliders
        // work live; this just stops the rig existing at the origin for the
        // one frame before that runs.
        var t = go.transform;
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(localEulerAngles);
        t.localScale = Vector3.one * Mathf.Max(0.01f, localScale);

        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("[Viewmodel] Clone has no SkinnedMeshRenderer to swap the arms " +
                           "mesh onto - aborting rather than showing the full body.");
            Destroy(go);
            return;
        }

        // THE SWAP. Same bindposes, same bone-index-per-vertex layout as the
        // source mesh (FirstPersonArmsMeshBuilder copied both unchanged), and
        // Instantiate has already remapped smr.bones to THIS clone's own
        // Transforms - so this is the entire fix, one assignment.
        smr.sharedMesh = armsMesh;

        StripForViewmodel(go);

        // Per-hand placement needs IK, and Unity only delivers OnAnimatorIK to
        // components sharing a GameObject with the Animator - so it has to be
        // added HERE, to the thing that did not exist until a moment ago.
        armsIK = go.GetComponent<ViewmodelArmsIK>();
        if (armsIK == null) armsIK = go.AddComponent<ViewmodelArmsIK>();
        armsIK.space = anchor;
        SetLayerRecursively(go, LayerMask.NameToLayer(ViewmodelLayerName));

        clone = t;

        // ---- MEASURE WHERE THE ARMS ACTUALLY ARE IN THIS RIG ----
        //
        // Solves the problem that has produced three wrong Y offsets in a row.
        // Where the hands land on screen is eyeOffset.y + localPosition.y +
        // (arm height in the rig x scale), and every one of those was being
        // guessed independently - so the scene quietly having eyeOffset.y at
        // 1.25 instead of the script's 1.60 put the hands 35cm higher than any
        // of the guesses expected, which is why they kept coming out at the
        // top of the frame.
        //
        // Measured off the real skeleton, at scale 1, so the arithmetic below
        // can solve for the offset instead of anybody dialling for it.
        var handBone = cloneAnim != null && cloneAnim.isHuman
            ? cloneAnim.GetBoneTransform(HumanBodyBones.LeftHand)
            : null;

        if (handBone != null && localScale > 0.001f)
        {
            rigArmHeight = (handBone.position.y - t.position.y) / localScale;
            Report("rig measured: hands sit " + rigArmHeight.ToString("0.00") +
                   "m above the arms rig origin at scale 1.");
        }

        // ---- THE ARMS LISTEN TO THE SAME ANIMATION AS THE BODY ----
        //
        // The clone has its own Animator with the same controller, but
        // nothing was ever setting its parameters - PlayerAnimatorDriver
        // lives on the real Player root and drives the real body only. So the
        // blend tree sat at MoveX 0 / MoveZ 0 forever and the arms held one
        // pose no matter what the player did.
        //
        // Mirroring the parameters is what makes them move, and it is
        // deliberately the PARAMETERS rather than the pose: the two skeletons
        // stay independent, which is the whole point of having two. The arms
        // can be given their own reach and grab states later without the real
        // body being dragged into them.
        cloneAnim = go.GetComponent<Animator>();
        if (cloneAnim == null) cloneAnim = go.GetComponentInChildren<Animator>();

        realAnim = target.GetComponentInChildren<Animator>();

        cloneParams = cloneAnim != null && cloneAnim.runtimeAnimatorController != null
            ? cloneAnim.parameters
            : null;

        // Confirmed working on this body - the real skeleton no longer needs
        // to be bent toward the camera to fake hands.
        if (realHands != null) realHands.enabled = false;

        // And stop YOUR camera drawing your own body, now that there are arms
        // to replace it with. Only now - doing it earlier or unconditionally
        // would leave you with no hands at all whenever this failed.
        //
        // By LAYER, not by scaling bones. The skeleton keeps its real pose,
        // keeps animating, keeps replicating; the local camera is simply not
        // told to draw it. A teammate sees the complete character, unchanged.
        if (cull != null) cull.HideBodyFromOwnCamera(true);

        Report("BUILT on '" + target.name + "'. FirstPersonHands " +
               (realHands != null ? "disabled" : "NOT FOUND (old hands may still show)") +
               ". If the arms are not visible, they are in the wrong place rather than " +
               "missing - select ~FirstPersonViewmodel and move Local Position.");
    }

    /// <summary>
    /// Remove everything on the clone that assumes it is the real body.
    ///
    /// FirstPersonHands, ProceduralLegsIK and PlayerPushArms all live on
    /// PlayerModel_FBX_VISUAL and all reach UPWARD via GetComponentInParent
    /// to find PlayerMotor, PlayerHealth, PlayerPush. On this clone that
    /// search finds the viewmodel camera instead and comes back null, so
    /// each would sit there doing nothing every frame - harmless, but a
    /// wasted OnAnimatorIK call for nothing is still worth not having.
    /// </summary>
    void StripForViewmodel(GameObject go)
    {
        DestroyAllOfType<FirstPersonHands>(go);
        DestroyAllOfType<ProceduralLegsIK>(go);
        DestroyAllOfType<PlayerPushArms>(go);

        // Defensive: this is a visual FBX child and should carry none of
        // these, but a clone that could physically collide with the world or
        // that Unity tried to network would be a much stranger bug to chase
        // later than a null check now.
        DestroyAllOfType<Collider>(go);
        DestroyAllOfType<Rigidbody>(go);
        DestroyAllOfType<Unity.Netcode.NetworkBehaviour>(go);

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    static void DestroyAllOfType<T>(GameObject go) where T : Component
    {
        foreach (var c in go.GetComponentsInChildren<T>(true))
            Destroy(c);
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
