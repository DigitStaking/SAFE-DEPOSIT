using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the six room modules as prefabs.
///
/// ====================================================================
/// WHY THIS IS A SCRIPT AND NOT A MORNING IN THE EDITOR
///
/// Every prefab in this project is built by an editor script, and it has
/// worked every time. A module is walls, a doorway and a dozen empties at
/// exact coordinates - the kind of thing hand-placing gets 95% right and then
/// costs an hour finding the 5%. Re-running this is also how a proportion
/// gets changed later: edit one constant, rebuild all six, rather than nudge
/// six prefabs and miss one.
///
/// THE FRAME, restated because everything depends on it:
///
///   origin = the module's own doorway, on the floor, centre of the opening
///   +X     = into the room
///   +Z     = the room's width
///   door   = 2 wide, 2.5 tall, matching what Grayboxbuilder already fixed
///
/// An exit is that same frame pointing outward, so attaching B to an exit of
/// A is "put B's origin on the exit and match rotation" - for any A and any
/// B, with neither knowing about the other.
///
/// GREYBOX ONLY. Materials, props and lighting are Phase 8. A module that
/// looks finished before the generator arranges them is a module nobody wants
/// to change.
/// ====================================================================
///
/// Phase 5, Step 5. See PHASE5_SPEC.md.
/// </summary>
public static class RoomModuleBuilder
{
    const string OutputDir = "Assets/_Project/Prefabs/Rooms";
    const string GrayboxMat = "Assets/_Project/Materials/M_Graybox.mat";

    // Matching Grayboxbuilder, deliberately - a module that disagrees with the
    // shaft about how big a door is cannot be attached to it.
    const float DoorWidth = 2f;
    const float DoorHeight = 2.5f;
    const float WallThick = 0.5f;

    // Interior height. Above the door and below the 5m floor pitch, so a
    // module still fits between two levels of the existing shaft.
    const float Height = 4f;

    [MenuItem("Tools/Rooms/Build Six Modules")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(OutputDir);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GrayboxMat);

        // 2 landings, 2 mains, 1 side, 1 back. The split is not arbitrary:
        // landings and mains are what you see on EVERY floor, so they carry
        // the variety; the side and the back are optional and rarer, so one
        // of each is enough until ten floors say otherwise.
        Landing("Room_Landing_Open", mat, pillars: false);
        Landing("Room_Landing_Pillared", mat, pillars: true);
        // THREE mains, not two, and the third is not decoration. The
        // generator guarantees no main repeats on consecutive floors; with
        // only two modules that guarantee FORCES A,B,A,B, which reads as
        // generated exactly as loudly as a repeat would. Two of anything
        // alternates. Three is the smallest set where the sequence can
        // surprise you - see FloorLayout.MainFor.
        Main("Room_Main_Hall", mat, variant: 0);
        Main("Room_Main_Divided", mat, variant: 1);
        Main("Room_Main_Columns", mat, variant: 2);
        SideRoom(mat);
        BackRoom(mat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Six room modules written to {OutputDir}.\n" +
                  "Run Tools > Rooms > Validate Modules In Scene after dropping " +
                  "any of them into a scene.\n" +
                  "NOT checked by anything: each one must be walkable with every " +
                  "socket empty and every exit sealed. Walk them.");
    }

    // ------------------------------------------------------------------
    // THE SIX
    //
    // LONG, NOT SQUARE. The first set were 10x12, 7x7, 6x8 - near-square
    // boxes, and a near-square box has no direction. You walk in, you see the
    // whole thing, you leave. Ten floors of them read as ten of the same room
    // whatever the generator does with the order, because there is nothing to
    // walk ALONG.
    //
    // Depth roughly twice width now. A long room has a far end, which means it
    // has somewhere the light does not reach, somewhere loot is worth the walk,
    // and somewhere a thing can be between you and the door. None of that
    // exists in a square.
    // ------------------------------------------------------------------

    /// <summary>Where you step out of the lift. Must read as "the way back"
    /// from anywhere on the floor - a landing you can lose is a landing that
    /// makes the whole floor frightening for the wrong reason.</summary>
    static void Landing(string name, Material mat, bool pillars)
    {
        const float D = 11f, W = 7f;
        var root = Shell(name, RoomModule.Role.Landing, D, W, mat, backDoor: true);

        if (pillars)
        {
            // Down the length rather than across it, so they divide the walk
            // instead of blocking the entrance.
            for (int i = 0; i < 3; i++)
                Box($"Pillar_{i}", root, new Vector3(3f + i * 3f, Height * 0.5f, -1.7f),
                    new Vector3(0.6f, Height, 0.6f), mat);
        }

        Socket(root, "Loot_1", new Vector3(3.5f, 0f, 2.2f), RoomSocket.Kind.Loot);
        Socket(root, "Loot_2", new Vector3(8f, 0f, -2.2f), RoomSocket.Kind.Loot);
        Exit(root, "Exit_Main", new Vector3(D, 0f, 0f), 0f);

        Finish(root);
    }

    /// <summary>The room the landing opens into. Longest, most loot, and the
    /// only module with two ways on - the side room and the back hang off
    /// it.</summary>
    static void Main(string name, Material mat, int variant)
    {
        const float D = 20f, W = 10f;
        // THREE WAYS ON, not one. Walking into a room and being offered a
        // single corridor is not exploring, it is being led - and a crew that
        // cannot split up has no reason to talk to each other, which is the
        // mechanic the whole middle of this game is built on.
        var root = Shell(name, RoomModule.Role.Main, D, W, mat,
                         backDoor: true, leftDoorX: 10f, rightDoorX: 10f);

        if (variant == 1)
        {
            // A spine wall down most of the length with a gap at the far end.
            // Two routes through one room is what lets a crew split without
            // leaving the room, which is where the cannibal and the eyeless
            // get interesting in Phase 6.
            Box("Partition", root, new Vector3(8f, Height * 0.5f, 1.5f),
                new Vector3(0.4f, Height, 13f), mat);

            // Paired half of a two-room mechanism. Nothing reads pairId until
            // Phase 6 - it is placed now because PUZZLES.md needs the halves
            // in DIFFERENT rooms, and a generator that cannot express that
            // cannot build a single puzzle in that document.
            Socket(root, "Puzzle_A", new Vector3(17f, 0f, 4f),
                   RoomSocket.Kind.Puzzle, pairId: "main_back");
        }

        if (variant == 2)
        {
            // A colonnade down the length. Reads completely differently from
            // the hall and the partition at a glance, which is the whole job -
            // a player is not auditing the layout, they are deciding whether
            // they have been here before.
            for (int i = 0; i < 8; i++)
            {
                float px = 3f + (i / 2) * 4.5f;
                float pz = (i % 2 == 0) ? -2.8f : 2.8f;
                Box($"Column_{i}", root, new Vector3(px, Height * 0.5f, pz),
                    new Vector3(0.8f, Height, 0.8f), mat);
            }
        }

        Socket(root, "Loot_1", new Vector3(4f, 0f, -3.5f), RoomSocket.Kind.Loot);
        Socket(root, "Loot_2", new Vector3(11f, 0f, 3.5f), RoomSocket.Kind.Loot);
        Socket(root, "Loot_3", new Vector3(17f, 0f, -3f), RoomSocket.Kind.Loot);
        Socket(root, "Hazard_1", new Vector3(14f, 0f, 0f), RoomSocket.Kind.Hazard);

        // Left, right, straight on. Unlabelled now: the generator picks what
        // goes through each door rather than being told which door leads to
        // which kind of room, so a side room can be on the left of one floor
        // and straight ahead on another.
        Exit(root, "Exit_Ahead", new Vector3(D, 0f, 0f), 0f);
        Exit(root, "Exit_Left", new Vector3(10f, 0f, -W * 0.5f), -90f);
        Exit(root, "Exit_Right", new Vector3(10f, 0f, W * 0.5f), 90f);

        Finish(root);
    }

    /// <summary>Off the main, optional. Where a lock or a survivor lives -
    /// narrow and deep, so it is a place you commit to walking down rather
    /// than a bulge you can see the end of from the door.</summary>
    static void SideRoom(Material mat)
    {
        const float D = 10f, W = 5f;
        var root = Shell("Room_Side_Store", RoomModule.Role.Side, D, W, mat);

        Socket(root, "Loot_1", new Vector3(3f, 0f, -1.5f), RoomSocket.Kind.Loot);
        Socket(root, "Loot_2", new Vector3(7.5f, 0f, 1.5f), RoomSocket.Kind.Loot);
        Socket(root, "Lock_1", new Vector3(1f, 0f, 1.8f), RoomSocket.Kind.Lock);
        Socket(root, "Survivor_1", new Vector3(8.5f, 0f, -1.2f), RoomSocket.Kind.Survivor);

        // No exit. A side room is somewhere you go back out of.

        Finish(root);
    }

    /// <summary>The dead end. Best loot, worst place to be caught - and those
    /// are the same sentence, which is the only reason it is worth walking
    /// to.</summary>
    static void BackRoom(Material mat)
    {
        const float D = 14f, W = 8f;
        var root = Shell("Room_Back_DeadEnd", RoomModule.Role.Back, D, W, mat);

        Socket(root, "Loot_1", new Vector3(6f, 0f, -2.5f), RoomSocket.Kind.Loot);
        Socket(root, "Loot_2", new Vector3(11f, 0f, 2.5f), RoomSocket.Kind.Loot);
        Socket(root, "Loot_3", new Vector3(12.5f, 0f, -2f), RoomSocket.Kind.Loot);
        Socket(root, "Survivor_1", new Vector3(3f, 0f, 3f), RoomSocket.Kind.Survivor);
        Socket(root, "Puzzle_B", new Vector3(12f, 0f, 0f),
               RoomSocket.Kind.Puzzle, pairId: "main_back");

        Finish(root);
    }

    // ------------------------------------------------------------------
    // THE SHELL: floor, ceiling, four walls, one doorway at the origin
    // ------------------------------------------------------------------

    static Transform Shell(string name, RoomModule.Role role,
                           float depth, float width, Material mat,
                           bool backDoor = false, float leftDoorX = float.NaN,
                           float rightDoorX = float.NaN)
    {
        var go = new GameObject(name);
        var module = go.AddComponent<RoomModule>();
        module.role = role;
        module.label = name;

        Transform t = go.transform;
        float halfW = width * 0.5f;
        float midX = depth * 0.5f;

        Box("Floor", t, new Vector3(midX, -WallThick * 0.5f, 0f),
            new Vector3(depth, WallThick, width), mat);

        Box("Ceiling", t, new Vector3(midX, Height + WallThick * 0.5f, 0f),
            new Vector3(depth, WallThick, width), mat);

        // ---- EVERY WALL IS SEGMENTED, AND EVERY EXIT GETS A HOLE ----
        //
        // The first version of this built four solid walls and cut a doorway
        // only at the front, because the front is the one opening every module
        // has. The main room's two exits then pointed straight into concrete -
        // an exit is a promise that you can walk through it, and nothing in
        // the code was keeping that promise.
        //
        // So the openings are passed in and the walls are built around them.
        // A module cannot now declare an exit the shell does not know about
        // without it being visible the moment you walk into the room.

        // Front: the module's own entrance, always, at z = 0.
        Wall("Wall_Front", t, alongZ: true, fixedCoord: -WallThick * 0.5f,
             spanCenter: 0f, spanLength: width, doors: new[] { 0f }, mat: mat);

        Wall("Wall_Back", t, alongZ: true, fixedCoord: depth + WallThick * 0.5f,
             spanCenter: 0f, spanLength: width,
             doors: backDoor ? new[] { 0f } : new float[0], mat: mat);

        Wall("Wall_Left", t, alongZ: false, fixedCoord: -halfW - WallThick * 0.5f,
             spanCenter: midX, spanLength: depth,
             doors: float.IsNaN(leftDoorX) ? new float[0] : new[] { leftDoorX },
             mat: mat);

        Wall("Wall_Right", t, alongZ: false, fixedCoord: halfW + WallThick * 0.5f,
             spanCenter: midX, spanLength: depth,
             doors: float.IsNaN(rightDoorX) ? new float[0] : new[] { rightDoorX },
             mat: mat);

        return t;
    }

    /// <summary>
    /// One wall, built as segments with a gap and a lintel at each doorway.
    ///
    /// alongZ means the wall RUNS along Z and therefore faces X - the front
    /// and back walls. The others run along X. Doors are given as positions on
    /// whichever axis the wall runs along.
    ///
    /// Primitives rather than a mesh with a hole in it, for the reason the
    /// rest of the graybox uses them: a primitive arrives with a collider that
    /// is already correct, and a room you cannot walk through is worse than a
    /// room that looks blocky.
    /// </summary>
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

            // A doorway that falls outside the wall is a module authored
            // wrong. Skipped rather than clamped: clamping would slide the
            // door somewhere nobody asked for and the exit would still not
            // line up with it.
            if (hi <= min || lo >= max) continue;

            if (lo > cursor)
                Segment($"{name}_{piece++}", t, alongZ, fixedCoord,
                        cursor, lo, 0f, Height, mat);

            Segment($"{name}_Lintel{piece}", t, alongZ, fixedCoord,
                    Mathf.Max(lo, min), Mathf.Min(hi, max),
                    DoorHeight, Height - DoorHeight, mat);

            cursor = Mathf.Max(cursor, hi);
        }

        if (cursor < max)
            Segment($"{name}_{piece}", t, alongZ, fixedCoord,
                    cursor, max, 0f, Height, mat);
    }

    /// <summary>One box of wall, from a to b along the wall's own axis.</summary>
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

    /// <summary>An exit, turned so its +X points the way you would travel
    /// through it. yaw 0 is straight on; -90 turns it to the room's left.</summary>
    static void Exit(Transform parent, string name, Vector3 pos, float yaw,
                     string label = "")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        go.AddComponent<RoomExit>().label = label;
    }

    static GameObject Box(string name, Transform parent, Vector3 localPos,
                          Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;

        // 'false' means do NOT keep world position - without it Unity rewrites
        // localPosition to compensate and silently discards what we set next.
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>
    /// Write the finished module and remove the copy used to build it.
    ///
    /// Once, at the end, with the hierarchy complete - saving as we went would
    /// have written the prefab six times per module and left the scene holding
    /// the originals, which is how a "build" menu item quietly starts adding
    /// six rooms to whatever scene happened to be open.
    /// </summary>
    static void Finish(Transform root)
    {
        var go = root.gameObject;
        PrefabUtility.SaveAsPrefabAsset(go, $"{OutputDir}/{go.name}.prefab");
        Object.DestroyImmediate(go);
    }
}
