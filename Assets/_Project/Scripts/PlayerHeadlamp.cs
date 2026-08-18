// PlayerHeadlamp.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerHeadlamp.cs
// Goes on: the Main Camera (or Player root - finds Camera.main).
//
// ========================================================================
// Concept art identity: every diver is a cone of light in the dark.
// Without this the PEAK flat materials just look like gray cubes in a lit
// Unity default scene.
// ========================================================================

using UnityEngine;

public class PlayerHeadlamp : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Leave empty to use Camera.main.")]
    public Transform attachTo;

    [Tooltip("Local offset from the camera. Slightly forward so the near clip does not eat the cone.")]
    public Vector3 localOffset = new Vector3(0.12f, -0.08f, 0.15f);

    [Header("Beam")]
    public Color color = new Color(1f, 0.96f, 0.88f, 1f);
    [Range(0f, 30f)] public float intensity = 6f;
    [Range(1f, 120f)] public float range = 28f;
    [Range(1f, 150f)] public float spotAngle = 55f;
    [Range(0f, 1f)] public float innerSpotPercent = 0.55f;

    [Header("Shadows")]
    public bool enableShadows = true;

    Light spot;
    Transform host;

    void Start()
    {
        host = attachTo;
        if (host == null && Camera.main != null) host = Camera.main.transform;
        if (host == null)
        {
            Debug.LogError("[PlayerHeadlamp] No camera to attach to.");
            enabled = false;
            return;
        }

        var go = new GameObject("Headlamp");
        go.transform.SetParent(host, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;

        spot = go.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = spotAngle * innerSpotPercent;
        spot.shadows = enableShadows ? LightShadows.Soft : LightShadows.None;
        spot.renderMode = LightRenderMode.Auto;

        // The visible beam. In the concept art the cones of light hanging in
        // the air do more for the mood than anything else in the frame - the
        // lamp lighting a surface is only half of it.
        if (visibleBeam)
        {
            var shaft = go.AddComponent<LightShaft>();
            shaft.length = range * 0.75f;   // stops short of the lit pool on purpose
            shaft.intensity = beamIntensity;
            shaft.tint = color;
        }
    }

    [Header("Visible beam")]
    [Tooltip("Draws the cone of light in the air, not just the pool it lands " +
             "on. This is the single biggest thing separating the game from " +
             "the concept art.")]
    public bool visibleBeam = true;

    [Tooltip("Keep low. The beam suggests dust in the air; it should never " +
             "look like a solid object.")]
    [Range(0f, 0.4f)] public float beamIntensity = 0.05f;

    void OnValidate()
    {
        if (spot == null) return;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = spotAngle * innerSpotPercent;
    }
}
