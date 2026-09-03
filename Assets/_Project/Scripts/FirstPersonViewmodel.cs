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
    [Header("Placement")]
    [Tooltip("Local position of the cloned arms, relative to the viewmodel " +
             "camera. " +
             "STARTING NUMBERS ONLY - this could not be previewed while " +
             "writing it, the same way the whole-body attempt could not. " +
             "Select ~FirstPersonViewmodel in the Hierarchy while playing and " +
             "drag these until they read like a normal FPS view of your own " +
             "arms, low and to the sides.")]
    public Vector3 localPosition = new Vector3(0f, -0.35f, 0.45f);

    [Tooltip("Local rotation of the cloned arms, in degrees.")]
    public Vector3 localEulerAngles = Vector3.zero;

    [Tooltip("Uniform scale. Arms-only geometry does not balloon the frame " +
             "the way a whole body did, so this can sit much closer to real " +
             "scale than the old attempt's 0.3 - start near 1 and adjust from " +
             "here rather than assuming it needs shrinking.")]
    public float localScale = 0.7f;

    [Header("Camera")]
    [Tooltip("Field of view of the dedicated viewmodel camera, in degrees.")]
    public float fieldOfView = 60f;

    [Tooltip("Near clip of the viewmodel camera. Can be very small - its " +
             "culling mask contains nothing but the clone, so there is no " +
             "level geometry to clip against.")]
    public float nearClip = 0.01f;

    FirstPersonCamera fpCam;
    Camera mainCam;
    Camera vmCam;
    Transform anchor;
    Transform clone;
    LocalFirstPersonBodyCull cull;   // on the REAL body, to ask about third person
    FirstPersonHands realHands;      // on the REAL body, turned off once we work

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
            Rebuild();
        }

        if (clone == null) return;

        // Hidden in third person - the viewmodel camera sits at the MAIN
        // camera's position, and in third person that is three metres behind
        // the character, which would show the arms floating in mid-air for
        // no reason.
        bool firstPerson = cull == null || !cull.ThirdPerson;

        if (vmCam != null) vmCam.enabled = firstPerson;

        // Third person exists to LOOK at your own character, so the body has
        // to come back the moment the camera pulls away from it - and the
        // arms have to go, since they are a first-person illusion sitting at
        // a camera that is now three metres behind you.
        if (cull != null) cull.HideBodyFromOwnCamera(firstPerson);
    }

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

        var t = go.transform;
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(localEulerAngles);
        t.localScale = Vector3.one * localScale;

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
        SetLayerRecursively(go, LayerMask.NameToLayer(ViewmodelLayerName));

        clone = t;

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
