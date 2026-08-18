// GrayboxBuilder.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/GrayboxBuilder.cs
//
// THE FOLDER MUST BE NAMED "Editor". Unity treats any folder called Editor
// as tooling that runs in the editor only and strips it from builds. Put
// this anywhere else and your game will fail to build, because UnityEditor
// code cannot ship in a player.
//
// USE:  menu bar -> SAFE DEPOSIT -> Build Graybox Shaft
//
// Deletes and rebuilds every time, so run it as often as you like. Change a
// constant, click the menu item, look at the result. That loop is the point
// of this file - once the rope exists you will be asking "how deep before
// the descent feels heavy?" constantly, and by hand each answer would cost
// an hour instead of three seconds.
//
// It is also draft one of the real level generator. You already decided
// floors are assembled procedurally from handmade modules; this is that,
// with cubes instead of art.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class GrayboxBuilder
{
    // ------------------------------------------------------------------
    // DIMENSIONS - the only numbers you should need to edit.
    // 1 unit = 1 metre. Unity's physics defaults assume this.
    // ------------------------------------------------------------------

    // WAS 8. The car is 4x4m (ElevatorBuilder.CarInner), parked centred, so
    // this left a 2m gap between the car's outer wall and the doorway on
    // every side - and 2m is a gap a player can simply run and jump across.
    //
    // A player's maximum achievable horizontal jump, computed from
    // PlayerMotor's own numbers (moveSpeed 4.5, jumpHeight 1.1,
    // fallGravityMultiplier 1.8): launch speed sqrt(2 * 9.81 * 1.1) =
    // 4.65 m/s, air time (rise under normal gravity + fall under 1.8x)
    // = 0.83s, so distance = 4.5 * 0.83 = ~3.7m at an absolute best-case
    // dead sprint with a perfect edge takeoff. The old 2m gap was barely
    // half of that - trivial, not tense.
    //
    // 14m leaves a 4.9m gap on every side: roughly 30% beyond the
    // theoretical best jump, with margin for the fact nobody takes off from
    // the exact lip of the deck. That is what makes falling read as
    // certain death rather than an embarrassing miss, and is what makes
    // Step 7's bridge feel load-bearing rather than decorative - see the
    // note in ELEVATOR_SPEC.md.
    const float ShaftInner = 14f;   // interior width and depth of the shaft
    const float WallThick = 0.5f;
    const float FloorHeight = 4f;    // vertical distance between levels
    const float RoomDepth = 6f;    // how far a room extends from the shaft
    const float DoorWidth = 2f;
    const float DoorHeight = 2.5f;
    const int LevelCount = 5;

    // Y rotation per level. This is what makes doorways face different
    // directions, so players must SWING to reach them instead of just
    // dropping straight down. Wraps if LevelCount is longer than this list.
    static readonly float[] LevelRotations = { 0f, 90f, 180f, 270f, 90f };

    // Loot: real prop prefabs when present, cubes only as fallback.
        // Mass/value match PropImporter table.
        static readonly (string prefab, string name, float mass, float size, int value)[] LootTable =
        {
            ("Prop_Crate",          "Cash_Crate",      6f,  0.6f,  220),
            ("Prop_FilingCabinet",  "Filing_Cabinet", 34f,  1.1f,  450),
            ("Prop_VendingMachine", "Vending_Machine",140f, 1.6f, 1600),
        };

    const string GrayMaterialPath = "Assets/_Project/Materials/M_Graybox.mat";
    const string LootMaterialPath = "Assets/_Project/Materials/M_Loot.mat";
    const string RootName = "SHAFT";
    const string LootRootName = "LOOT";

    [MenuItem("SAFE DEPOSIT/Build Graybox Shaft")]
    static void Build()
    {
        DestroyIfPresent(RootName);
        DestroyIfPresent(LootRootName);

        // Derived once here, so changing ShaftInner keeps everything correct.
        float half = ShaftInner * 0.5f;             // 4.0  inner wall face
        float wallMid = half + WallThick * 0.5f;       // 4.25 centre of a wall
        float wallSpan = ShaftInner + WallThick * 2f;   // 9.0  wall length incl. corners
        float totalDrop = FloorHeight * LevelCount;      // 20.0 full depth

        Material gray = GetOrCreateMaterial(GrayMaterialPath, new Color(0.5f, 0.5f, 0.5f));
        Material loot = GetOrCreateMaterial(LootMaterialPath, new Color(0.85f, 0.6f, 0.2f));

        var root = new GameObject(RootName);
        root.transform.position = Vector3.zero;

        var lootRoot = new GameObject(LootRootName);
        lootRoot.transform.position = Vector3.zero;

        // Cap slab. Its BOTTOM face sits at y = 0, which is why the position
        // is half the thickness above zero. A transform position is the
        // CENTRE of an object, never a corner.
        Box("Ceiling_Top", root.transform,
            new Vector3(0f, WallThick * 0.5f, 0f),
            new Vector3(wallSpan, WallThick, wallSpan), gray);

        Box("Floor_Bottom", root.transform,
            new Vector3(0f, -totalDrop - WallThick * 0.5f, 0f),
            new Vector3(wallSpan, WallThick, wallSpan), gray);

        // The point the rope hangs from. An empty on purpose - it is a
        // coordinate, not an object, so the anchor can move without touching
        // any visible geometry.
        var anchor = new GameObject("Winch_Anchor");
        anchor.transform.SetParent(root.transform, false);
        anchor.transform.localPosition = Vector3.zero;

        for (int i = 0; i < LevelCount; i++)
        {
            var level = new GameObject($"Level_{i + 1:00}");
            level.transform.SetParent(root.transform, false);
            level.transform.localPosition = new Vector3(0f, -FloorHeight * (i + 1), 0f);
            level.transform.localRotation = Quaternion.Euler(
                0f, LevelRotations[i % LevelRotations.Length], 0f);

            BuildLevel(level.transform, half, wallMid, wallSpan, gray);
            SpawnLoot(level.transform, lootRoot.transform, half, loot, i);
        }

        int envLayer = LayerMask.NameToLayer("Environment");
        if (envLayer >= 0) SetLayerRecursive(root, envLayer);
        else Debug.LogWarning("[Graybox] Layer 'Environment' missing. Create it in Tags and Layers.");

        SetStaticRecursive(root);

        Undo.RegisterCreatedObjectUndo(root, "Build Graybox Shaft");
        Selection.activeGameObject = root;

        Debug.Log($"[Graybox] {LevelCount} levels, {totalDrop}m drop, " +
                  $"{LevelCount * LootTable.Length} loot items.");
    }

    // ------------------------------------------------------------------
    // ONE LEVEL
    //
    // A 4m tall slice of shaft. Local origin sits at the FLOOR of that
    // level, so every local Y reads as "height above this floor".
    //
    // Three walls are solid. The fourth has a doorway - and a cube cannot
    // have a hole in it, so the opening is made by building the wall AROUND
    // it: a piece left, a piece right, a lintel above. The gap left over is
    // the doorway.
    // ------------------------------------------------------------------

    static void BuildLevel(Transform level, float half, float wallMid, float wallSpan, Material mat)
    {
        float sideWidth = wallSpan * 0.5f - DoorWidth * 0.5f;               // 3.5
        float sideCenterZ = DoorWidth * 0.5f + sideWidth * 0.5f;              // 2.75
        float lintelH = FloorHeight - DoorHeight;                         // 1.5
        float roomFloorW = WallThick + RoomDepth;                            // 6.5
        float roomFloorX = half + roomFloorW * 0.5f;                         // 7.25
        float roomMidX = half + WallThick + RoomDepth * 0.5f;              // 7.5
        float roomBackX = half + WallThick + RoomDepth + WallThick * 0.5f;  // 10.75
        float midY = FloorHeight * 0.5f;                               // 2.0

        Box("Wall_North", level, new Vector3(0f, midY, wallMid),
            new Vector3(wallSpan, FloorHeight, WallThick), mat);
        Box("Wall_South", level, new Vector3(0f, midY, -wallMid),
            new Vector3(wallSpan, FloorHeight, WallThick), mat);
        Box("Wall_West", level, new Vector3(-wallMid, midY, 0f),
            new Vector3(WallThick, FloorHeight, wallSpan), mat);

        Box("Wall_East_Left", level,
            new Vector3(wallMid, DoorHeight * 0.5f, -sideCenterZ),
            new Vector3(WallThick, DoorHeight, sideWidth), mat);
        Box("Wall_East_Right", level,
            new Vector3(wallMid, DoorHeight * 0.5f, sideCenterZ),
            new Vector3(WallThick, DoorHeight, sideWidth), mat);
        Box("Wall_East_Above", level,
            new Vector3(wallMid, DoorHeight + lintelH * 0.5f, 0f),
            new Vector3(WallThick, lintelH, wallSpan), mat);

        // Room_Floor starts at the shaft's INNER wall face, not the outer
        // one, so there is no gap to fall through on the threshold.
        Box("Room_Floor", level,
            new Vector3(roomFloorX, -WallThick * 0.5f, 0f),
            new Vector3(roomFloorW, WallThick, ShaftInner), mat);
        Box("Room_Ceiling", level,
            new Vector3(roomFloorX, FloorHeight + WallThick * 0.5f, 0f),
            new Vector3(roomFloorW, WallThick, ShaftInner), mat);
        Box("Room_BackWall", level,
            new Vector3(roomBackX, midY, 0f),
            new Vector3(WallThick, FloorHeight, wallSpan), mat);
        Box("Room_Wall_North", level,
            new Vector3(roomMidX, midY, wallMid),
            new Vector3(RoomDepth, FloorHeight, WallThick), mat);
        Box("Room_Wall_South", level,
            new Vector3(roomMidX, midY, -wallMid),
            new Vector3(RoomDepth, FloorHeight, WallThick), mat);
    }

    // ------------------------------------------------------------------
    // LOOT
    //
    // Parented to a separate LOOT root rather than to the level, because
    // level geometry is marked Static (never moves) and loot very much
    // moves. Mixing them would break batching and confuse the editor.
    //
    // Positions are worked out in the LEVEL's local space and then converted
    // to world, so loot lands correctly inside rotated rooms.
    // ------------------------------------------------------------------

    static void SpawnLoot(Transform level, Transform lootRoot, float half, Material mat, int levelIndex)
        {
            int lootLayer = LayerMask.NameToLayer("Loot");

            for (int i = 0; i < LootTable.Length; i++)
            {
                var entry = LootTable[i];

                float x = half + WallThick + 1.2f + i * 1.9f;
                float z = -2.2f + i * 2.2f;
                Vector3 local = new Vector3(x, entry.size * 0.5f + 0.1f, z);
                Vector3 world = level.TransformPoint(local);

                GameObject go = null;
                string prefabPath = $"Assets/_Project/Prefabs/Loot/{entry.prefab}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.name = $"{entry.name}_L{levelIndex + 1:00}";
                    go.transform.SetParent(lootRoot, false);
                    go.transform.position = world;
                    go.transform.rotation = level.rotation;

                    // Keep prefab collider/rigidbody; force mass/value in case
                    // the prefab was authored with different numbers.
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb == null) rb = go.AddComponent<Rigidbody>();
                    rb.mass = entry.mass;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                    var carryable = go.GetComponent<Carryable>();
                    if (carryable == null) carryable = go.AddComponent<Carryable>();
                    carryable.value = entry.value;
                }
                else
                {
                    // Fallback cube if loot prefabs are missing.
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"{entry.name}_L{levelIndex + 1:00}";
                    go.transform.SetParent(lootRoot, false);
                    go.transform.position = world;
                    go.transform.rotation = level.rotation;
                    go.transform.localScale = Vector3.one * entry.size;
                    if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = entry.mass;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                    var carryable = go.AddComponent<Carryable>();
                    carryable.value = entry.value;
                }

                if (lootLayer >= 0 && go != null)
                    SetLayerRecursive(go, lootLayer);
            }
        }

    // ------------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------------

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        // CreatePrimitive gives a cube WITH a MeshRenderer and a BoxCollider
        // already attached - physics collision for free.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;

        // The 'false' means do NOT preserve world position. Without it Unity
        // keeps the object where it currently sits and rewrites localPosition
        // to compensate, silently discarding the coordinates we set next.
        // This argument is the single most common cause of "why is my object
        // in the wrong place" in Unity.
        go.transform.SetParent(parent, false);

        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        // sharedMaterial, not material. Assigning .material in the editor
        // clones the material for every object, leaving dozens of duplicates.
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        return go;
    }

    static void DestroyIfPresent(string name)
    {
        var existing = GameObject.Find(name);
        // DestroyImmediate, because the normal Destroy waits until the end of
        // a frame - and outside play mode that frame never arrives.
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
    }

    static Material GetOrCreateMaterial(string path, Color colour)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[Graybox] URP Lit shader not found. Is this project on URP?");
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.Refresh();

        mat = new Material(shader);
        // URP uses "_BaseColor", not the older "_Color". Setting the wrong
        // one fails silently and leaves the material white.
        mat.SetColor("_BaseColor", colour);

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Layers are per-object and not inherited, so we walk the whole tree.
    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    static void SetStaticRecursive(GameObject go)
    {
        go.isStatic = true;
        foreach (Transform child in go.transform)
            SetStaticRecursive(child.gameObject);
    }
}