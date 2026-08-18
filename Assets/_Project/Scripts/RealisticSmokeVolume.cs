// RealisticSmokeVolume.cs  -  SAFE DEPOSIT
// Soft billboard smoke/haze. No cube meshes.
//
// This creates low-alpha camera-facing particles with a generated radial alpha
// texture, so smoke reads as blurry dust/fog instead of square/cube cards.

using UnityEngine;

[DefaultExecutionOrder(-80)]
public class RealisticSmokeVolume : MonoBehaviour
{
    [Header("Volume")]
    public int emitters = 4;
    public float radius = 4.8f;
    public float verticalSpread = 7f;

    [Header("Smoke look")]
    public Color smokeTint = new Color(0.62f, 0.64f, 0.66f, 0.08f);
    public Vector2 particleSize = new Vector2(3.0f, 7.0f);
    public Vector2 lifetime = new Vector2(10f, 22f);
    public Vector2 emissionRate = new Vector2(1.4f, 3.0f);

    [Header("Motion")]
    public Vector2 riseSpeed = new Vector2(0.025f, 0.11f);
    public float driftSpeed = 0.16f;
    public float turbulence = 0.32f;

    Material smokeMaterial;
    Texture2D smokeTexture;

    void Awake()
    {
        Build();
    }

    [ContextMenu("Rebuild Smoke Volume")]
    public void Build()
    {
        ClearChildren();
        smokeTexture = MakeSmokeTexture();
        smokeMaterial = MakeSmokeMaterial(smokeTexture);

        int count = Mathf.Max(1, emitters);
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            var pos = new Vector3(
                Mathf.Cos(a) * radius * 0.42f,
                1.0f + (i % 3) * 1.1f,
                Mathf.Sin(a) * radius * 0.42f);
            BuildEmitter($"soft_smoke_{i + 1:00}", pos, (uint)(1000 + i * 277));
        }
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    Texture2D MakeSmokeTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "SAFE_DEPOSIT_Smoke_Soft_Radial",
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                // Soft irregular circular alpha. The sine breakup prevents a
                // perfect sticker look without requiring a texture file.
                float breakup = 0.08f * Mathf.Sin((nx * 13.1f + ny * 5.7f) * Mathf.PI);
                float a = Mathf.Clamp01(1f - (r + breakup));
                a = a * a * (3f - 2f * a);
                a *= Mathf.Clamp01(1.12f - r * 0.92f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply(false, true);
        return tex;
    }

    Material MakeSmokeMaterial(Texture2D tex)
    {
        // Prefer an actual particle alpha shader. The old smoke looked like
        // cubes when the material path fell back to an opaque/default shader.
        Shader sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        var mat = new Material(sh) { name = "SAFE_DEPOSIT_Smoke_Transparent_Billboard" };
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

        // Force transparent blend where the shader exposes the usual knobs.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    void BuildEmitter(string name, Vector3 localPosition, uint seed)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;

        var ps = go.AddComponent<ParticleSystem>();

        // AddComponent<ParticleSystem>() creates it ALREADY PLAYING - Play On
        // Awake defaults to true and Awake has already run by the time this
        // line returns.
        //
        // duration, randomSeed and useAutoRandomSeed are read-only on a
        // playing system, because changing them mid-simulation would make the
        // existing particles inconsistent with the ones that follow. Every
        // write below would be rejected with its own error, which is where the
        // 75 errors came from - three per emitter, one emitter per shaft
        // section, every time the scene loads.
        //
        // So: stop it, configure it, start it. Play() is called at the end of
        // this method.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ps.randomSeed = seed;
        ps.useAutoRandomSeed = false;

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 18f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.035f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
        main.startColor = smokeTint;
        main.maxParticles = 120;
        main.cullingMode = ParticleSystemCullingMode.Automatic;

        var emission = ps.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate.x, emissionRate.y);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 0.75f, verticalSpread, radius * 0.75f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
        velocity.y = new ParticleSystem.MinMaxCurve(riseSpeed.x, riseSpeed.y);
        velocity.z = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = turbulence;
        noise.frequency = 0.12f;
        noise.scrollSpeed = 0.10f;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.High;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.82f, 0.84f, 0.86f), 0f),
                new GradientColorKey(new Color(0.50f, 0.52f, 0.55f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(smokeTint.a, 0.22f),
                new GradientAlphaKey(smokeTint.a * 0.65f, 0.68f),
                new GradientAlphaKey(0f, 1f),
            });
        color.color = grad;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.35f, 1.0f),
            new Keyframe(1f, 1.75f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.minParticleSize = 0.02f;
        renderer.maxParticleSize = 0.55f;
        renderer.material = smokeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
    }
}
