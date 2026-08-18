// SceneAtmosphere.cs  -  SAFE DEPOSIT
// Readable PEAK-style shaft atmosphere. Re-applied every scene load.
// Smoke target: visible low shaft haze like reference art, not cube clouds.

using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class SceneAtmosphere : MonoBehaviour
{
    [Header("Fog / visibility")]
    public bool enableFog = true;
    public Color fogColor = new Color(0.105f, 0.115f, 0.13f, 1f);
    [Tooltip("Lower = you can see farther. Old value was 0.05 and was too black.")]
    public float fogDensity = 0.026f;

    [Header("Ambient")]
    public Color ambientSky = new Color(0.17f, 0.18f, 0.20f, 1f);
    public Color ambientEquator = new Color(0.11f, 0.12f, 0.13f, 1f);
    public Color ambientGround = new Color(0.055f, 0.055f, 0.06f, 1f);
    [Range(0f, 2f)] public float ambientIntensity = 0.82f;

    [Header("Directional fill")]
    public bool dimDirectionalLights = true;
    [Range(0f, 1f)] public float directionalIntensity = 0.16f;

    [Header("Camera")]
    public Color cameraBackground = new Color(0.075f, 0.083f, 0.095f, 1f);

    [Header("Low shaft smoke")]
    public bool enableSmoke = true;
    [Tooltip("World-space center of the smoke layer. Graybox shaft is about 20m deep, so -13 sits low like the reference.")]
    public Vector3 smokeCenter = new Vector3(0f, -13f, 0f);
    public int smokeEmitters = 7;
    public float smokeRadius = 7.5f;
    public float smokeVerticalSpread = 8f;
    public Color smokeTint = new Color(0.62f, 0.64f, 0.68f, 0.13f);

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start() => Apply();

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    [ContextMenu("Apply Atmosphere Now")]
    public void Apply()
    {
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;

        RenderSettings.fog = enableFog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.subtractiveShadowColor = new Color(0.06f, 0.06f, 0.07f);
        RenderSettings.reflectionIntensity = 0.06f;

        if (dimDirectionalLights)
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light == null) continue;
                if (light.type != LightType.Directional) continue;

                light.intensity = directionalIntensity;
                light.color = new Color(0.62f, 0.68f, 0.78f);
                light.shadows = LightShadows.Soft;
            }
        }

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam == null) continue;
            cam.backgroundColor = cameraBackground;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        if (enableSmoke) EnsureSmoke();
        DynamicGI.UpdateEnvironment();
    }

    void EnsureSmoke()
    {
        var smoke = FindFirstObjectByType<RealisticSmokeVolume>();
        if (smoke == null)
        {
            var go = new GameObject("LOW_SHAFT_SMOKE_runtime");
            DontDestroyOnLoad(go);
            smoke = go.AddComponent<RealisticSmokeVolume>();
        }

        smoke.transform.position = smokeCenter;
        smoke.emitters = smokeEmitters;
        smoke.radius = smokeRadius;
        smoke.verticalSpread = smokeVerticalSpread;
        smoke.smokeTint = smokeTint;
        smoke.particleSize = new Vector2(5.0f, 11.0f);
        smoke.lifetime = new Vector2(14f, 30f);
        smoke.emissionRate = new Vector2(2.2f, 4.2f);
        smoke.riseSpeed = new Vector2(0.015f, 0.07f);
        smoke.driftSpeed = 0.11f;
        smoke.turbulence = 0.22f;
        smoke.Build();
    }
}
