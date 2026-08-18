// LocalPlayerNoShadow.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LocalPlayerNoShadow.cs
// Goes on: the Player root.
//
// ========================================================================
// STOP YOUR OWN BODY SHADOWING YOUR OWN VIEW
//
// Your headlamp sits above your chest and points where you look. Your torso
// is directly under it. So your own body throws a large moving blob onto the
// floor exactly where you are trying to see - the dark shape you screenshotted.
//
// This is separate from LocalFirstPersonBodyCull ON PURPOSE. Head-hiding and
// shadow-hiding are two unrelated decisions, and having them in one component
// meant switching off the head cull also switched the shadow back on. One
// component, one job.
//
// LOCAL ONLY. In multiplayer every machine runs this on ITS OWN player, so
// the other three still cast normal shadows on your screen. You are the only
// person who never sees your own shadow - which is also true in real life,
// roughly, when the light is strapped to your forehead.
// ========================================================================

using UnityEngine;
using UnityEngine.Rendering;

public class LocalPlayerNoShadow : MonoBehaviour
{
    [Tooltip("Off = this player casts no shadow. Uncheck to get it back.")]
    public bool suppressShadow = true;

    [Tooltip("Keep receiving shadows from the world. Leave ON - a character " +
             "that ignores shadow looks like it is lit by its own private sun " +
             "and stops sitting in the scene.")]
    public bool stillReceiveShadows = true;

    Renderer[] renderers;
    ShadowCastingMode[] original;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        original = new ShadowCastingMode[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            original[i] = renderers[i].shadowCastingMode;

        Apply();
    }

    void OnValidate()
    {
        if (Application.isPlaying && renderers != null) Apply();
    }

    void Apply()
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.shadowCastingMode = suppressShadow ? ShadowCastingMode.Off : original[i];
            if (stillReceiveShadows) r.receiveShadows = true;
        }
    }

    void OnDisable()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].shadowCastingMode = original[i];
    }
}
