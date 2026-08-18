// ElevatorBuilder.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/ElevatorBuilder.cs
//
// THE FOLDER MUST BE NAMED "Editor". Unity treats any folder called Editor as
// tooling that runs in the editor only and strips it from builds. Put this
// anywhere else and the game will fail to build, because UnityEditor code
// cannot ship in a player.
//
// USE:  menu bar -> SAFE DEPOSIT -> Build Elevator Car
//
// ====================================================================
// ELEVATOR_SPEC STEP 3 - THE CAR, STATIC.
//
// Geometry and anchors. No movement, no scripts on it, no dashboard UI, no
// bridge, no load. The bar for this step is one sentence:
//
//     "you can walk into it and it looks like somewhere you'd argue"
//
// Everything here exists to be replaced or driven later, so the shape of the
// file matters as much as the shape of the box: every part a later step needs
// to find is a NAMED CHILD or a NAMED EMPTY, never a magic offset. Step 5
// looks up DashboardAnchor, Step 7 looks up the shutters, Step 8 looks up
// DeckAnchor. None of them should have to know a number from this file.
// ====================================================================
//
// Rebuilt from scratch on every run, like GrayboxBuilder. Change a constant,
// click the menu item, look at the result.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class ElevatorBuilder
{
    // ------------------------------------------------------------------
    // DIMENSIONS - the only numbers you should need to edit.
    // 1 unit = 1 metre.
    //
    // These are not free choices. GrayboxBuilder builds a 14 m inner shaft
    // with 4 m floor spacing, a 2 m wide door and a 2.5 m high one, so:
    //
    //   CarInner 4      leaves a 4.9 m gap on every side of the shaft - see
    //                   GrayboxBuilder.ShaftInner for the jump-distance math
    //                   this is sized against. That gap is not slack, it is
    //                   exactly what the BRIDGE spans in Step 7, and it is
    //                   deliberately wider than a player can jump: falling
    //                   should read as certain death, not an embarrassing
    //                   near-miss. Widen the car and you shrink the gap back
    //                   toward jumpable; narrow it and the bridge gets silly.
    //
    //   CarHeight 2.8   fits inside the 4 m floor pitch with room above for
    //                   the roof and the cable hitch.
    //
    //   DoorHeight 2.3  deliberately UNDER the shaft's 2.5 m doorway, so the
    //                   shutter reads as a shutter inside a frame rather
    //                   than as a wall that happens to move.
    // ------------------------------------------------------------------

    const float CarInner   = 4f;      // interior width and depth
    const float WallThick  = 0.12f;   // steel cage, not concrete
    const float CarHeight  = 2.8f;    // floor surface to ceiling underside
    const float DoorWidth  = 2f;
    const float DoorHeight = 2.3f;
    const float PostSize   = 0.16f;   // corner uprights

    const float DeckSize   = 2.6f;    // marked-out cargo area on the floor

    // Where to park it so you can look at it. Level_01's floor sits at
    // world y = -4 (GrayboxBuilder puts level i at -FloorHeight * i), and the
    // car's local origin is its FLOOR SURFACE, so -4 lines the car's floor up
    // with the room's floor and you can walk straight across.
    const float ParkY = -4f;

    // EVERY SHUTTER IS BUILT CLOSED.
    //
    // Step 3 baked one open so the car could be walked into. Step 4 drives
    // them from Elevator.cs, which captures each shutter's CLOSED pose at
    // Awake and derives the open one from it - so the builder has to hand
    // over a consistent starting state, and "closed" is the only one that is
    // the same for all four.
    //
    // The alternative was for Elevator.cs to hard-code DoorHeight to work out
    // where a shutter belongs. Two files owning the same number is how you
    // get a door that opens to the wrong height six steps later and no idea
    // which file lied.

    const string SteelMaterialPath  = "Assets/_Project/Materials/M_ElevatorSteel.mat";
    const string HazardMaterialPath = "Assets/_Project/Materials/M_ElevatorHazard.mat";
    const string PanelMaterialPath  = "Assets/_Project/Materials/M_ElevatorPanel.mat";
    const string GlowMaterialPath   = "Assets/_Project/Materials/M_ElevatorGlow.mat";
    const string ScreenMaterialPath = "Assets/_Project/Materials/M_ElevatorScreen.mat";

    const string PrefabPath = "Assets/_Project/Prefabs/Elevator.prefab";
    const string RootName   = "ELEVATOR";

    [MenuItem("SAFE DEPOSIT/Build Elevator Car")]
    static void Build()
    {
        DestroyIfPresent(RootName);

        // Derived once, so changing CarInner keeps everything correct.
        float half     = CarInner * 0.5f;                 // 2.00  inner wall face
        float wallMid  = half + WallThick * 0.5f;         // 2.06  centre of a wall
        float wallSpan = CarInner + WallThick * 2f;       // 4.24  outer extent

        Material steel  = GetOrCreateMaterial(SteelMaterialPath,  new Color(0.34f, 0.36f, 0.38f));
        Material hazard = GetOrCreateMaterial(HazardMaterialPath, new Color(0.85f, 0.68f, 0.12f));
        Material panel  = GetOrCreateMaterial(PanelMaterialPath,  new Color(0.11f, 0.12f, 0.14f));
        Material glow   = GetOrCreateMaterial(GlowMaterialPath,   new Color(1f, 0.94f, 0.82f),
                                              new Color(1f, 0.90f, 0.72f) * 2.2f);

        // A DARK display, not a lamp.
        //
        // The screen was the same emissive material as the ceiling light, and
        // under a 2.6-intensity lamp 1.5m away it blew out to pure white and
        // swallowed the number written on it. A readout works the way a real
        // one does: a dark face, and the GLYPHS are the only thing emitting.
        Material screen = GetOrCreateMaterial(ScreenMaterialPath, new Color(0.05f, 0.07f, 0.09f));

        // ROOT IS EMPTY ON PURPOSE.
        //
        // Step 4 puts Elevator.cs on this object and moves it. Keeping the
        // root free of geometry means the thing that moves has no mesh, no
        // collider and no pivot surprises - it is a coordinate, exactly like
        // GrayboxBuilder's Winch_Anchor.
        var root = new GameObject(RootName);
        root.transform.position = new Vector3(0f, ParkY, 0f);

        var car = new GameObject("Car");
        car.transform.SetParent(root.transform, false);

        // ---- floor and ceiling ----
        //
        // The floor's TOP face sits at local y = 0, which is why its centre is
        // half a thickness below zero. A transform position is the CENTRE of
        // an object, never a corner. y = 0 is therefore "the surface a player
        // stands on", which is what every other number here is measured from.
        Box("Floor", car.transform,
            new Vector3(0f, -WallThick * 0.5f, 0f),
            new Vector3(wallSpan, WallThick, wallSpan), steel);

        Box("Ceiling", car.transform,
            new Vector3(0f, CarHeight + WallThick * 0.5f, 0f),
            new Vector3(wallSpan, WallThick, wallSpan), steel);

        // ---- corner uprights ----
        var frame = new GameObject("Frame");
        frame.transform.SetParent(car.transform, false);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box($"Post_{(sx < 0 ? "W" : "E")}{(sz < 0 ? "S" : "N")}", frame.transform,
                    new Vector3(sx * wallMid, CarHeight * 0.5f, sz * wallMid),
                    new Vector3(PostSize, CarHeight, PostSize), steel);

        // ---- the four sides ----
        //
        // Built as rotated child groups rather than four hand-written sets of
        // coordinates. Each side is authored once, facing +Z, and the group's
        // yaw puts it where it belongs - the same trick BuildLevel uses to
        // face doorways in different directions. One place to fix a mistake.
        BuildSide(car.transform, "Side_North",   0f, wallMid, wallSpan, steel, panel);
        BuildSide(car.transform, "Side_East",   90f, wallMid, wallSpan, steel, panel);
        BuildSide(car.transform, "Side_South", 180f, wallMid, wallSpan, steel, panel);
        BuildSide(car.transform, "Side_West",  270f, wallMid, wallSpan, steel, panel);

        BuildDeck(car.transform, hazard);
        BuildDashboard(car.transform, half, steel, panel, screen, hazard);
        BuildScanner(car.transform, half, steel, glow);
        BuildLight(car.transform, glow);
        BuildCableHitch(car.transform, steel);

        // ENVIRONMENT LAYER, BUT NOT STATIC.
        //
        // GrayboxBuilder marks the shaft static because it never moves. This
        // must NOT be static: Step 4 moves it, and Unity's static batching
        // bakes a static object's transform into a shared mesh. A moving
        // static object either does not move or drags the whole batch with
        // it, and neither failure says why.
        int envLayer = LayerMask.NameToLayer("Environment");
        if (envLayer >= 0) SetLayerRecursive(root, envLayer);
        else Debug.LogWarning("[Elevator] Layer 'Environment' missing. Create it in Tags and Layers.");

        // ---- Step 4: the components that make it move ----
        //
        // Added here rather than left for hand-dragging, for the same reason
        // the geometry is built in code: the prefab has to be reproducible
        // from a menu click, or a rebuild silently loses whatever was wired
        // up by hand last time.
        //
        // Elevator carries [RequireComponent(typeof(Rigidbody))], so the
        // kinematic body arrives with it and configures itself in Awake.
        var lift = root.AddComponent<Elevator>();
        lift.floorHeight = 4f;      // GrayboxBuilder.FloorHeight
        lift.lowestFloor = 5;       // GrayboxBuilder.LevelCount - Step 11 raises to 20
        lift.activeSide = "Side_East";

        root.AddComponent<ElevatorCable>();

        SaveAsPrefab(root);

        Undo.RegisterCreatedObjectUndo(root, "Build Elevator Car");
        Selection.activeGameObject = root;

        Debug.Log($"[Elevator] {CarInner}x{CarInner}m car at y={ParkY}. " +
                  $"All shutters closed - Elevator.cs opens the active one. " +
                  $"Prefab saved to {PrefabPath}.");
    }

    // ------------------------------------------------------------------
    // ONE SIDE
    //
    // A cube cannot have a hole in it, so the opening is made by building the
    // wall AROUND it - a piece left, a piece right, a lintel above. The gap
    // left over is the doorway. Same approach as BuildLevel's east wall.
    //
    // The shutter is a separate named child in both states, because Step 7
    // has to find and drive it. It is never deleted, only moved and resized -
    // an object that sometimes does not exist is far harder to script than
    // one that is always there in a different pose.
    // ------------------------------------------------------------------

    static void BuildSide(Transform car, string name, float yaw,
                          float wallMid, float wallSpan, Material steel, Material panel)
    {
        var side = new GameObject(name);
        side.transform.SetParent(car, false);
        side.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        float sideWidth   = (wallSpan - DoorWidth) * 0.5f;             // 1.12
        float sideCenterX = DoorWidth * 0.5f + sideWidth * 0.5f;       // 1.56
        float lintelH     = CarHeight - DoorHeight;                    // 0.50

        Box("Post_L", side.transform,
            new Vector3(-sideCenterX, DoorHeight * 0.5f, wallMid),
            new Vector3(sideWidth, DoorHeight, WallThick), steel);

        Box("Post_R", side.transform,
            new Vector3(sideCenterX, DoorHeight * 0.5f, wallMid),
            new Vector3(sideWidth, DoorHeight, WallThick), steel);

        Box("Lintel", side.transform,
            new Vector3(0f, DoorHeight + lintelH * 0.5f, wallMid),
            new Vector3(wallSpan, lintelH, WallThick), steel);

        // The drum the shutter rolls onto. Present on every side whether the
        // shutter is up or down, because a shutter with nowhere to go reads
        // as a mistake the moment anyone looks up.
        Box("Shutter_Drum", side.transform,
            new Vector3(0f, DoorHeight + 0.16f, wallMid - 0.06f),
            new Vector3(DoorWidth + 0.12f, 0.22f, 0.22f), steel);

        // Closed. Elevator.cs reads this pose at Awake and rolls it up from
        // here, so this is the single definition of where a shutter lives.
        Box("Shutter", side.transform,
            new Vector3(0f, DoorHeight * 0.5f, wallMid),
            new Vector3(DoorWidth, DoorHeight, WallThick * 0.7f), panel);
    }

    // ------------------------------------------------------------------
    // CARGO DECK
    //
    // Markings, not a container. The loot sits on the car floor like anything
    // else; this only tells the crew where "on the deck" means, so that in
    // Step 8 nobody argues about whether the crate they dumped in a doorway
    // counts. Centred deliberately: the pile of cargo belongs in the middle
    // of the room with four people standing round it.
    // ------------------------------------------------------------------

    static void BuildDeck(Transform car, Material hazard)
    {
        var deck = new GameObject("Deck");
        deck.transform.SetParent(car, false);

        // A hair above the floor so it does not z-fight with it. Two coplanar
        // surfaces flicker against each other and it looks like a broken
        // shader rather than a broken offset.
        const float y = 0.006f;
        const float stripe = 0.09f;
        float h = DeckSize * 0.5f;

        Box("Edge_N", deck.transform, new Vector3(0f, y,  h), new Vector3(DeckSize, 0.012f, stripe), hazard);
        Box("Edge_S", deck.transform, new Vector3(0f, y, -h), new Vector3(DeckSize, 0.012f, stripe), hazard);
        Box("Edge_E", deck.transform, new Vector3( h, y, 0f), new Vector3(stripe, 0.012f, DeckSize), hazard);
        Box("Edge_W", deck.transform, new Vector3(-h, y, 0f), new Vector3(stripe, 0.012f, DeckSize), hazard);

        // Markings must never block anything. A player walking over the deck
        // catching on a 12mm lip would be a bug nobody would think to look
        // for down here.
        foreach (var col in deck.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);

        Anchor("DeckAnchor", deck.transform, Vector3.zero);
    }

    // ------------------------------------------------------------------
    // DASHBOARD
    //
    // On a wall SEGMENT, not a whole wall - every side of this car has a door
    // in it, so the only flat metre of wall available is beside one. It goes
    // on the west side's north segment.
    //
    // DashboardAnchor is where Step 5 flies the camera to. It sits in front
    // of the panel looking AT it, so that step never has to know where the
    // panel is or which way it faces.
    // ------------------------------------------------------------------

    static void BuildDashboard(Transform car, float half,
                               Material steel, Material panel, Material screen, Material hazard)
    {
        float sideWidth   = (CarInner + WallThick * 2f - DoorWidth) * 0.5f;
        float sideCenterZ = DoorWidth * 0.5f + sideWidth * 0.5f;

        var dash = new GameObject("Dashboard");
        dash.transform.SetParent(car, false);
        // Sits against the west wall, facing east into the car.
        dash.transform.localPosition = new Vector3(-half + 0.02f, 1.15f, sideCenterZ);
        dash.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        // Housing, then the fascia tilted back so it faces a standing player
        // rather than the opposite wall.
        Box("Housing", dash.transform, new Vector3(0f, 0f, 0.09f),
            new Vector3(1.0f, 0.78f, 0.18f), steel);

        var face = new GameObject("Face");
        face.transform.SetParent(dash.transform, false);
        face.transform.localPosition = new Vector3(0f, 0.02f, 0.18f);
        face.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);

        Box("Fascia", face.transform, Vector3.zero,
            new Vector3(0.92f, 0.70f, 0.04f), panel);

        // ---- the screen ----
        //
        // Front face lands at z = 0.026. Everything drawn ON the screen must
        // sit in FRONT of that or the box hides it - the readout was at 0.012
        // and the white slab ate the bottom half of the number.
        Box("Screen", face.transform, new Vector3(0f, 0.20f, 0.021f),
            new Vector3(0.78f, 0.22f, 0.01f), screen);

        // Real world-space text, not a canvas. The panel is an object in a
        // room, so its readout has to be lit by the cage light, hidden when
        // somebody stands in front of it, and visible to the rest of the crew.
        // A screen-space label would be none of those things.
        //
        // TextMesh rather than TextMeshPro: TMP's runtime ships with the
        // project but its essential assets are a manual import, and a missing
        // font renders nothing at all with no error.
        Label("FloorText", face.transform, new Vector3(0f, 0.20f, 0.034f),
              "--", 0.0070f, new Color(0.35f, 0.92f, 1f), bold: true);

        // Step 8 hangs the load gauge here.
        Anchor("ScreenAnchor", face.transform, new Vector3(0f, 0.20f, 0.04f));

        // ---- STEP 6: numeric keypad, left. UP / DOWN, right. ----
        //
        // Everything below the screen splits into two zones rather than
        // stacking every control in one column: a real elevator puts call
        // arrows to one side and floor entry in the middle, and doing the
        // same here is what makes twelve new buttons fit next to the two
        // that already existed without either zone feeling like an
        // afterthought.
        //
        // Below-screen area is x[-0.46, 0.46], y[-0.35, 0.09].
        const float BelowY0 = -0.35f, BelowY1 = 0.09f;
        const float KeypadX0 = -0.46f, KeypadX1 = 0.16f;   // 0.62m wide
        const float ArrowsX0 = 0.16f, ArrowsX1 = 0.46f;    // 0.30m wide

        // 3 columns x 4 rows: 1-9, then CLR / 0 / GO.
        float colW = (KeypadX1 - KeypadX0) / 3f;
        float rowH = (BelowY1 - BelowY0) / 4f;
        Vector3 keySize = new Vector3(colW - 0.03f, rowH - 0.02f, 0.05f);

        string[,] keys = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "CLR", "0", "GO" } };

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string label = keys[row, col];
                float cx = KeypadX0 + colW * (col + 0.5f);
                float cy = BelowY1 - rowH * (row + 0.5f);

                ElevatorButton.Kind kind = label switch
                {
                    "CLR" => ElevatorButton.Kind.Clear,
                    "GO" => ElevatorButton.Kind.Go,
                    _ => ElevatorButton.Kind.Digit
                };

                // GO gets the hazard-yellow accent so it reads as the one
                // button that commits you to something, the same colour
                // language the deck stripes already use for "this matters".
                Material body = kind == ElevatorButton.Kind.Go ? hazard : steel;

                var go = MakeButton(face.transform, $"Key_{row}_{col}",
                                    new Vector3(cx, cy, 0.045f), kind, label, body, keySize, 0.0026f);

                if (kind == ElevatorButton.Kind.Digit)
                    go.GetComponent<ElevatorButton>().digit = int.Parse(label);
            }
        }

        // UP / DOWN keep their own stacked shape, just narrower and moved
        // into the right-hand column instead of spanning the whole fascia.
        float arrowsCx = (ArrowsX0 + ArrowsX1) * 0.5f;
        float arrowW = ArrowsX1 - ArrowsX0 - 0.03f;
        float arrowH = (BelowY1 - BelowY0) * 0.5f - 0.02f;
        float arrowUpCy = BelowY1 - (BelowY1 - BelowY0) * 0.25f;
        float arrowDownCy = BelowY0 + (BelowY1 - BelowY0) * 0.25f;

        // labelSize dropped from the original 0.0042 to 0.0022. Label() sizes
        // text in WORLD units, independent of the button's own box size -
        // shrinking the button from 0.52 wide to 0.27 wide did nothing to
        // the text drawn on it, so "DOWN" kept its original physical width
        // and spilled off both edges of a button now half as wide.
        MakeButton(face.transform, "Button_Up", new Vector3(arrowsCx, arrowUpCy, 0.045f),
                   ElevatorButton.Kind.Up, "UP", steel, new Vector3(arrowW, arrowH, 0.05f), 0.0022f);
        MakeButton(face.transform, "Button_Down", new Vector3(arrowsCx, arrowDownCy, 0.045f),
                   ElevatorButton.Kind.Down, "DOWN", steel, new Vector3(arrowW, arrowH, 0.05f), 0.0022f);

        // WHERE THE CAMERA STANDS.
        //
        // Pulled back again for Step 6: at 0.62 back the fascia's HORIZONTAL
        // centre (x=0, where FloorText sits) framed correctly, but the panel
        // went from two centred buttons to controls using the fascia's full
        // 0.92 x 0.70 extent - and at that distance the whole assembly no
        // longer fit inside frame, cropping one side while leaving empty
        // space on the other.
        //
        // Distance grew to 1.15; the up offset grew with it to hold the same
        // ~14 degree look angle (up = back * tan(14 deg)). Change one of
        // these three numbers and the panel slides off centre again, so
        // change them together.
        var look = Anchor("DashboardAnchor", dash.transform, new Vector3(0f, 0.29f, 1.15f));
        look.transform.localRotation = Quaternion.Euler(14f, 180f, 0f);

        // Step 5. Finds its Elevator via GetComponentInParent and its anchor
        // by name at runtime, so it does not care that the root does not have
        // Elevator on it yet at this point in the build.
        dash.AddComponent<ElevatorDashboard>();
    }

    // ------------------------------------------------------------------
    // PRICE SCANNER  (Step 9 fills this in)
    //
    // A waist-high plinth you hold an item against. Put deliberately across
    // the car from the dashboard: the argument about what to leave behind
    // should happen between two people standing at two different machines,
    // not over one person's shoulder.
    // ------------------------------------------------------------------

    static void BuildScanner(Transform car, float half, Material steel, Material glow)
    {
        var scan = new GameObject("Scanner");
        scan.transform.SetParent(car, false);
        scan.transform.localPosition = new Vector3(half - 0.34f, 0f, -1.45f);

        Box("Plinth", scan.transform, new Vector3(0f, 0.45f, 0f),
            new Vector3(0.5f, 0.9f, 0.5f), steel);
        Box("Pad", scan.transform, new Vector3(0f, 0.92f, 0f),
            new Vector3(0.38f, 0.03f, 0.38f), glow);

        Anchor("ScannerAnchor", scan.transform, new Vector3(0f, 1.0f, 0f));
    }

    // ------------------------------------------------------------------
    // CAGE LIGHT
    //
    // GAME_DESIGN calls this the only reliable light in the game, so it is a
    // real Light and not an emissive box pretending. Warm, because every
    // other light source down here is a cold headlamp - walking back into the
    // car should feel like walking indoors.
    // ------------------------------------------------------------------

    static void BuildLight(Transform car, Material glow)
    {
        var fixt = new GameObject("CageLight");
        fixt.transform.SetParent(car, false);
        fixt.transform.localPosition = new Vector3(0f, CarHeight - 0.04f, 0f);

        var shade = Box("Fixture", fixt.transform, Vector3.zero,
                        new Vector3(0.7f, 0.07f, 0.7f), glow);
        Object.DestroyImmediate(shade.GetComponent<Collider>());

        var lamp = new GameObject("Lamp");
        lamp.transform.SetParent(fixt.transform, false);
        lamp.transform.localPosition = new Vector3(0f, -0.12f, 0f);

        var light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.91f, 0.76f);

        // 1.5, down from 2.6. At 2.6 the hotspot on the dashboard washed the
        // whole panel to white and swallowed the readout. The car still wants
        // to feel like the one warm room in the building - that comes from it
        // being the ONLY light, not from it being a searchlight.
        light.intensity = 1.5f;
        light.range = 7f;
        light.shadows = LightShadows.Soft;
    }

    // ------------------------------------------------------------------
    // CABLE HITCH
    //
    // Where the hoist rope lands. The rope itself is drawn by
    // ElevatorCable.cs once there is movement to draw it against - a static
    // cable on a static car is a cylinder of nothing.
    //
    // It matters that this is visible from inside: the wire rope is what the
    // shop sells, and the load limit needs a physical object attached to it.
    // ------------------------------------------------------------------

    static void BuildCableHitch(Transform car, Material steel)
    {
        var hitch = new GameObject("CableHitch");
        hitch.transform.SetParent(car, false);
        hitch.transform.localPosition = new Vector3(0f, CarHeight + WallThick, 0f);

        Box("Plate", hitch.transform, new Vector3(0f, 0.03f, 0f),
            new Vector3(0.55f, 0.06f, 0.55f), steel);
        Box("Lug", hitch.transform, new Vector3(0f, 0.16f, 0f),
            new Vector3(0.14f, 0.22f, 0.14f), steel);

        Anchor("CableAnchor", hitch.transform, new Vector3(0f, 0.27f, 0f));
    }

    // ------------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------------

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        // CreatePrimitive gives a cube WITH a MeshRenderer and a BoxCollider
        // already attached - collision for free.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;

        // The 'false' means do NOT preserve world position. Without it Unity
        // keeps the object where it currently sits and rewrites localPosition
        // to compensate, silently discarding the coordinates set next.
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        // sharedMaterial, not material - assigning .material in the editor
        // clones it for every object and leaves dozens of duplicates.
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        return go;
    }

    /// <summary>
    /// A physical button: a box that sticks out of the fascia, with a
    /// collider to raycast against and a label sitting on its face.
    ///
    /// size and labelSize both default to the values the original UP/DOWN
    /// buttons shipped with, so nothing about those two changes just because
    /// the keypad now calls this with its own, smaller numbers.
    /// </summary>
    static GameObject MakeButton(Transform face, string name, Vector3 localPos,
                                 ElevatorButton.Kind kind, string text, Material body,
                                 Vector3? size = null, float labelSize = 0.0042f)
    {
        // Depth 0.05 against a fascia 0.04 thick, sitting proud of it. A flush
        // button reads as a sticker; one you can see the side of reads as
        // something to push.
        var go = Box(name, face, localPos, size ?? new Vector3(0.52f, 0.17f, 0.05f), body);

        var btn = go.AddComponent<ElevatorButton>();
        btn.kind = kind;

        // Parented to the BUTTON, so it sinks with it when pressed. Parent it
        // to the fascia instead and the label floats while the button moves.
        //
        // z +0.55 in the button's own space: the box is one unit deep before
        // its 0.05 scale, so half a unit is the front face and a little more
        // lifts the text clear of it. That is true regardless of the box's
        // WIDTH or HEIGHT - Label() cancels out the parent's scale before
        // sizing the text, so a small keypad key does not need a different z.
        Label(name + "_Label", go.transform, new Vector3(0f, 0f, 0.55f),
              text, labelSize, Color.white, bold: true);

        return go;
    }

    /// <summary>
    /// World-space text.
    ///
    /// PHYSICAL SIZE IS fontSize x characterSize. RESOLUTION IS fontSize ALONE.
    ///
    /// That is the whole trick, and getting it wrong is why the first version
    /// came out blurry. Unity rasterises the glyph atlas at fontSize pixels;
    /// if the text then covers more screen pixels than that, it is upscaled
    /// and goes soft. At 96 a floor number half a metre from your face lands
    /// on roughly 160 pixels, so it was being stretched 1.7x.
    ///
    /// FontRes is therefore high and the sizes passed in are correspondingly
    /// small. To change how BIG text is, change the size argument. To change
    /// how SHARP it is, change FontRes - and then scale every size argument by
    /// the inverse, or everything silently grows.
    /// </summary>
    const int FontRes = 256;

    static GameObject Label(string name, Transform parent, Vector3 localPos,
                            string text, float size, Color colour, bool bold = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        // The label lives on the FRONT of whatever it is attached to, and a
        // child of a scaled box inherits that scale - so undo it, or text on
        // a 0.34 x 0.20 x 0.05 button comes out smeared.
        var s = parent.localScale;
        go.transform.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f);

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = FontRes;
        tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = colour;
        tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        tm.richText = false;

        // "Arial.ttf" was removed in newer Unity; this is the replacement and
        // it is always present, so no font asset has to be imported.
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // TextMesh builds its quads facing its own -Z, so a label parented to
        // something whose +Z points at the reader comes out MIRRORED. Tested,
        // not reasoned: without this, "DOWN" renders as "NWOD".
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        return go;
    }

    /// <summary>
    /// A named empty. A coordinate, not an object - later steps look these up
    /// by name so they never have to hard-code a number from this file.
    /// </summary>
    static GameObject Anchor(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go;
    }

    static void SaveAsPrefab(GameObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();

        // ...AndConnect, so the object left in the scene is an INSTANCE of the
        // prefab rather than an unrelated copy that happens to look the same.
        // Without it, the next edit to the prefab would not reach the scene
        // and you would be debugging two divergent elevators.
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
    }

    static void DestroyIfPresent(string name)
    {
        var existing = GameObject.Find(name);
        // DestroyImmediate, because the normal Destroy waits until the end of
        // a frame - and outside play mode that frame never arrives.
        if (existing != null) Object.DestroyImmediate(existing);
    }

    static Material GetOrCreateMaterial(string path, Color colour, Color? emission = null)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[Elevator] URP Lit shader not found. Is this project on URP?");
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.Refresh();

        mat = new Material(shader);
        // URP uses "_BaseColor", not the older "_Color". Setting the wrong one
        // fails silently and leaves the material white.
        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_Smoothness", 0.28f);

        if (emission.HasValue)
        {
            // The keyword is required. Set _EmissionColor without enabling
            // _EMISSION and the value is stored and then ignored, which looks
            // exactly like the colour being wrong.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

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
}
