// LightShaft.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LightShaft.cs
// Goes on: the same GameObject as a headlamp Light (or any spot light).
//
// ========================================================================
// THE VISIBLE BEAM.
//
// Look at the concept art again: there is barely any smoke in it. What
// reads as atmosphere is almost entirely CONES OF LIGHT hanging in the air.
// Every diver is a torch beam you can see the shape of, and that single
// effect does more for the mood than any particle system will.
//
// URP has no volumetric lighting out of the box, and adding real volumetrics
// to a game that has to run on a friend's laptop is a bad trade. So this is
// the cheap version everybody uses: an actual cone MESH, additively blended,
// fading out along its length.
//
// Three things make it convincing rather than obviously a cone:
//
//   ADDITIVE BLENDING   light adds to what is behind it, it never darkens.
//                       Alpha blending here looks like grey plastic.
//   SOFT PARTICLES      fades where the cone intersects a wall or a floor,
//                       so the beam does not end in a hard ellipse.
//   CAMERA FADE         dissolves near the camera, so your own beam does not
//                       fill your screen when you look at a close wall.
//
// Needs Depth Texture ticked on the URP Asset for the soft intersection.
// Everything else works without it.
// ========================================================================

using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Light))]
public class LightShaft : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("How far the visible beam reaches. Usually a little SHORTER than " +
             "the light's own range - a beam that reaches exactly as far as " +
             "the lit area draws attention to where the light stops.")]
    public float length = 9f;

    [Tooltip("Leave 0 to take the cone angle from the Light itself, so the " +
             "beam always matches the pool of light it belongs to.")]
    public float coneAngle = 0f;

    [Range(6, 32)] public int sides = 18;

    [Header("Look")]
    [Tooltip("Keep this LOW. The beam should suggest dust in the air, not " +
             "look like a solid object. Above about 0.1 it turns into a cone " +
             "of milk.")]
    [Range(0f, 0.4f)] public float intensity = 0.05f;

    public Color tint = new Color(1f, 0.96f, 0.85f);

    [Tooltip("Metres of fade at the far end, so the beam dissolves into the " +
             "dark instead of stopping.")]
    public float fadeLength = 4f;

    [Header("Fading")]
    public float cameraNearFade = 0.6f;
    public float cameraFarFade = 2.5f;
    public float softFadeDistance = 1.5f;

    Light lamp;
    GameObject cone;
    Material mat;

    void Start()
    {
        lamp = GetComponent<Light>();
        Build();
    }

    [ContextMenu("Rebuild Beam")]
    public void Build()
    {
        if (lamp == null) lamp = GetComponent<Light>();

        if (cone != null)
        {
            if (Application.isPlaying) Destroy(cone);
            else DestroyImmediate(cone);
        }

        float angle = coneAngle > 0.1f ? coneAngle : (lamp != null ? lamp.spotAngle : 55f);

        cone = new GameObject("LightBeam");
        cone.transform.SetParent(transform, false);
        cone.transform.localPosition = Vector3.zero;
        cone.transform.localRotation = Quaternion.identity;

        cone.AddComponent<MeshFilter>().sharedMesh = BuildCone(angle);

        var mr = cone.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat = BuildMaterial();

        // A beam is light, not an object. It must not cast, receive, or be
        // picked up by probes - all three produce obvious artefacts.
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    /// <summary>
    /// An open cone pointing down +Z, apex at the origin.
    ///
    /// UV.y runs 0 at the apex to 1 at the far end, which is what the
    /// generated texture uses to fade the beam out with distance. No cap on
    /// the far end - a capped cone reads as a solid object the moment you see
    /// it side-on.
    /// </summary>
    Mesh BuildCone(float angleDegrees)
    {
        int n = Mathf.Clamp(sides, 6, 32);
        float radius = Mathf.Tan(angleDegrees * 0.5f * Mathf.Deg2Rad) * length;

        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new int[n * 6];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float a = t * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);

            verts[i] = Vector3.zero;                       // apex ring
            verts[i + n] = dir * radius + Vector3.forward * length;

            uvs[i] = new Vector2(t, 0f);
            uvs[i + n] = new Vector2(t, 1f);
        }

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int o = i * 6;

            tris[o + 0] = i;      tris[o + 1] = i + n;      tris[o + 2] = next + n;
            tris[o + 3] = i;      tris[o + 4] = next + n;   tris[o + 5] = next;
        }

        var mesh = new Mesh { name = "LightBeamCone" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Material BuildMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        var m = new Material(sh) { name = "SAFE_DEPOSIT_LightBeam" };

        var tex = BuildFalloffTexture();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);

        var c = tint * intensity;
        c.a = 1f;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);

        m.SetOverrideTag("RenderType", "Transparent");

        // ADDITIVE. Light adds to what is behind it and can never darken it.
        // Alpha blending here would make the beam look like grey plastic
        // laid over the scene, which is the usual reason home-made light
        // shafts look wrong.
        if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 2f);   // additive
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
        if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_Cull"))     m.SetFloat("_Cull", (float)CullMode.Off);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // Fades where the cone cuts into a wall, so the beam never ends in a
        // hard ellipse on the floor. Needs Depth Texture on the URP Asset.
        if (m.HasProperty("_SoftParticlesEnabled"))
        {
            m.SetFloat("_SoftParticlesEnabled", 1f);
            m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            m.SetFloat("_SoftParticlesFarFadeDistance", softFadeDistance);
            m.EnableKeyword("_SOFTPARTICLES_ON");
        }

        // Dissolves near the camera. Without it, looking at a close wall puts
        // your own beam across the whole screen.
        if (m.HasProperty("_CameraFadingEnabled"))
        {
            m.SetFloat("_CameraFadingEnabled", 1f);
            m.SetFloat("_CameraNearFadeDistance", cameraNearFade);
            m.SetFloat("_CameraFarFadeDistance", cameraFarFade);
            m.EnableKeyword("_FADING_ON");
        }

        m.renderQueue = (int)RenderQueue.Transparent + 50;
        return m;
    }

    /// <summary>
    /// Brightest at the apex, gone by the far end, with a slight flare right
    /// at the lamp. Real beams are brightest where the air is densest with
    /// scattered light, which is nearest the source.
    /// </summary>
    Texture2D BuildFalloffTexture()
    {
        const int w = 4, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "SAFE_DEPOSIT_BeamFalloff",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float fade = Mathf.Clamp01(fadeLength / Mathf.Max(0.01f, length));

        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);

            // Inverse-square-ish decay along the beam.
            float a = 1f / (1f + v * v * 9f);

            // Force it to zero over the last stretch so it dissolves into the
            // dark rather than being cut off by the end of the mesh.
            if (v > 1f - fade)
            {
                float k = (1f - v) / Mathf.Max(0.0001f, fade);
                a *= k * k;
            }

            var col = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, col);
        }

        tex.Apply(false, true);
        return tex;
    }
}
