// PropImporter.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/PropImporter.cs
//
// Menu: SAFE DEPOSIT -> Props -> Make Loot Prefabs
//
// ========================================================================
// Turns the generated .obj files in _Project/Models into real loot: URP
// materials with the right flat colours, a Rigidbody with a believable
// mass, a Carryable, and a prefab.
//
// WHY THIS EXISTS
//
// Unity imports .obj and reads the .mtl next to it, but it builds those
// materials against whatever the project's default shader is - which under
// URP is usually wrong, and often magenta. Rather than fixing nine
// materials by hand every time a prop is regenerated, this rebuilds them
// from the .mtl each time.
//
// It also means a prop's MASS lives in one table here rather than being
// typed into an Inspector, so its weight class can never drift away from
// what the object obviously is.
// ========================================================================

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PropImporter
{
    const string ModelDir  = "Assets/_Project/Models";
    const string MatDir    = "Assets/_Project/Models/Materials";
    const string PrefabDir = "Assets/_Project/Prefabs/Loot";

    // name -> (mass kg, value). Mass decides the weight class in Carryable,
    // so these numbers ARE the design: 8kg and under goes in the backpack,
    // over 60 cannot leave the building without the Collector.
    static readonly Dictionary<string, (float mass, int value)> Table =
        new Dictionary<string, (float, int)>
    {
        { "Prop_VendingMachine", (140f, 1600) },   // Massive - needs the Collector
        { "Prop_FilingCabinet",  ( 34f,  450) },   // Heavy   - two hands, no climbing
        { "Prop_Crate",          (  6f,  220) },   // Small   - goes on your back
    };

    [MenuItem("SAFE DEPOSIT/Props/Make Loot Prefabs")]
    static void Build()
    {
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(PrefabDir);
        AssetDatabase.Refresh();

        int lootLayer = LayerMask.NameToLayer("Loot");
        int made = 0;

        foreach (var objPath in Directory.GetFiles(ModelDir, "Prop_*.obj"))
        {
            string name = Path.GetFileNameWithoutExtension(objPath);
            string assetPath = $"{ModelDir}/{name}.obj";

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (source == null)
            {
                Debug.LogWarning($"[Props] {name}.obj not imported yet - reopen Unity and run again.");
                continue;
            }

            var materials = BuildMaterials(name);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            go.name = name;

            // Reassign every renderer to our URP materials, matched by the
            // name Unity gave the submesh from the .mtl.
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string key = slots[i] != null
                        ? slots[i].name.Replace(" (Instance)", "")
                        : "";
                    if (materials.TryGetValue(key, out var m)) slots[i] = m;
                }
                r.sharedMaterials = slots;
            }

            // Collider from the mesh bounds rather than a MeshCollider.
            // A convex box is stable, cheap, and predictable when the thing is
            // being winched around on a rope; a concave mesh collider is none
            // of those.
            var bounds = LocalBounds(go);
            var col = go.AddComponent<BoxCollider>();
            col.center = bounds.center;
            col.size = bounds.size;

            var rb = go.AddComponent<Rigidbody>();
            var entry = Table.TryGetValue(name, out var e) ? e : (mass: 20f, value: 200);
            rb.mass = entry.mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var carry = go.AddComponent<Carryable>();
            carry.value = entry.value;

            if (lootLayer >= 0) SetLayerRecursive(go, lootLayer);

            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Props] {made} loot prefabs written to {PrefabDir}");
    }

    /// <summary>
    /// Rebuild URP materials straight from the .mtl file, so the colours in
    /// the generator are the colours in the game with nothing in between.
    /// </summary>
    static Dictionary<string, Material> BuildMaterials(string propName)
    {
        var result = new Dictionary<string, Material>();
        string mtlPath = $"{ModelDir}/{propName}.mtl";
        if (!File.Exists(mtlPath)) return result;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[Props] URP Lit shader not found.");
            return result;
        }

        string current = null;

        foreach (var raw in File.ReadAllLines(mtlPath))
        {
            var line = raw.Trim();
            if (line.StartsWith("newmtl ")) { current = line.Substring(7).Trim(); continue; }
            if (current == null || !line.StartsWith("Kd ")) continue;

            var p = line.Split(' ');
            if (p.Length < 4) continue;

            var c = new Color(
                float.Parse(p[1], CultureInfo.InvariantCulture),
                float.Parse(p[2], CultureInfo.InvariantCulture),
                float.Parse(p[3], CultureInfo.InvariantCulture));

            string path = $"{MatDir}/{propName}_{current}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            mat.SetColor("_BaseColor", c);

            // Flat and matte. The whole art direction depends on light and
            // silhouette rather than surface, so nothing should be shiny.
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);

            result[current] = mat;
            EditorUtility.SetDirty(mat);
        }

        return result;
    }

    static Bounds LocalBounds(GameObject go)
    {
        var filters = go.GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

        var b = filters[0].sharedMesh.bounds;
        for (int i = 1; i < filters.Length; i++)
            b.Encapsulate(filters[i].sharedMesh.bounds);

        return b;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }
}
