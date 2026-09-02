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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs after FirstPersonCamera's own LateUpdate (default order 0). EyeRotation
// is a property read from pitch/yaw fields that FirstPersonCamera.LateUpdate
// updates every frame - go first and the beam aims from LAST frame's look
// direction, a one-frame lag that would show up as the beam trailing behind a
// fast mouse flick.
[DefaultExecutionOrder(40)]
public class PlayerHeadlamp : MonoBehaviour
{
    [Header("Whose head")]
    [Tooltip("Root of the character to search for a Head bone. Leave empty: " +
             "found from this body's own FirstPersonCamera.target, or as a last " +
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
    // OFF. The cone was worse than the light it was drawing attention to.
    //
    // LightShaft builds a real cone MESH in front of the lamp. In a dark
    // concrete shaft that reads as a hard-edged wedge slicing across the
    // screen rather than as a beam in dust - it hides the room, it moves with
    // your head so it never sits still, and the spotlight on its own was
    // already doing the job.
    //
    // Kept as a field rather than deleted: a beam is the right idea and it
    // will work once Phase 8's audio-and-atmosphere pass gives the shaft real
    // volumetrics and dust to catch. The cone mesh is a stand-in for that, and
    // a stand-in that looks worse than nothing should be switched off until
    // the real thing exists.
    public bool visibleBeam = false;

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
    [Tooltip("Also switch off the little glass bump ON THE MODEL when the " +
             "beam is off, so a teammate behind you can tell your light is " +
             "out without needing to see the beam itself.")]
    public bool tintHelmetBump = true;

    [Tooltip("Submesh material whose name contains this (case-insensitive) " +
             "is treated as the lamp bump. Matches PlayerFbxSetupTool's own " +
             "material-naming rule, so nothing here needs to change if that " +
             "file does not.")]
    public string bumpMaterialKey = "light";

    // EMISSION ONLY. This script does not touch _BaseColor / _Color at all -
    // whatever colour you paint the material in the Inspector is what it
    // stays, lit or not. It used to also override the base colour for the
    // OFF state, which is exactly backwards: an "off" that overrides your
    // own colour choice with a computed grey is not off, it is a second
    // artist fighting the first one. Toggling the actual light source is
    // "off" enough - the material underneath needs no opinion of its own.
    //
    // bumpOnEmission is read once from the asset in FindBumpSlot(), so ON
    // always matches whatever PlayerFbxSetupTool baked in. OFF is simply
    // black - not a dim fraction of ON, a literal zero. No emission, no glow,
    // full stop.
    Color bumpOnEmission = Color.white * 2.6f;   // overwritten from the asset

    public bool IsOn { get; private set; }

    Transform headBone;
    Animator anim;
    FirstPersonCamera cachedCam;

    /// <summary>
    /// The camera this lamp aims with, ASKED rather than kept.
    ///
    /// It was resolved once in Start. The scene reload between rounds destroys
    /// the old camera, so from round 2 this was null - and the aim quietly
    /// fell through to the head-bone branch, which points the beam wherever
    /// the BODY is facing rather than wherever you are LOOKING.
    ///
    /// That is why it "worked in round 1 and followed the face afterwards".
    /// Nothing about the lamp changed; it lost the thing it was aiming with
    /// and used its fallback without saying so.
    /// </summary>
    FirstPersonCamera fpCam
    {
        get
        {
            if (cachedCam != null) return cachedCam;

            var owner = PlayerRegistry.OwnerOf(this);
            cachedCam = owner != null ? owner.View : null;
            return cachedCam;
        }
        set => cachedCam = value;
    }
    GameObject rig;
    Light spot;
    LightShaft shaft;

    // EVERY submesh whose material matches, not just the first. The model
    // reuses "M_Player_Light" on more than one part - the helmet bump, and
    // trim bands on the chest / wrist / boots - and a MaterialPropertyBlock
    // override is keyed by (renderer, submesh index), never by material. The
    // first version stopped at the first match and called it done, which is
    // why toggling off only ever turned off whichever one happened to be
    // first in the list and left the rest glowing regardless of state.
    Renderer bumpRenderer;
    readonly List<int> bumpSlots = new List<int>();
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

        var lampOwner = PlayerRegistry.OwnerOf(this);
        if (fpCam == null && lampOwner != null) fpCam = lampOwner.View;
        if (root == null && fpCam != null && fpCam.target != null) root = fpCam.target;

        anim = root != null ? root.GetComponentInChildren<Animator>() : null;

        // Single-player lookup, same caveat as everywhere else in this file
        // set. Phase 3 Step 1 replaced the scan with the registry.
        //
        // This one was the worst of the five. FindFirstObjectByType<Animator>
        // returns ANY animator in the scene - not a player's, necessarily,
        // and with two players not even reliably the same one twice. The lamp
        // is unparented and repositioned from that bone every LateUpdate, so
        // a wrong answer here puts one crew member's headlamp on another
        // crew member's skull. PHASE3_SPEC Part 3 lists it as the third
        // predicted failure.
        //
        // Still LOCAL rather than owner-relative, because this component does
        // not know whose it is yet - Step 2 gives it that. But local is at
        // least a player, which the old line could not promise.
        // MY animator, from MY own hierarchy. Step 1 narrowed this from "any
        // animator in the scene" to "the local player's"; Step 2 narrows it
        // the rest of the way to "the player this component is attached to",
        // which is the only answer that stays correct with four of them.
        if (anim == null)
        {
            var owner = GetComponentInParent<PlayerMotor>();
            anim = owner != null
                ? owner.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);
        }

        if (anim != null && anim.isHuman)
            headBone = anim.GetBoneTransform(HumanBodyBones.Head);

        if (headBone == null)
        {
            Debug.LogWarning("[Headlamp] No Head bone found - falling back to " +
                             "riding the camera, centred, with no offset.");
            cameraFallback = PlayerRegistry.EyeOf(this);
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
    /// Find EVERY submesh of the character's renderer that uses the lamp
    /// material - the helmet bump AND any trim bands sharing the same asset -
    /// so all of them can be dimmed with a MaterialPropertyBlock rather than
    /// editing the shared material. PlayerSkin uses the identical technique
    /// for crew colours, for the identical reason: touching .material clones
    /// it per instance, and a MaterialPropertyBlock does not.
    /// </summary>
    void FindBumpSlot()
    {
        if (!tintHelmetBump || anim == null) return;

        bumpRenderer = anim.GetComponentInChildren<SkinnedMeshRenderer>();
        if (bumpRenderer == null) return;

        var mats = bumpRenderer.sharedMaterials;
        bool gotEmission = false;

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

            if (n.IndexOf(bumpMaterialKey, StringComparison.OrdinalIgnoreCase) < 0) continue;

            // NOT a `return` - the model puts this same material on more than
            // one submesh (helmet bump, chest / wrist / boot trim), and a
            // property block override only ever affects ONE (renderer,
            // index) pair. Stopping at the first match was the actual bug:
            // toggling off only ever silenced whichever slot happened to
            // come first, and every other glowing part on the body ignored
            // the light entirely because nothing had ever told IT to turn
            // off.
            bumpSlots.Add(i);

            // Read ON straight from the asset - PlayerFbxSetupTool bakes it
            // bright already, so this script does not carry a second copy of
            // that colour to go stale against it. Whatever you paint the
            // base colour as in the Inspector, THIS is what ON reproduces -
            // nothing here touches base colour at all. Read once, from the
            // first match - every slot shares the one material asset, so
            // they already agree.
            if (!gotEmission && mats[i].HasProperty(EmissionColorId))
            {
                bumpOnEmission = mats[i].GetColor(EmissionColorId);
                gotEmission = true;
            }
        }
    }

    void Update()
    {
        var kb = PlayerRegistry.KeysOf(this);
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
        // Reads the IsOn field, not a parameter - Apply() applies whatever
        // state was last set by SetOn(), it does not receive one. The first
        // version of this method referenced a local named `on` that only
        // ever existed inside SetOn()'s own scope; it should never have
        // compiled as written, and didn't.
        bool on = IsOn;

        // The whole rig, not just the Light. spot.enabled alone would leave
        // the LightShaft's cone mesh sitting there fully visible - it is a
        // separately built MeshRenderer, not something LightShaft repaints
        // every frame - so an "off" beam would still show as a solid cone.
        // Deactivating the parent takes both with it in one call.
        if (rig != null) rig.SetActive(on);

        if (tintHelmetBump && bumpRenderer != null)
        {
            if (bumpBlock == null) bumpBlock = new MaterialPropertyBlock();
            Color emission = on ? bumpOnEmission : Color.black;

            // EVERY matching submesh, not just one - see FindBumpSlot(). A
            // property block override is keyed by (renderer, submesh index),
            // so the chest band and the boot trim each need their own
            // Get/Set pair even though they share one material asset.
            //
            // EMISSION ONLY, written every time, on AND off, not just on - an
            // override holds whatever was last set until something sets it
            // again, so writing only the ON case would leave every one of
            // these permanently lit the first time it was ever switched on.
            // Base colour is never touched here at all; whatever the
            // material's own _BaseColor is stays exactly that, lit or not.
            foreach (int slot in bumpSlots)
            {
                bumpRenderer.GetPropertyBlock(bumpBlock, slot);
                bumpBlock.SetColor(EmissionColorId, emission);
                bumpRenderer.SetPropertyBlock(bumpBlock, slot);
            }
        }
    }

    // LateUpdate: after the Animator has written this frame's pose, so
    // headBone.position is not one frame stale. Runs whether the light is on
    // or off, so it is already in the right place the instant it is switched
    // back on.
    //
    // POSITION comes from the head bone - it is physically on top of the
    // character's head, and that is a body-facing (yaw) question, unaffected
    // by where you are currently looking.
    //
    // ROTATION comes from the CAMERA, not the head bone, and this is the
    // actual fix for "the light must go with camera direction". The rig
    // (Player root) only turns to match camera YAW, in FixedUpdate, and
    // never pitches at all - PITCH stays purely a camera concept so looking
    // straight up or down never turns the body. A rig sourcing rotation from
    // headBone therefore aimed level no matter how far up or down you
    // looked, which is why the beam was hitting the ceiling and the ground
    // was dark in the screenshot: the lit patch on the wall was where the
    // BODY was still facing, not where the CAMERA was pointed.
    void LateUpdate()
    {
        // ---- REBUILD IT IF THE SCENE TOOK IT ----
        //
        // The rig is deliberately UNPARENTED, which is what lets it sidestep
        // the first-person head shrink - and it is therefore a root object in
        // the scene, so a scene load destroys it. The player survives that
        // load now (Step 8), so from round 2 onward this component was holding
        // a destroyed reference and quietly lighting nothing.
        //
        // Reported as "lamp not working anymore after round 1", which is
        // exactly the round the first scene load happens.
        if (rig == null) BuildRig();
        if (rig == null) return;

        // ---- SOMEBODY ELSE'S SWITCH IS THEIRS TO PRESS ----
        //
        // A remote body never runs the toggle - the key press happens on their
        // machine - so its IsOn stayed at whatever it started as, and a
        // crewmate who went dark stayed lit for everybody else.
        //
        // That matters more here than it sounds. Darkness is information in
        // this game: somebody turning their lamp off is telling you something,
        // and a light that lies about it is worse than no light.
        var mine = PlayerRegistry.IsLocalFor(this);
        if (!mine)
        {
            var owner = PlayerRegistry.OwnerOf(this);
            var row = owner != null ? CrewMemberNet.ForSlot(owner.Slot) : null;

            if (row != null && row.LampOn.Value != IsOn) SetOn(row.LampOn.Value);
        }

        // ---- NO LAMP FOR SOMEBODY WHO IS NOT THERE ----
        //
        // The rig is unparented, so hiding the body does nothing to it: a
        // player taken by the mafia left their headlamp burning in mid-air
        // where they fell, and it followed a body nobody could see.
        //
        // Reported as "looks like the spectator have light too".
        if (Crew.Of(this).Lost)
        {
            rig.SetActive(false);
            return;
        }

        Vector3 pos;
        Quaternion aim;

        if (fpCam != null)
        {
            // EyeRotation already IS pitch+yaw+tilt combined - exactly the
            // direction you are actually looking, full stop.
            aim = fpCam.EyeRotation * Quaternion.Euler(aimEuler);
            pos = headBone != null
                ? headBone.position + headBone.TransformDirection(headOffset)
                : fpCam.EyePosition;
        }
        else if (headBone != null)
        {
            // ---- SOMEBODY ELSE'S LAMP ----
            //
            // There is no camera for a remote body, so this branch is what
            // every teammate's headlamp actually uses - and on its own it only
            // knows which way the BODY faces. NetworkTransform replicates yaw;
            // pitch lives in a camera that does not exist here.
            //
            // The result was a lamp pointing flat down the corridor while its
            // owner was plainly looking at the ceiling. Reported exactly that
            // way, and it is the kind of thing that makes a teammate feel like
            // a puppet rather than a person.
            //
            // CrewMemberNet.LookPitch is one float, written by the only
            // machine that knows it.
            float pitch = 0f;
            float yaw = transform.eulerAngles.y;

            var owner = PlayerRegistry.OwnerOf(this);
            var row = owner != null ? CrewMemberNet.ForSlot(owner.Slot) : null;

            if (row != null)
            {
                pitch = row.LookPitch.Value;

                // ---- YAW FROM THE GAZE, NOT FROM THE BODY ----
                //
                // Body yaw was the same as look yaw right up until the body
                // started facing where it WALKS. Now a crewmate walking
                // sideways has a body pointing along their travel and a head
                // pointing somewhere else - and the lamp belongs to the head.
                //
                // Using the body here would have put every teammate's beam
                // along their footsteps, which looks like the lamp is broken
                // rather than like they are looking over their shoulder.
                yaw = row.LookYaw.Value;
            }

            pos = headBone.position + headBone.TransformDirection(headOffset);
            aim = Quaternion.Euler(pitch, yaw, 0f) * Quaternion.Euler(aimEuler);
        }
        else if (cameraFallback != null)
        {
            pos = cameraFallback.position + cameraFallback.TransformDirection(new Vector3(0f, 0f, 0.15f));
            aim = cameraFallback.rotation * Quaternion.Euler(aimEuler);
        }
        else return;

        rig.transform.SetPositionAndRotation(pos, aim);
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
