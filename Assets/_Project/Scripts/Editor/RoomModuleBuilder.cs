using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the room set as prefabs, TYPED BY DOOR COUNT.
///
/// ====================================================================
/// THE SET EXISTS TO SERVE THE GRAPH
///
/// FloorGraph decides a tree in which every node's degree equals its room's
/// door count. That only works if there is a real room for each count, so the
/// set is organised by exactly that: 1-door dead ends, 2-door through rooms,
/// 3-door junctions, 4-door crossroads.
///
/// A 1-door room is not a room with three doors walled up. It is a room built
/// with one door, whose far end is simply the end of the floor. That
/// distinction is the whole of "no fake dead ends".
///
/// THE DOOR FRAME
///
///   +X points OUT of the room, through the opening.
///
///   front  x=0      yaw 180   (outward -X)
///   back   x=D      yaw 0     (outward +X)
///   left   z=-W/2   yaw 90    (outward -Z)
///   right  z=+W/2   yaw -90   (outward +Z)
///
/// The left/right yaws were INVERTED in the previous version - -90 on the -Z
/// wall points +Z, which is back into the room - so every side branch was
/// attached facing inward, overlapping its own parent. Worth checking on
/// paper rather than by eye: yaw t sends +X to (cos t, 0, -sin t), so -Z
/// needs sin t = 1, which is +90.
///
/// GREYBOX ONLY. Materials, props and lighting are Phase 8.
/// ====================================================================
///
/// Phase 5, Steps 5-6. See PHASE5_SPEC.md.
/// </summary>
public static class RoomModuleBuilder
{
    const string OutputDir = "Assets/_Project/Prefabs/Rooms";
    const string GrayboxMat = "Assets/_Project/Materials/M_Graybox.mat";

    const float DoorWidth = 2f;
    const float DoorHeight = 2.5f;
    const float WallThick = 0.5f;
    const float Height = 4f;

    [MenuItem("Tools/Rooms/Build Room Set")]
    public static void BuildAll()
    {
        if (Directory.Exists(OutputDir))
            foreach (var old in Directory.GetFiles(OutputDir, "*.prefab"))
                AssetDatabase.DeleteAsset(old.Replace('\\', '/'));

        Directory.CreateDirectory(OutputDir);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GrayboxMat);

        // ---- 1 DOOR: dead ends. The only way a branch may end. ----
        End("Room_End_Store", 9f, 6f, mat, survivor: true);
        End("Room_End_Vault", 13f, 7f, mat, survivor: false);

        // ---- 2 DOORS ----
        Through("Room_Through_Hall", 16f, 8f, mat);
        Corner("Room_Corner_Bend", 11f, 9f, mat);

        // ---- 3 DOORS: the junctions. A floor with none is a corridor. ----
        Tee("Room_Tee_Junction", 15f, 10f, mat);
        Fork("Room_Fork_Wide", 12f, 12f, mat);

        // ---- 4 DOORS ----
        Cross("Room_Cross_Atrium", 16f, 14f, mat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Room set rebuilt in " + OutputDir + "\n" +
                  "  1 door : Store, Vault        (dead ends)\n" +
                  "  2 doors: Through, Corner\n" +
                  "  3 doors: Tee, Fork           (junctions)\n" +
                  "  4 doors: Cross\n\n" +
                  "Every door is a connection the generator MUST satisfy. " +
                  "Nothing here is ever sealed.");
    }

    // ------------------------------------------------------------------
    // THE ROOMS
    // ------------------------------------------------------------------

    static void End(string name, float d, float w, Material mat, bool survivor)
    {
        var r = Shell(name, d, w, mat, front: true);

        Door(r, "Door_Front", new Vector3(0f, 0f, 0f), 180f);

        Socket(r, "Loot_1", new Vector3(d * 0.55f, 0f, -w * 0.25f), RoomSocket.Kind.Loot);
        Socket(r, "Loot_2", new Vector3(d * 0.8f, 0f, w * 0.25f), RoomSocket.Kind.Loot);

        // The best loot is at the far end of a dead end, which is also the
        // worst place to be caught. Those are the same sentence, and the only
        // reason a dead end is worth walking into.
        if (survivor)
            Socket(r, "Survivor_1", new Vector3(d * 0.85f, 0f, -w * 0.2f),
                   RoomSocket.Kind.Survivor);
        else
            Socket(r, "Puzzle_B", new Vector3(d * 0.85f, 0f, 0f),
                   RoomSocket.Kind.Puzzle, pairId: "vault");

        Finish(r);
    }

    static void Through(string name, float d, float w, Material mat)
    {
        var r = Shell(name, d, w, mat, front: true, back: true);

        Door(r, "Door_Front", new Vector3(0f, 0f, 0f), 180f);
        Door(r, "Door_Back", new Vector3(d, 0f, 0f), 0f);

        // A colonnade down the length, so a long room has something to walk
        // past rather than being a tube.
        for (int i = 0; i < 6; i++)
            Box($"Column_{i}", r,
                new Vector3(3f + (i / 2) * 4.5f, Height * 0.5f, (i % 2 == 0) ? -2.2f : 2.2f),
                new Vector3(0.7f, Height, 0.7f), mat);

        Socket(r, "Loot_1", new Vector3(d * 0.3f, 0f, -w * 0.3f), RoomSocket.Kind.Loot);
        Socket(r, "Loot_2", new Vector3(d * 0.7f, 0f, w * 0.3f), RoomSocket.Kind.Loot);
        Socket(r, "Hazard_1", new Vector3(d * 0.5f, 0f, 0f), RoomSocket.Kind.Hazard);

        Finish(r);
    }

    static void Corner(string name, float d, float w, Material mat)
    {
        var r = Shell(name, d, w, mat, front: true, leftX: d * 0.6f);

        Door(r, "Door_Front", new Vector3(0f, 0f, 0f), 180f);
        Door(r, "Door_Left", new Vector3(d * 0.6f, 0f, -w * 0.5f), 90f);

        Socket(r, "Loot_1", new Vector3(d * 0.3f, 0f, w * 0.25f), RoomSocket.Kind.Loot);
        Socket(r, "Lock_1", new Vector3(d * 0.85f, 0f, w * 0.3f), RoomSocket.Kind.Lock);

        Finish(r);
    }

    static void Tee(string name, float d, float w, Material mat)
    {
        var r = Shell(name, d, w, mat, front: true, back: true, leftX: d * 0.5f);

        Door(r, "Door_Front", new Vector3(0f, 0f, 0f), 180f);
        Door(r, "Door_Back", new Vector3(d, 0f, 0f), 0f);
        Door(r, "Door_Left", new Vector3(d * 0.5f, 0f, -w * 0.5f), 90f);

        Socket(r, "Loot_1", new Vector3(d * 0.25f, 0f, w * 0.28f), RoomSocket.Kind.Loot);
        Socket(r, "Loot_2", new Vector3(d * 0.75f, 0f, w * 0.28f), RoomSocket.Kind.Loot);
        Socket(r, "Hazard_1", new Vector3(d * 0.5f, 0f, w * 0.1f), RoomSocket.Kind.Hazard);

        Finish(r);
    }

    static void Fork(string name, float d, float w, Material mat)
    {
        var r = Shell(name, d, w, mat, front: true,
                      leftX: d * 0.6f, rightX: d * 0.6f);

        Door(r, "Door_Front", new Vector3(0f, 0f, 0f), 180f);
        Door(r, "Door_Left", new Vector3(d * 0.6f, 0f, -w * 0.5f), 90f);
        Door(r, "Door_Right", new Vector3(d * 0.6f, 0f, w * 0.5f), -90f);

        Socket(r, "Loot_1", new Vector3(d * 0.3f, 0f, -w * 0.2f), RoomSocket.Kind.Loot);
        Socket(r, "Loot_2", new Vector3(d * 0.85f, 0f, 0f), RoomSocket.Kind.Loot);
        Socket(r, "Puzzle_A", new Vector3(d * 0.85f, 0f, w * 0.25f),
               RoomSocket.Kind.Puzzle, pairId: "vault");

        Finish(r);
    }

    static void Cross(string name, float d, float w, Material mat)
    {
        var r = Shell(name, d, w, mat, front: true, back: true,
                      leftX: d * 0.5f, rightX: d * 0.5f);

        Door(r, "Door_Front", new Vector3(0f, 0f, 0f), 180f);
        Door(r, "Door_Back", new Vector3(d, 0f, 0f), 0f);
        Door(r, "Door_Left", new Vector3(d * 0.5f, 0f, -w * 0.5f), 90f);
        Door(r, "Door_Right", new Vector3(d * 0.5f, 0f, w * 0.5f), -90f);

        // Four columns around the middle, so a crossroads reads as a place
        // rather than as an intersection of corridors.
        for (int i = 0; i < 4; i++)
            Box($"Column_{i}", r,
                new Vector3(d * 0.5f + ((i % 2 == 0) ? -3f : 3f), Height * 0.5f,
                            (i / 2 == 0) ? -3f : 3f),
                new Vector3(0.9f, Height, 0.9f), mat);

        Socket(r, "Loot_1", new Vector3(d * 0.25f, 0f, -w * 0.3f), RoomSocket.Kind.Loot);
        Socket(r, "Loot_2", new Vector3(d * 0.75f, 0f, w * 0.3f), RoomSocket.Kind.Loot);
        Socket(r, "Hazard_1", new Vector3(d * 0.5f, 0f, 0f), RoomSocket.Kind.Hazard);

        Finish(r);
    }

    // ------------------------------------------------------------------
    // SHELL - walls with an opening wherever a door goes, and nowhere else
    // ------------------------------------------------------------------

    static Transform Shell(string name, float depth, float width, Material mat,
                           bool front = false, bool back = false,
                           float leftX = float.NaN, float rightX = float.NaN)
    {
        var go = new GameObject(name);
        go.AddComponent<RoomModule>().label = name;

        Transform t = go.transform;
        float halfW = width * 0.5f;
        float midX = depth * 0.5f;

        Box("Floor", t, new Vector3(midX, -WallThick * 0.5f, 0f),
            new Vector3(depth, WallThick, width), mat);

        Box("Ceiling", t, new Vector3(midX, Height + WallThick * 0.5f, 0f),
            new Vector3(depth, WallThick, width), mat);

        Wall("Wall_Front", t, true, -WallThick * 0.5f, 0f, width,
             front ? new[] { 0f } : new float[0], mat);

        Wall("Wall_Back", t, true, depth + WallThick * 0.5f, 0f, width,
             back ? new[] { 0f } : new float[0], mat);

        Wall("Wall_Left", t, false, -halfW - WallThick * 0.5f, midX, depth,
             float.IsNaN(leftX) ? new float[0] : new[] { leftX }, mat);

        Wall("Wall_Right", t, false, halfW + WallThick * 0.5f, midX, depth,
             float.IsNaN(rightX) ? new float[0] : new[] { rightX }, mat);

        return t;
    }

    /// <summary>One wall, as segments with a gap and a lintel at each doorway.
    /// alongZ means the wall RUNS along Z and faces X.</summary>
    static void Wall(string name, Transform t, bool alongZ, float fixedCoord,
                     float spanCenter, float spanLength, float[] doors, Material mat)
    {
        float min = spanCenter - spanLength * 0.5f;
        float max = spanCenter + spanLength * 0.5f;

        var cuts = new System.Collections.Generic.List<float>(doors);
        cuts.Sort();

        float cursor = min;
        int piece = 0;

        foreach (float d in cuts)
        {
            float lo = d - DoorWidth * 0.5f;
            float hi = d + DoorWidth * 0.5f;
            if (hi <= min || lo >= max) continue;

            if (lo > cursor)
                Segment($"{name}_{piece++}", t, alongZ, fixedCoord, cursor, lo, 0f, Height, mat);

            Segment($"{name}_Lintel{piece}", t, alongZ, fixedCoord,
                    Mathf.Max(lo, min), Mathf.Min(hi, max),
                    DoorHeight, Height - DoorHeight, mat);

            cursor = Mathf.Max(cursor, hi);
        }

        if (cursor < max)
            Segment($"{name}_{piece}", t, alongZ, fixedCoord, cursor, max, 0f, Height, mat);
    }

    static void Segment(string name, Transform t, bool alongZ, float fixedCoord,
                        float a, float b, float yBase, float height, Material mat)
    {
        float length = b - a;
        if (length <= 0.001f || height <= 0.001f) return;

        float centre = (a + b) * 0.5f;
        float y = yBase + height * 0.5f;

        Vector3 pos = alongZ ? new Vector3(fixedCoord, y, centre)
                             : new Vector3(centre, y, fixedCoord);
        Vector3 scale = alongZ ? new Vector3(WallThick, height, length)
                               : new Vector3(length, height, WallThick);

        Box(name, t, pos, scale, mat);
    }

    // ------------------------------------------------------------------

    static void Door(Transform parent, string name, Vector3 pos, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.AddComponent<RoomExit>();
    }

    static void Socket(Transform parent, string name, Vector3 pos,
                       RoomSocket.Kind kind, string pairId = "")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;

        var s = go.AddComponent<RoomSocket>();
        s.kind = kind;
        s.pairId = pairId;
    }

    static GameObject Box(string name, Transform parent, Vector3 localPos,
                          Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void Finish(Transform root)
    {
        var go = root.gameObject;
        PrefabUtility.SaveAsPrefabAsset(go, $"{OutputDir}/{go.name}.prefab");
        Object.DestroyImmediate(go);
    }
}
