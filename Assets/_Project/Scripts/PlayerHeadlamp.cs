// PlayerHeadlamp.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerHeadlamp.cs
// Added at runtime by AtmosphereBootstrap - no placement needed by hand.
//
// ========================================================================
// Concept art identity: every diver is a cone of light in the dark.
// Without this the PEAK flat materials just look like gray cubes in a lit
// Unity default scene.
// ========================================================================
//
// ========================================================================
// REWRITTEN 18 Aug 2026: OFF THE CAMERA, ONTO THE HELMET.
//
// It used to be a spotlight parented to the camera with a sideways offset
// (0.12, -0.08, 0.15) - basically riding between your eyes. At the range
// this game lives at, that is nearly zero distance to your own hands and to
// anything you lean toward, like the dashboard. A spotlight that close
// overexposes whatever is in front of it and blooms into a white disc - the
// washed-out buttons and the glowing hands were both this light, not a
// material bug.
//
// It is now sourced from the character's HEAD BONE - top of the helmet,
// centred, where the model already has a lamp bump sculpted and textured
// (material "Light" in PlayerFbxSetupTool). Moving the origin up and off the
// view axis does not fix close-range bloom by itself; the fix for that is
// the on/off toggle below, so a player can kill the beam at short range
// instead of every close surface flaring.
//
// WHY THE LIGHT IS NOT PARENTED TO THE HEAD BONE
//
// LocalFirstPersonBodyCull hides your own head by scaling the Head bone to
// 0.0001 in first person. Scale is inherited: ANY child's local position
// offset is multiplied by that same 0.0001 on the way to world space, and
// Transform.TransformPoint bakes a transform's own scale into the result
// even when called on that transform itself. Parent the lamp to the bone and
// the moment the cull script runs, your "offset from the head" collapses to
// nothing and the lamp snaps to the bone's bare pivot.
//
// The fix is to never go through that matrix. Read headBone.position (a
// translation, unaffected by scale) and combine it with
// headBone.TransformDirection(offset) - TransformDirection applies rotation
// ONLY, never scale - so the offset survives the head-shrink untouched. The
// light rig is a free-floating object with no parent; LateUpdate places it
// by hand, every frame, from that math.
// ========================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHeadlamp : MonoBehaviour
{
    [Header("Whose head")]
    [Tooltip("Root of the character to search for a Head bone. Leave empty: " +
             "found from Camera.main's FirstPersonCamera.target, or as a last " +
             "resort the first Animator in the scene.")]
    public Transform characterRoot;

    [Header("Placement on the helmet")]
    [Tooltip("Metres from the Head bone: x right (0 = centred), y up, z " +
             "forward. This is where the model's own lamp bump sits, roughly - " +
             "nudge by eye once you can see it.")]
    public Vector3 headOffset = new Vector3(0f, 0.14f, 0.09f);

    [Tooltip("Extra tilt applied on top of the head's own facing. A few " +
             "degrees down reads better than dead level, since a lamp mounted " +
             "above the eyeline naturally points slightly down at what you " +
             "are looking at.")]
    public Vector3 aimEuler = new Vector3(6f, 0f, 0f);

    [Header("Beam - when ON")]
    public Color color = new Color(1f, 0.96f, 0.88f, 1f);
    [Range(0f, 30f)] public float intensity = 6f;
    [Range(1f, 120f)] public float range = 28f;
    [Range(1f, 150f)] public float spotAngle = 55f;
    [Range(0f, 1f)] public float innerSpotPercent = 0.55f;

    [Header("Visible beam")]
    [Tooltip("Draws the cone of light in the air, not just the pool it lands " +
             "on. This is the single biggest thing separating the game from " +
             "the concept art.")]
    public bool visibleBeam = true;

    [Tooltip("Keep low. The beam suggests dust in the air; it should never " +
             "look like a solid object.")]
    [Range(0f, 0.4f)] public float beamIntensity = 0.05f;

    [Header("Shadows")]
    public bool enableShadows = true;

    [Header("Toggle")]
    [Tooltip("Lit the moment you spawn, same as before this file had a switch.")]
    public bool startOn = true;

    [Tooltip("Read directly from the keyboard, the same way Elevator reads " +
             "PageUp/PageDown - a permanent player ability rather than a " +
             "context action, so it does not belong behind PlayerInput's " +
             "per-scheme rebinding UI yet.")]
    public Key toggleKey = Key.L;

    [Header("The helmet lamp bump")]
    [Tooltip("Also dim the little glass bump ON THE MODEL when the beam is " +
             "off, so a teammate behind you can tell your light is out " +
             "without needing to see the beam itself. Matches the emissive " +
             "'Light' material PlayerFbxSetupTool already paints onto the rig.")]
    public bool tintHelmetBump = true;

    [Tooltip("Submesh material whose name contains this (case-insensitive) " +
             "is treated as the lamp bump. Matches PlayerFbxSetupTool's own " +
             "material-naming rule, so nothing here needs to change if that " +
             "file does not.")]
    public string bumpMaterialKey = "light";

    // Same values PlayerFbxSetupTool bakes into the shared material's base
    // emission, so "on" looks identical to how the bump already looked before
    // this script could turn it off - only the OFF state is new.
    public Color bumpOnEmission = new Color(1.0f, 0.85f, 0.22f) * 1.7f;
    public Color bumpOffEmission = new Color(1.0f, 0.85f, 0.22f) * 0.05f;

    public bool IsOn { get; private set; }

    Transform headBone;
    Animator anim;
    GameObject rig;
    Light spot;
    LightShaft shaft;

    Renderer bumpRenderer;
    int bumpSlot = -1;
    MaterialPropertyBlock bumpBlock;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // Old behaviour, kept as a fallback ONLY. If no rigged character can be
    // found - a bare test scene, say - the lamp still works, riding the
    // camera the way it always did, offset removed since there is no reason
    // left to prefer one eye over the other.
    Transform cameraFallback;

    void Start()
    {
        FindHead();
        BuildRig();
        FindBumpSlot();

        IsOn = startOn;
        Apply();
    }

    void FindHead()
    {
        Transform root = characterRoot;

        if (root == null && Camera.main != null)
        {
            var fpCam = Camera.main.GetComponent<FirstPersonCamera>();
            if (fpCam != null && fpCam.target != null) root = fpCam.target;
        }

        anim = root != null ? root.GetComponentInChildren<Animator>() : null;

        // Single-player lookup, same caveat as everywhere else in this file
        // set: Phase C replaces this with a player registry.
        if (anim == null) anim = Object.FindFirstObjectByType<Animator>();

        if (anim != null && anim.isHuman)
            headBone = anim.GetBoneTransform(HumanBodyBones.Head);

        if (headBone == null)
        {
            Debug.LogWarning("[Headlamp] No Head bone found - falling back to " +
                             "riding the camera, centred, with no offset.");
            cameraFallback = Camera.main != null ? Camera.main.transform : null;
        }
    }

    void BuildRig()
    {
        rig = new GameObject("Headlamp");
        // Deliberately UNPARENTED - see the header comment. LateUpdate places
        // it by hand every frame from the head bone's position and rotation,
        // which sidesteps the first-person head-shrink entirely.

        spot = rig.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = spotAngle * innerSpotPercent;
        spot.shadows = enableShadows ? LightShadows.Soft : LightShadows.None;
        spot.renderMode = LightRenderMode.Auto;

        if (visibleBeam)
        {
            shaft = rig.AddComponent<LightShaft>();
            shaft.length = range * 0.75f;   // stops short of the lit pool on purpose
            shaft.intensity = beamIntensity;
            shaft.tint = color;
        }
    }

    /// <summary>
    /// Find which submesh of the character's own renderer is the lamp bump,
    /// so it can be dimmed with a MaterialPropertyBlock rather than editing
    /// the shared material - PlayerSkin uses the identical technique for
    /// crew colours, for the identical reason: touching .material clones it
    /// per instance, and a MaterialPropertyBlock does not.
    /// </summary>
    void FindBumpSlot()
    {
        if (!tintHelmetBump || anim == null) return;

        bumpRenderer = anim.GetComponentInChildren<SkinnedMeshRenderer>();
        if (bumpRenderer == null) return;

        var mats = bumpRenderer.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            string n = mats[i].name;

            // PlayerFbxSetupTool also has a "M_Player_AntiLight" material for
            // dark rubber trim, and "AntiLight" contains "light" as a
            // substring too. Not on the model as shipped - it has exactly
            // Glass / Light / Rope / Body - but excluding it costs nothing
            // and stops a future material addition from silently stealing
            // this slot.
            if (n.IndexOf("anti", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (n.IndexOf(bumpMaterialKey, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bumpSlot = i;
                return;
            }
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) Toggle();
    }

    public void Toggle() => SetOn(!IsOn);

    public void SetOn(bool on)
    {
        IsOn = on;
        Apply();
    }

    void Apply()
    {
        // The whole rig, not just the Light. spot.enabled alone would leave
        // the LightShaft's cone mesh sitting there fully visible - it is a
        // separately built MeshRenderer, not something LightShaft repaints
        // every frame - so an "off" beam would still show as a solid cone.
        // Deactivating the parent takes both with it in one call.
        if (rig != null) rig.SetActive(on);

        if (tintHelmetBump && bumpRenderer != null && bumpSlot >= 0)
        {
            if (bumpBlock == null) bumpBlock = new MaterialPropertyBlock();
            bumpRenderer.GetPropertyBlock(bumpBlock, bumpSlot);
            bumpBlock.SetColor(EmissionColorId, on ? bumpOnEmission : bumpOffEmission);
            bumpRenderer.SetPropertyBlock(bumpBlock, bumpSlot);
        }
    }

    // LateUpdate: after the Animator has written this frame's pose, so
    // headBone.position is not one frame stale. Runs whether the light is on
    // or off, so it is already in the right place the instant it is switched
    // back on.
    void LateUpdate()
    {
        if (rig == null) return;

        if (headBone != null)
        {
            rig.transform.SetPositionAndRotation(
                headBone.position + headBone.TransformDirection(headOffset),
                headBone.rotation * Quaternion.Euler(aimEuler));
        }
        else if (cameraFallback != null)
        {
            rig.transform.SetPositionAndRotation(
                cameraFallback.position + cameraFallback.TransformDirection(new Vector3(0f, 0f, 0.15f)),
                cameraFallback.rotation * Quaternion.Euler(aimEuler));
        }
    }

    // Runs when you change a value in the Inspector, so tuning headOffset or
    // intensity shows up immediately without re-entering play mode.
    void OnValidate()
    {
        if (spot == null) return;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = spotAngle * innerSpotPercent;
    }

    void OnDestroy()
    {
        if (rig != null) Destroy(rig);
    }
}
