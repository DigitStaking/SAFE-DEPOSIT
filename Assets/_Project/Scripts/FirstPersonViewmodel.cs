// FirstPersonViewmodel.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/FirstPersonViewmodel.cs
// Goes on: nothing. Starts itself, like VoiceMic.
//
// ========================================================================
// STEP 1 - A SEPARATE PAIR OF ARMS, VISIBLE ONLY TO YOU.
//
// "I want TWO different representations of the player." This is the first
// of the two. FirstPersonHands bends your REAL skeleton toward the camera -
// one skeleton doing two jobs, which is why a teammate can catch you
// clutching your own face. This is a second, throwaway copy of that
// skeleton that only you ever render.
//
// Nothing here is wired to interactions yet. It clones, shrinks, positions,
// and idles. Grab, loot and use poses are the next step, once this one is
// confirmed working - so today it will look like a small idle figure near
// the bottom of your view, not reaching hands. That is expected.
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
// the first time and why. Overlay cameras never clear anything; they draw
// only their own culling mask on top of whatever the Base camera already
// produced, which is what makes clipping through nearby geometry structurally
// impossible rather than just unlikely - there is no world geometry in this
// camera's frustum to clip against in the first place.
//
// ------------------------------------------------------------------------
// WHY THE CLONE'S HEAD AND LEGS CAN BE HIDDEN AND THE TORSO CANNOT
//
// LocalFirstPersonBodyCull's trick - shrink a bone to hide what is skinned
// to it - works for the head and the legs because they hang off the SIDE of
// the skeleton: Head and Neck are one branch under the chest, UpperLeg is a
// separate branch under the hips. Shrinking one does not touch the others.
//
// The arms are not a side branch. In this rig, Shoulder -> UpperArm ->
// ForeArm -> Hand all hang OFF THE CHEST BONE, exactly like the head does.
// Shrinking the chest to hide the torso would carry the entire arm down
// with it, because a child's world position is its parent's transform times
// its own - a near-zero parent collapses everything beneath it to one point.
//
// So this hides the head and legs (both real wins - no chin in your own
// view, no legs sprouting out of your own chest) and leaves a small torso
// with arms attached, rather than promising isolated hands this system
// cannot deliver. Real isolated arms need their own geometry, cut free of
// the body in a modelling tool - which is exactly the Block 8 art pass
// FirstPersonHands has been pointing at all along.
// ========================================================================

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FirstPersonViewmodel : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Local position of the cloned body, relative to the viewmodel " +
             "camera. " +
             "SIZE ON SCREEN IS DISTANCE, NOT SCALE. The first numbers here " +
             "put the clone 0.35m out at half size and it filled the entire " +
             "screen with a chest, because a person-sized object that close " +
             "is enormous regardless of what the scale slider says - the two " +
             "fight each other. Z is now the one to move first if it is still " +
             "too big: push it further away before shrinking it further, or " +
             "shrinking eventually produces a doll rather than a small figure.")]
    public Vector3 localPosition = new Vector3(0f, -0.65f, 0.95f);

    [Tooltip("Local rotation of the cloned body, in degrees.")]
    public Vector3 localEulerAngles = Vector3.zero;

    [Tooltip("Uniform scale. Small enough that a whole idle figure reads as " +
             "a viewmodel rather than a shrunken person standing in front of " +
             "you.")]
    public float localScale = 0.3f;

    [Header("What to hide on the clone")]
    [Tooltip("Shrink the head bone so it cannot be seen. Same technique as " +
             "LocalFirstPersonBodyCull, applied to the clone instead of your " +
             "real body.")]
    public bool hideHead = true;

    [Tooltip("Also shrink the neck.")]
    public bool hideNeck = true;

    [Tooltip("Shrink both legs. Fine to hide - unlike the arms, legs are a " +
             "separate branch off the hips, so this cannot affect anything " +
             "else.")]
    public bool hideLegs = true;

    [Header("Camera")]
    [Tooltip("Field of view of the dedicated viewmodel camera, in degrees.")]
    public float fieldOfView = 60f;

    [Tooltip("Near clip of the viewmodel camera. Can be very small - its " +
             "culling mask contains nothing but the clone, so there is no " +
             "level geometry to clip against.")]
    public float nearClip = 0.01f;

    static bool booted;

    FirstPersonCamera fpCam;
    Camera mainCam;
    Camera vmCam;
    Transform anchor;
    Transform clone;
    Animator cloneAnim;
    LocalFirstPersonBodyCull cull;   // on the REAL body, to ask about third person

    Transform target;   // the body we last cloned, so a respawn re-clones

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // AfterSceneLoad fires on the very first load and on every reload
        // between rounds - RunManager rebuilds this same scene rather than
        // loading a different one. Re-running each time is correct: the old
        // camera and the old clone died with the scene, and a fresh one is
        // needed for whatever body spawns next.
        //
        // Not guarded with a "once ever" flag for that reason - booted below
        // only stops TWO of these existing at once within the same load.
        var go = new GameObject("~FirstPersonViewmodel");

        // DontSave, not HideAndDontSave. VoiceMic hides itself completely
        // because nobody ever needs its inspector. This is the opposite case:
        // every number on it is something to drag a slider on and watch,
        // exactly like ProceduralLegs and PlayerPush all session - hiding it
        // from the Hierarchy would have meant reporting a screenshot, reading
        // a guess back, and repeating that for every number below, instead of
        // moving one slider and seeing the answer immediately.
        //
        // DontSave alone still keeps it out of the saved scene, which is the
        // part that actually matters - a debug object baked into Prototype.unity
        // would be a real bug.
        go.hideFlags = HideFlags.DontSave;
        go.AddComponent<FirstPersonViewmodel>();
    }

    void Awake()
    {
        if (booted) { Destroy(gameObject); return; }
        booted = true;
    }

    void OnDestroy() => booted = false;

    void Update()
    {
        // ---- FIND THE CAMERA, THEN FIND ITS TARGET. NEITHER IS CACHED PAST
        //      A FAILED ATTEMPT. ----
        //
        // FirstPersonCamera itself does not bind to a body until one spawns
        // and claims local, so this has to be willing to keep asking rather
        // than giving up after one null result - the same reason
        // FirstPersonCamera polls for AdoptLocalPlayer instead of trying once.
        if (fpCam == null)
        {
            fpCam = Object.FindFirstObjectByType<FirstPersonCamera>();
            if (fpCam == null) return;

            mainCam = fpCam.GetComponent<Camera>();
            BuildViewmodelCamera();
        }

        if (fpCam.target == null) return;

        if (fpCam.target != target)
        {
            target = fpCam.target;
            cull = target.GetComponent<LocalFirstPersonBodyCull>();
            Rebuild();
        }

        if (clone == null) return;

        // Hidden in third person - the viewmodel camera sits at the MAIN
        // camera's position, and in third person that is three metres behind
        // the character, which would show the tiny arms floating in mid-air
        // for no reason.
        bool show = cull == null || !cull.ThirdPerson;
        if (vmCam != null) vmCam.enabled = show;
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

        var camGo = new GameObject("~ViewmodelCamera");
        camGo.transform.SetParent(mainCam.transform, false);

        vmCam = camGo.AddComponent<Camera>();
        vmCam.cullingMask = 1 << layer;
        vmCam.nearClipPlane = nearClip;
        vmCam.fieldOfView = fieldOfView;

        // ---- THIS PROJECT RENDERS THROUGH URP, WHICH IGNORES ALL OF THAT
        //      UNLESS THE CAMERA IS REGISTERED INTO A STACK ----
        //
        // clearFlags and depth are the LEGACY pipeline's answer to layering
        // two cameras, and they do nothing under URP - which is why the
        // result was not "the clone drawn over the world" but "the whole
        // world replaced by flat blue". A Camera added at runtime with
        // AddComponent has no UniversalAdditionalCameraData, and URP's
        // fallback for a camera in that state is to render it as its own
        // independent BASE camera - clearing to its own background (Unity's
        // default camera blue) rather than drawing on top of anything.
        //
        // A camera in the Editor gets that component attached automatically
        // the moment URP is the active pipeline. One created purely in code
        // does not, and there is no warning when it is missing - it just
        // quietly renders wrong.
        //
        // The actual URP mechanism: a camera is either Base (clears the
        // screen, the normal kind - your Main Camera already is one) or
        // Overlay (draws on top of a Base camera's result, never clears
        // anything itself). Overlay cameras do not free-float - they have to
        // be added to a Base camera's OWN camera stack, in the order they
        // should draw.
        var vmData = camGo.AddComponent<UniversalAdditionalCameraData>();
        vmData.renderType = CameraRenderType.Overlay;

        var baseData = mainCam.GetComponent<UniversalAdditionalCameraData>();
        if (baseData == null) baseData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        baseData.cameraStack.Add(vmCam);

        // The main camera must NOT also draw this layer - an Overlay camera
        // still only draws what its OWN culling mask names, but the Base
        // camera underneath it would otherwise draw the same clone a second
        // time, at whatever tiny size it happens to be relative to the WORLD
        // camera rather than the viewmodel one.
        mainCam.cullingMask &= ~(1 << layer);

        anchor = camGo.transform;
    }

    // MUST MATCH ViewmodelLayerSetup.LayerName exactly. They cannot share one
    // constant - that editor tool lives in an Editor-only assembly, which is
    // stripped from the actual game and cannot be referenced from here. Two
    // literals kept in sync by comment rather than by the compiler; this
    // project has already been bitten by exactly that shape of bug once
    // (PlayerPush's cooldown racing armTime), so it is written out plainly
    // in both files rather than trusted to memory.
    public const string ViewmodelLayerName = "Viewmodel";

    // --------------------------------------------------------------------
    // THE CLONE
    // --------------------------------------------------------------------

    void Rebuild()
    {
        if (clone != null) Destroy(clone.gameObject);
        clone = null;
        cloneAnim = null;

        if (anchor == null) return;   // layer setup failed; nothing to show

        var visual = target.Find("PlayerModel_FBX_VISUAL");
        if (visual == null)
        {
            Debug.LogWarning("[Viewmodel] Local player has no " +
                             "PlayerModel_FBX_VISUAL child - nothing to clone.");
            return;
        }

        var go = Instantiate(visual.gameObject, anchor);
        go.name = "ClonedArms";

        var t = go.transform;
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(localEulerAngles);
        t.localScale = Vector3.one * localScale;

        StripForViewmodel(go);
        SetLayerRecursively(go, LayerMask.NameToLayer(ViewmodelLayerName));

        clone = t;
        cloneAnim = go.GetComponent<Animator>();

        if (hideHead || hideNeck || hideLegs) ShrinkBones();
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

    void ShrinkBones()
    {
        if (cloneAnim == null || !cloneAnim.isHuman) return;

        const float shrink = 0.0001f;   // not exactly zero - see the note in
                                        // LocalFirstPersonBodyCull about
                                        // degenerate matrices

        if (hideHead) Shrink(HumanBodyBones.Head, shrink);
        if (hideNeck) Shrink(HumanBodyBones.Neck, shrink);

        if (hideLegs)
        {
            Shrink(HumanBodyBones.LeftUpperLeg, shrink);
            Shrink(HumanBodyBones.RightUpperLeg, shrink);
        }
    }

    void Shrink(HumanBodyBones bone, float amount)
    {
        var t = cloneAnim.GetBoneTransform(bone);
        if (t != null) t.localScale = Vector3.one * amount;
    }

    void LateUpdate()
    {
        // The Animator writes its pose in the animation update, which runs
        // BEFORE LateUpdate - so shrinking here, after it, is what stops the
        // idle clip putting the head back to full size every frame. Same
        // ordering reason LocalFirstPersonBodyCull's own shrink runs late.
        if (clone != null) ShrinkBones();
    }
}
