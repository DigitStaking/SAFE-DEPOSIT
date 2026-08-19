// LootPrefabBuilder.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/LootPrefabBuilder.cs
//
// THE FOLDER MUST BE NAMED "Editor" - see GrayboxBuilder for why.
//
// USE:  menu bar -> SAFE DEPOSIT -> Props -> Build Placeholder Loot Prefabs
//
// ====================================================================
// PLACEHOLDER LOOT, BUILT FROM PRIMITIVES.
//
// One prefab per economy tier, assembled from cubes and cylinders. Every one
// is meant to be thrown away and replaced with a real model - the point is
// that until then, the game has objects with the right SIZE, the right
// SILHOUETTE and the right physics, so the economy can actually be played.
//
// ====================================================================
// WHY SILHOUETTE MATTERS MORE THAN DETAIL HERE
//
// ECONOMY_AND_CAMPAIGN.md Part 3: "Learning to read a room and take the
// DENSE things is the mastery." That only works if you can tell tiers apart
// at a glance, in a dark room, from the doorway - which is a question of
// shape and size, not texture.
//
// So each tier gets a deliberately different profile rather than five cubes
// in five colours:
//
//   Bulk        wide and low, with cans on top - obviously heavy, obviously
//               not worth it
//   Common      a plain crate, the baseline everything else reads against
//   Good        a small flat case - reads as "packaged", not "bulk"
//   Rare        a vial. Tiny, bright, unmistakable, and the one thing in the
//               game worth 100 dollars a kilo
//   Bulk-heavy  a pallet you could not possibly pocket
//
// Colour is a second channel on top of that, matching PriceScanner's own
// tier bands so the readout and the object agree.
// ====================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LootPrefabBuilder
{
    const string PrefabDir = "Assets/_Project/Prefabs/Loot";
    const string MatDir = "Assets/_Project/Materials/Loot";

    [MenuItem("SAFE DEPOSIT/Props/Build Placeholder Loot Prefabs")]
    static void Build()
    {
        EnsureFolder("Assets/_Project/Prefabs", "Loot");
        EnsureFolder("Assets/_Project/Materials", "Loot");

        BuildBulk();
        BuildCommon();
        BuildGood();
        BuildRare();
        BuildBulkHeavy();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LootPrefabs] Five placeholder tiers written to {PrefabDir}. " +
                  "Replace the meshes; keep the names and LootSpawner keeps working.");
    }

    // ---- the five tiers ----

    static void BuildBulk()
    {
        var mat = Mat("Bulk", new Color(0.62f, 0.42f, 0.28f));
        var root = NewRoot("Loot_Bulk");

        // Sacks: wide, low, unmistakably awkward.
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f),
             new Vector3(0.78f, 0.32f, 0.55f), mat);
        Part(root, PrimitiveType.Cube, new Vector3(0.06f, 0.44f, 0f),
             new Vector3(0.60f, 0.26f, 0.46f), mat, yaw: 12f);

        // A couple of cans on top, so it reads as food and not as a box.
        var tin = Mat("BulkTin", new Color(0.72f, 0.70f, 0.66f));
        Part(root, PrimitiveType.Cylinder, new Vector3(-0.20f, 0.66f, 0.10f),
             new Vector3(0.16f, 0.09f, 0.16f), tin);
        Part(root, PrimitiveType.Cylinder, new Vector3(-0.20f, 0.66f, -0.12f),
             new Vector3(0.16f, 0.09f, 0.16f), tin);

        Finish(root, "Prop_LootBulk");
    }

    static void BuildCommon()
    {
        var mat = Mat("Common", new Color(0.70f, 0.68f, 0.60f));
        var root = NewRoot("Loot_Common");

        // The baseline crate. Everything else is read against this.
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.26f, 0f),
             new Vector3(0.52f, 0.52f, 0.52f), mat);

        var band = Mat("CommonBand", new Color(0.42f, 0.38f, 0.32f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.26f, 0f),
             new Vector3(0.55f, 0.10f, 0.55f), band);

        Finish(root, "Prop_LootCommon");
    }

    static void BuildGood()
    {
        var mat = Mat("Good", new Color(0.45f, 0.80f, 0.50f));
        var root = NewRoot("Loot_Good");

        // A sealed case - flat and packaged, deliberately not crate-shaped.
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f),
             new Vector3(0.44f, 0.22f, 0.32f), mat);

        var clasp = Mat("GoodClasp", new Color(0.85f, 0.88f, 0.90f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.135f, 0.17f),
             new Vector3(0.12f, 0.06f, 0.03f), clasp);

        Finish(root, "Prop_LootGood");
    }

    static void BuildRare()
    {
        var mat = Mat("Rare", new Color(1f, 0.82f, 0.25f));
        var root = NewRoot("Loot_Rare");

        // A vial. The whole tier's identity is that it is TINY and worth
        // more than everything else in the room put together.
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.10f, 0f),
             new Vector3(0.10f, 0.10f, 0.10f), mat);

        var cap = Mat("RareCap", new Color(0.85f, 0.30f, 0.25f));
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.215f, 0f),
             new Vector3(0.11f, 0.025f, 0.11f), cap);

        Finish(root, "Prop_LootRare");
    }

    static void BuildBulkHeavy()
    {
        var mat = Mat("BulkHeavy", new Color(0.40f, 0.44f, 0.52f));
        var root = NewRoot("Loot_BulkHeavy");

        // A pallet. Nothing about this should suggest you can pocket it.
        var pallet = Mat("Pallet", new Color(0.46f, 0.34f, 0.22f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f),
             new Vector3(1.30f, 0.14f, 1.00f), pallet);

        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.62f, 0f),
             new Vector3(1.15f, 0.96f, 0.88f), mat);

        var strap = Mat("Strap", new Color(0.20f, 0.22f, 0.26f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.62f, 0f),
             new Vector3(1.18f, 0.10f, 0.91f), strap);

        Finish(root, "Prop_LootBulkHeavy");
    }

    // ---- machinery ----

    static GameObject NewRoot(string name) => new GameObject(name);

    /// <summary>
    /// One visual piece. Colliders are stripped: the root gets a single
    /// BoxCollider fitted to the whole silhouette in Finish(). A compound of
    /// six child colliders on a Rigidbody is more expensive to simulate and
    /// far more likely to snag on a doorframe than one box.
    /// </summary>
    static void Part(GameObject root, PrimitiveType shape, Vector3 pos,
                     Vector3 scale, Material mat, float yaw = 0f)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = shape.ToString();
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    /// <summary>
    /// Fit one collider to the assembled bounds, add the physics and gameplay
    /// components, save, and clean the temporary object out of the scene.
    ///
    /// Mass and value are NOT set here on purpose - LootSpawner rolls them
    /// per item from the tier's range, so a value baked into the prefab would
    /// be silently overwritten and would only ever mislead whoever opened it.
    /// </summary>
    static void Finish(GameObject root, string assetName)
    {
        var bounds = new Bounds(root.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }

        var box = root.AddComponent<BoxCollider>();
        box.center = bounds.center - root.transform.position;
        box.size = bounds.size;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 10f;   // placeholder; LootSpawner sets the real value
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        root.AddComponent<Carryable>();

        string path = $"{PrefabDir}/{assetName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

    static Material Mat(string name, Color colour)
    {
        if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;

        string path = $"{MatDir}/M_Loot_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[LootPrefabs] URP Lit shader not found.");
                return null;
            }
            mat = new Material(shader) { name = $"M_Loot_{name}" };
            AssetDatabase.CreateAsset(mat, path);
        }

        // URP uses _BaseColor, not the older _Color. Setting the wrong one
        // fails silently and leaves the material white.
        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_Smoothness", 0.18f);
        EditorUtility.SetDirty(mat);

        Cache[name] = mat;
        return mat;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
        Directory.CreateDirectory($"{parent}/{child}");
    }
}
