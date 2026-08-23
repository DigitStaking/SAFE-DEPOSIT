// AtmosphereBootstrap.cs  -  SAFE DEPOSIT
// Always re-applies dark look after every scene load (shop reload too).

using UnityEngine;
using UnityEngine.SceneManagement;

public static class AtmosphereBootstrap
{
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ensure()
    {
        if (!hooked)
        {
            hooked = true;
            SceneManager.sceneLoaded += (_, __) => ApplyAll();
        }

        ApplyAll();
    }

    static void ApplyAll()
    {
        var atmo = SceneRefs.Atmosphere;
        if (atmo == null)
        {
            var go = new GameObject("SceneAtmosphere");
            Object.DontDestroyOnLoad(go);
            atmo = go.AddComponent<SceneAtmosphere>();
        }
        atmo.Apply();

        var cam = PlayerRegistry.Local != null && PlayerRegistry.Local.View != null ? PlayerRegistry.Local.View.GetComponent<Camera>() : null;
        if (cam != null)
        {
            if (cam.GetComponent<PlayerHeadlamp>() == null &&
                cam.GetComponentInChildren<PlayerHeadlamp>() == null)
            {
                cam.gameObject.AddComponent<PlayerHeadlamp>();
            }

            cam.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
