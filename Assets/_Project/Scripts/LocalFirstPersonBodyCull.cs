// LocalFirstPersonBodyCull.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LocalFirstPersonBodyCull.cs
// Goes on: the Player root.
//
// ========================================================================
// HIDING YOUR OWN HEAD WITHOUT HIDING YOUR OWN BODY
//
// The camera sits inside the skull. Something has to go, or you spend the
// whole game looking at the inside of your own helmet.
//
// THE TRAP THIS SCRIPT USED TO FALL INTO
//
// A Renderer is enabled or disabled as a WHOLE. There is no way to switch
// off one submesh. Your character is a single SkinnedMeshRenderer -
// geometry_0.001 - carrying seven material slots: suit, skin, boots, belt,
// helmet, visor, and so on.
//
// The old version checked every material on a renderer and disabled the
// renderer if any of them looked like a head. One slot on that single mesh
// is the helmet, so the test passed, and Unity switched off the entire
// character - head, torso, arms, hands, legs. Hence: no hands, no body, an
// empty screen, and a Console line cheerfully reporting success.
//
// THE FIX: SHRINK THE BONE, DO NOT DISABLE THE RENDERER
//
// The head's vertices are skinned to the Head bone. Scale that bone to
// effectively zero and every vertex weighted to it collapses to a single
// point - mathematically present, visually gone. Every other vertex in the
// same mesh is untouched, because it is weighted to different bones.
//
// One line, no re-export, no material renaming, and it works on any rig
// with a Humanoid avatar. It is what most first-person games with a full
// body actually do.
//
// It has to run in LateUpdate, AFTER the Animator has written the pose,
// otherwise the animation puts the head back at full size every frame.
// ========================================================================

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(60)]      // after Animator and after FirstPersonHands
public class LocalFirstPersonBodyCull : MonoBehaviour
{
    [Header("Head")]
    [Tooltip("How small to squash the head bone. Not exactly zero - a zero " +
             "scale produces a degenerate matrix and some drivers render " +
             "garbage triangles instead of nothing.")]
    public float headShrink = 0.0001f;

    [Tooltip("Also shrink the neck. Turn on if you can still see a collar or " +
             "the top of the suit poking into view.")]
    public bool shrinkNeck = false;

    [Header("Body")]
    [Tooltip("Shrink the legs too. OFF by default: it does make looking " +
             "straight down cleaner, but it leaves a torso floating in mid-air " +
             "in the Scene view and in third person, which is alarming and " +
             "buys you very little.")]
    public bool hideLegs = false;

    [Tooltip("Stop your own body casting a shadow. Your headlamp sits ABOVE " +
             "your chest, so your torso throws a large moving blob onto the " +
             "floor directly in front of you - the dark shape in your " +
             "screenshot. Other players still see your shadow normally; this " +
             "only affects your own view.")]
    public bool hideOwnShadow = true;

    [Tooltip("If your model is split so the arms are their own object, put " +
             "part of that object's name here. When one is found, the rest of " +
             "the body is hidden outright and you get true arms-only first " +
             "person. Leave as is until you have split the mesh in Blender.")]
    public string armsRendererName = "arm";

    [Header("Separate head props")]
    [Tooltip("Renderers on their OWN GameObject whose name matches one of " +
             "these are disabled outright. Only applies to objects that are " +
             "not the main body mesh, so it can never blank the character.")]
    public string[] hideNameParts = { "helmet", "visor", "hair", "hat" };

    [Header("Third person check")]
    // P, requested 30 Aug so the PEAK-style locomotion can actually be looked
    // at. It moved twice while the voice keys were being decided - V went to
    // push-to-talk, U to the radio - and it is not going back onto either of
    // those. P is free, memorable, and next to nothing.
    public KeyCode thirdPersonToggle = KeyCode.P;   // shown for reference
    public float thirdPersonDistance = 3.2f;
    public float thirdPersonHeight = 1.4f;

    readonly List<Renderer> hidden = new List<Renderer>();
    Animator anim;
    Transform headBone, neckBone, legL, legR;
    Renderer bodyRenderer, armsRenderer;
    Vector3 headScale = Vector3.one, neckScale = Vector3.one;
    Vector3 legLScale = Vector3.one, legRScale = Vector3.one;
    UnityEngine.Rendering.ShadowCastingMode bodyShadowMode =
        UnityEngine.Rendering.ShadowCastingMode.On;
    bool thirdPerson;
    FirstPersonCamera fpCam;
    Camera cam;
    bool reported;

    void Start()
    {
        // ==============================================================
        // THIS COMPONENT HIDES A HEAD. ONLY EVER MINE.
        //
        // PHASE3_SPEC Part 3, failure #1: it shrinks the Head bone to
        // 0.0001 so you are not looking at the inside of your own skull.
        // Attached to a second player and left ungated, it does that to
        // THEM - and their teammates spend the run talking to a body with
        // no head.
        //
        // The name said "Local" from the day it was written. Step 2 is
        // where the name became enforceable.
        // ==============================================================
        if (!PlayerRegistry.IsLocalFor(this))
        {
            enabled = false;
            return;
        }

        var owner = PlayerRegistry.OwnerOf(this);
        cam = owner != null && owner.View != null
            ? owner.View.GetComponent<Camera>()
            : null;
        if (cam != null) fpCam = cam.GetComponent<FirstPersonCamera>();

        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            neckBone = anim.GetBoneTransform(HumanBodyBones.Neck);
            legL     = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            legR     = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);

            if (headBone != null) headScale = headBone.localScale;
            if (neckBone != null) neckScale = neckBone.localScale;
            if (legL != null) legLScale = legL.localScale;
            if (legR != null) legRScale = legR.localScale;
        }

        bodyRenderer = FindBodyRenderer();
        if (bodyRenderer != null) bodyShadowMode = bodyRenderer.shadowCastingMode;

        armsRenderer = FindArmsRenderer();
        if (armsRenderer != null)
            Debug.Log($"[FP Cull] found a separate arms mesh '{armsRenderer.name}' - " +
                      "using true arms-only first person.", armsRenderer);

        HideSeparateProps(true);
        ApplyBodyVisibility(true);

        if (headBone == null)
            Debug.LogWarning("[FP Cull] No Head bone found - is the rig Humanoid? " +
                             "Falling back to leaving the head visible.", this);
    }

    /// <summary>
    /// The renderer with the most vertices is the character. It is never
    /// disabled, whatever its materials are called - that mistake is what
    /// made the whole player disappear.
    /// </summary>
    Renderer FindBodyRenderer()
    {
        Renderer best = null;
        int bestVerts = -1;

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            int v = smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0;
            if (v > bestVerts) { bestVerts = v; best = smr; }
        }
        return best;
    }

    /// <summary>
    /// A separate arms-only mesh, if the model has been split. Optional - see
    /// the note at the bottom of this file about why one skinned mesh cannot
    /// show arms without also showing the torso.
    /// </summary>
    Renderer FindArmsRenderer()
    {
        if (string.IsNullOrEmpty(armsRendererName)) return null;

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == bodyRenderer) continue;
            if (smr.name.IndexOf(armsRendererName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return smr;
        }
        return null;
    }

    void ApplyBodyVisibility(bool firstPerson)
    {
        if (bodyRenderer == null) return;

        // Split model: hide the body outright, keep the arms mesh. This is the
        // only way to get arms with no torso.
        if (armsRenderer != null)
            bodyRenderer.enabled = !firstPerson;

        // Your headlamp sits above your chest, so your own torso throws a big
        // moving blob on the floor right where you are trying to look.
        bodyRenderer.shadowCastingMode = (firstPerson && hideOwnShadow)
            ? UnityEngine.Rendering.ShadowCastingMode.Off
            : bodyShadowMode;

        if (armsRenderer != null)
            armsRenderer.shadowCastingMode = (firstPerson && hideOwnShadow)
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
    }

    void Update()
    {
        // Not while a menu is up. P is a letter, and the crew-name field on
        // the lobby is a text box - typing "Pete's crew" should not flip the
        // camera behind you three times.
        if (CrewLobby.PanelUp) return;

        var kb = PlayerRegistry.KeysOf(this);
        if (kb != null && kb.pKey.wasPressedThisFrame)
        {
            thirdPerson = !thirdPerson;
            HideSeparateProps(!thirdPerson);
            ApplyBodyVisibility(!thirdPerson);
            // THIS DISABLES A CAMERA IT DOES NOT OWN.
            //
            // The camera is a scene object shared with everything else that
            // touches it - the dashboard disables it too. Leaving it off is
            // how you get a frozen view with a body that still walks, which
            // reads as a netcode bug and is not one.
            //
            // Restored in OnDisable as well, so a body destroyed while in
            // third person cannot take the camera down with it.
            // ---- THE LOOK CONTROLLER STAYS ON IN THIRD PERSON ----
            //
            // Disabling it froze the mouse: FirstPersonCamera is what turns
            // mouse movement into yaw and pitch, so switching it off left the
            // yaw stuck at whatever it was when P was pressed.
            //
            // That is worse than a stuck camera. PlayerMotor faces the body at
            // the CAMERA's yaw, and the boom below aims the camera AT the
            // body - so with nothing driving the yaw those two chase each
            // other, and the camera ends up inside the character looking at
            // the back of its own mesh.
            //
            // It stays enabled and keeps owning the aim. This script runs at
            // execution order 60, after it, and overrides only the POSITION -
            // which is all third person ever needed to change.
            RestoreHead();                       // full body in third person

            Debug.Log(thirdPerson
                ? "[Cull] third person ON - the camera is detached and will " +
                  "not follow you. Press P again."
                : "[Cull] third person off - camera reattached.");
        }
    }

    void LateUpdate()
    {
        // Re-apply every frame. The Animator rewrites the pose each update, so
        // a one-off scale in Start would be undone on the very next frame.
        if (!thirdPerson)
        {
            if (headBone != null) headBone.localScale = Vector3.one * headShrink;
            if (shrinkNeck && neckBone != null) neckBone.localScale = Vector3.one * headShrink;

            if (hideLegs)
            {
                if (legL != null) legL.localScale = Vector3.one * headShrink;
                if (legR != null) legR.localScale = Vector3.one * headShrink;
            }

            if (!reported)
            {
                reported = true;
                Debug.Log($"[FP Cull] head bone shrunk, body kept. " +
                          $"{hidden.Count} separate head props hidden. Press P for third person.");
            }
        }

        if (!thirdPerson || cam == null) return;

        // ---- PULLED BACK ALONG THE AIM IT ALREADY HAS ----
        //
        // The camera has been aimed by FirstPersonCamera a moment ago, from
        // the mouse. So third person is only a POSITION change: slide back
        // along that same forward and the view orbits the character exactly
        // as the mouse says, with nothing here having to re-derive a yaw.
        //
        // The rotation is deliberately NOT touched. Aiming the camera at the
        // body was the feedback loop - the body faces the camera's yaw, so a
        // camera that turns to look at the body turns the body, which turns
        // the camera. That is what put the view inside the mesh.
        Vector3 pivot = transform.position + Vector3.up * thirdPersonHeight;
        Vector3 target = pivot - cam.transform.forward * thirdPersonDistance;

        cam.transform.position = Vector3.Lerp(cam.transform.position, target,
                                              12f * Time.deltaTime);
    }

    void RestoreHead()
    {
        if (headBone != null) headBone.localScale = headScale;
        if (neckBone != null) neckBone.localScale = neckScale;
        if (legL != null) legL.localScale = legLScale;
        if (legR != null) legR.localScale = legRScale;
    }

    void HideSeparateProps(bool hide)
    {
        foreach (var r in hidden) if (r != null) r.enabled = true;
        hidden.Clear();
        if (!hide) return;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r == bodyRenderer) continue;   // never the body
            if (!NameMatches(r.gameObject.name)) continue;
            r.enabled = false;
            hidden.Add(r);
        }
    }

    void OnDisable()
    {
        RestoreHead();
        ApplyBodyVisibility(false);
        foreach (var r in hidden) if (r != null) r.enabled = true;
        hidden.Clear();
        if (fpCam != null) fpCam.enabled = true;
    }

    bool NameMatches(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var part in hideNameParts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            if (name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }
}
