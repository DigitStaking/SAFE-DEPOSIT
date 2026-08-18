// PlayerFbxSetupTool.cs  -  SAFE DEPOSIT
// Editor-only helper. Menu: SAFE DEPOSIT -> Player -> Setup Player FBX Prefab
//
// Fixes the workflow problem where the Player prefab has no camera and manual
// prefab editing is awkward. It adds Assets/_Project/Models/Player.fbx as a
// VISUAL CHILD of Assets/_Project/Prefabs/Player.prefab and assigns close
// URP materials by source material name.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerFbxSetupTool
{
    const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";
    const string FbxPath = "Assets/_Project/Models/Player.fbx";
    const string MatDir = "Assets/_Project/Materials/PlayerFbx";
    const string VisualRootName = "PlayerModel_FBX_VISUAL";

    [MenuItem("SAFE DEPOSIT/Player/Setup Player FBX Prefab")]
    public static void SetupPlayerFbxPrefab()
    {
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (modelPrefab == null)
        {
            Debug.LogError($"[PlayerFbxSetup] Missing model at {FbxPath}");
            return;
        }

        if (!File.Exists(PrefabPath))
        {
            Debug.LogError($"[PlayerFbxSetup] Missing prefab at {PrefabPath}");
            return;
        }

        EnsureMaterialFolder();
        var mats = CreateOrUpdateMaterials();

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RemoveOldVisual(root.transform);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
            visual.name = VisualRootName;
            visual.transform.localPosition = Vector3.zero;
            // Model faces camera-forward with 0 yaw.
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            RemoveVisualPhysics(visual);
            AssignMaterials(visual, mats);
            HideLegacyGraybody(root.transform);

            // Keep PlayerSkin on the root for future crew colors. It will tint
            // source materials named Player/Body/Suit/Torso without cloning.
            if (root.GetComponent<PlayerSkin>() == null)
                root.AddComponent<PlayerSkin>();
            if (root.GetComponent<LocalFirstPersonBodyCull>() == null)
                root.AddComponent<LocalFirstPersonBodyCull>();
            if (root.GetComponent<PlayerAnimatorDriver>() == null)
                root.AddComponent<PlayerAnimatorDriver>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[PlayerFbxSetup] Saved {PrefabPath} with {FbxPath} as visual child. Camera stays scene-level.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("SAFE DEPOSIT/Player/Create/Update Player FBX Materials")]
    public static void CreatePlayerMaterialsOnly()
    {
        EnsureMaterialFolder();
        CreateOrUpdateMaterials();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerFbxSetup] Player FBX materials created/updated.");
    }

    static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
            AssetDatabase.CreateFolder("Assets/_Project", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "PlayerFbx");
    }

    struct MaterialSet
    {
        public Material playerSuit;
        public Material bodyTrim;
        public Material rubberDark;
        public Material glassVisor;
        public Material lampGlass;
        public Material rope;
        public Material badge;
        public Material antiLight;
    }

    static MaterialSet CreateOrUpdateMaterials()
    {
        return new MaterialSet
        {
            // Close to your Blender screenshot: red/orange suit, dark rubber,
            // white bulb glass, grey straps, dark badge.
            playerSuit = Mat("Player", new Color(0.95f, 0.12f, 0.07f), 0.0f, 0.28f),
            bodyTrim = Mat("Body", new Color(0.52f, 0.54f, 0.55f), 0.0f, 0.36f),
            rubberDark = Mat("AntiLight", new Color(0.035f, 0.033f, 0.032f), 0.0f, 0.18f),
            glassVisor = Mat("Glass", new Color(0.08f, 0.12f, 0.13f, 0.74f), 0.0f, 0.74f, transparent: true),

            // WHITE, not the yellow this was for years. A headlamp bulb reads
            // as a warm INDICATOR light at this colour, not an active bulb -
            // PlayerHeadlamp.cs reads this asset's own base and emission at
            // runtime and dims them for the OFF state, so this value is the
            // single source of truth for what the lamp looks like lit, in
            // both the editor and in play.
            lampGlass = Mat("Light", new Color(0.92f, 0.95f, 1.0f), 0.0f, 0.55f, emission: Color.white * 2.6f),
            rope = Mat("Rope", new Color(0.55f, 0.34f, 0.10f), 0.0f, 0.32f),
            badge = Mat("Badge", new Color(0.025f, 0.035f, 0.048f), 0.0f, 0.22f),
            antiLight = Mat("DarkRubber", new Color(0.055f, 0.045f, 0.04f), 0.0f, 0.16f),
        };
    }

    static Material Mat(string name, Color baseColor, float metallic, float smoothness,
        bool transparent = false, Color? emission = null)
    {
        string path = $"{MatDir}/M_Player_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { name = $"M_Player_{name}" };
            AssetDatabase.CreateAsset(mat, path);
        }

        SetColor(mat, "_BaseColor", baseColor);
        SetColor(mat, "_Color", baseColor);
        SetFloat(mat, "_Metallic", metallic);
        SetFloat(mat, "_Smoothness", smoothness);

        if (transparent)
        {
            mat.SetOverrideTag("RenderType", "Transparent");
            SetFloat(mat, "_Surface", 1f);
            SetFloat(mat, "_Blend", 0f);
            SetFloat(mat, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloat(mat, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloat(mat, "_ZWrite", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            mat.SetOverrideTag("RenderType", "Opaque");
            SetFloat(mat, "_Surface", 0f);
            SetFloat(mat, "_Blend", 0f);
            SetFloat(mat, "_SrcBlend", 1f);
            SetFloat(mat, "_DstBlend", 0f);
            SetFloat(mat, "_ZWrite", 1f);
            mat.renderQueue = -1;
        }

        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            SetColor(mat, "_EmissionColor", emission.Value);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            SetColor(mat, "_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AssignMaterials(GameObject visual, MaterialSet mats)
    {
        foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                string n = shared[i] != null ? shared[i].name : "";
                shared[i] = Pick(n, mats);
            }
            r.sharedMaterials = shared;
        }
    }

    static Material Pick(string sourceName, MaterialSet mats)
    {
        string n = sourceName.ToLowerInvariant();
        if (n.Contains("glass") || n.Contains("visor")) return mats.glassVisor;
        if (n.Contains("light") || n.Contains("lamp")) return mats.lampGlass;
        if (n.Contains("rope")) return mats.rope;
        if (n.Contains("badge")) return mats.badge;
        if (n.Contains("anti") || n.Contains("rubber") || n.Contains("boot") || n.Contains("glove")) return mats.rubberDark;
        if (n.Contains("body") || n.Contains("trim") || n.Contains("metal")) return mats.bodyTrim;
        if (n.Contains("player") || n.Contains("suit") || n.Contains("torso")) return mats.playerSuit;
        return mats.playerSuit;
    }

    static void RemoveVisualPhysics(GameObject visual)
    {
        foreach (var c in visual.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        foreach (var rb in visual.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rb);
    }

    static void RemoveOldVisual(Transform root)
    {
        var old = root.Find(VisualRootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    // Old graybox capsule/arms must not fight the real FBX silhouette.
    static void HideLegacyGraybody(Transform root)
    {
        // "Head" belongs on this list and was missing, which is why a grey
        // cube floated at eye height in every screenshot: LocalFirstPersonBodyCull
        // only ever touches SkinnedMeshRenderers, and the graybox head is a
        // plain MeshRenderer, so nothing was hiding it.
        string[] hide = { "Body", "Head", "Cube", "ChestPivot", "Arm_L", "Arm_R" };
        foreach (var n in hide)
        {
            var t = root.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    static void SetColor(Material mat, string prop, Color value)
    {
        if (mat.HasProperty(prop)) mat.SetColor(prop, value);
    }

    static void SetFloat(Material mat, string prop, float value)
    {
        if (mat.HasProperty(prop)) mat.SetFloat(prop, value);
    }
}
