// RoomTape.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/RoomTape.cs
//
// Tapes the doorway of a floor the crew has stripped bare and banked.
//
// The counterpart to RoomSeal, and deliberately its opposite in meaning. Both
// shut a doorway; rubble says the building took the floor from you, tape says
// you finished it. So this is built to be READ ACROSS THE SHAFT: bright yellow
// against grey concrete, so a floor you have already beaten is obvious from
// inside the lift without riding to it.
//
// The lift still travels here. Only the doorway is shut - requested that way
// on 10 Sep: "even if you click floor 1 in elevator it can go but you can't go
// inside". A refusal at the dashboard would have been cheaper and worse: you
// would never see your own progress.

using UnityEngine;

/// <summary>
/// Builds the caution-tape barrier across a cleared floor's shaft doorway.
/// </summary>
public static class RoomTape
{
    /// <summary>Name of the root object, and the idempotency check.</summary>
    const string RootName = "CautionTape";

    const float WallThick = 0.5f;

    /// <summary>Half the door opening. Grayboxbuilder: DoorWidth 2, DoorHeight 2.5.</summary>
    const float HalfDoorW = 1.0f;
    const float DoorH = 2.5f;

    /// <summary>
    /// The doorway plane, derived rather than typed.
    ///
    /// Grayboxbuilder: ShaftInner 14 gives a half-width of 7, and the east
    /// wall is WallThick 0.5 on top of that - so the wall spans x 7.0 to 7.5
    /// and its centre, the doorway plane, is 7.25.
    /// FloorDirector.ShaftDoorwayLocal.x is the OUTER face at 7.5, where
    /// generated rooms attach, so half a wall back from it is the plane.
    ///
    /// Taken from that constant instead of written as 7.25, because a typed
    /// number is exactly how RoomSeal ended up at 4.0: right for the shaft it
    /// was authored against, and silently wrong once the shaft was widened.
    /// </summary>
    static float DoorwayX => FloorDirector.ShaftDoorwayLocal.x - WallThick * 0.5f;

    static Material yellow;
    static Material grey;

    /// <summary>
    /// Tape the doorway of this level. Does nothing if it is already taped,
    /// so this is safe to call every time the campaign changes - the same
    /// contract RebuildRubbleFromCampaign relies on.
    /// </summary>
    public static void TapeDoorway(Transform level)
    {
        if (level == null) return;
        if (level.Find(RootName) != null) return;      // already taped

        var root = new GameObject(RootName);
        root.transform.SetParent(level, false);
        root.transform.localPosition = new Vector3(DoorwayX, DoorH * 0.5f, 0f);
        root.transform.localRotation = Quaternion.identity;

        int env = LayerMask.NameToLayer("Environment");

        // Seeded from the level so a floor's tape looks the same on every
        // machine and every round. UnityEngine.Random would make each client
        // hang it differently - harmless here, but this project does not use
        // it for anything that shows.
        var rng = new System.Random(level.GetInstanceID() ^ 0x7A9E);

        MakeMaterials();

        // ---- THE YELLOW STRIPS ----
        //
        // Seven, spread over the full height of the opening, each tilted a few
        // degrees. The tilt is the whole trick: perfectly level strips read as
        // a fence, and crooked ones read as something a person taped up in a
        // hurry.
        const int strips = 7;

        for (int i = 0; i < strips; i++)
        {
            var tape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tape.name = $"Tape_{i:00}";
            tape.transform.SetParent(root.transform, false);

            float t = strips == 1 ? 0.5f : i / (float)(strips - 1);
            float y = Mathf.Lerp(-1.05f, 1.05f, t);

            // Sized to the 2m opening with a little overhang onto the frame,
            // the way tape is actually stuck down. It used to be 2.24-2.40,
            // which hung well past the frame on both sides.
            float span = HalfDoorW * 2f + 0.04f + (float)rng.NextDouble() * 0.1f;

            tape.transform.localScale = new Vector3(0.03f, 0.085f, span);

            // Hung on the SHAFT side of the wall (x 7.0 is the inner face),
            // so it reads from inside the lift rather than from the room
            // nobody is going to be standing in.
            tape.transform.localPosition = new Vector3(
                -0.22f,
                y + (float)(rng.NextDouble() - 0.5) * 0.06f,
                (float)(rng.NextDouble() - 0.5) * 0.05f);

            // Tilt about the doorway's own normal (+X), which is what leans a
            // horizontal strip. Alternating sign keeps it from looking combed.
            float tilt = (2.5f + (float)rng.NextDouble() * 6f) * (i % 2 == 0 ? 1f : -1f);
            tape.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);

            tape.GetComponent<MeshRenderer>().sharedMaterial = yellow;
            if (env >= 0) tape.layer = env;

            // The strips are decoration. The blocker below does the stopping,
            // so these must not catch a player or a thrown crate on an edge.
            Object.Destroy(tape.GetComponent<Collider>());
        }

        // ---- THE GREY ANCHORS ----
        //
        // Two vertical runs down the door frame, the way real tape is anchored
        // before the yellow goes across it. Cheap, and they stop the yellow
        // strips looking like they float.
        for (int side = 0; side < 2; side++)
        {
            var anchor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anchor.name = side == 0 ? "Anchor_L" : "Anchor_R";
            anchor.transform.SetParent(root.transform, false);

            anchor.transform.localScale = new Vector3(0.03f, DoorH - 0.15f, 0.11f);
            anchor.transform.localPosition = new Vector3(
                -0.2f, 0f, side == 0 ? -HalfDoorW + 0.04f : HalfDoorW - 0.04f);

            anchor.GetComponent<MeshRenderer>().sharedMaterial = grey;
            if (env >= 0) anchor.layer = env;

            Object.Destroy(anchor.GetComponent<Collider>());
        }

        // ---- WHAT ACTUALLY STOPS YOU ----
        //
        // Invisible, because tape you cannot see through is a wall, and the
        // point is to look in at a floor you already emptied. One box across
        // the whole opening rather than collision on each strip: a player
        // should be stopped, not wedged between two pieces of tape.
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "TapeBlocker";
        block.transform.SetParent(root.transform, false);
        // Fills the opening exactly and sits IN the wall, not in front of it.
        block.transform.localPosition = Vector3.zero;
        block.transform.localScale = new Vector3(0.4f, DoorH + 0.05f, HalfDoorW * 2f);

        var blockRenderer = block.GetComponent<MeshRenderer>();
        if (blockRenderer != null) blockRenderer.enabled = false;

        if (env >= 0) block.layer = env;
    }

    /// <summary>Remove the tape, if this level has any.</summary>
    public static void Untape(Transform level)
    {
        if (level == null) return;

        var root = level.Find(RootName);
        if (root != null) Object.Destroy(root.gameObject);
    }

    static void MakeMaterials()
    {
        if (yellow != null && grey != null) return;

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return;

        // Emission as well as base colour, because the shelter is dark and
        // unlit yellow reads as brown. This is the one thing on a dead floor
        // that has to be visible from across the shaft.
        yellow = new Material(sh);
        yellow.SetColor("_BaseColor", new Color(0.95f, 0.78f, 0.05f));
        yellow.SetFloat("_Smoothness", 0.25f);
        yellow.EnableKeyword("_EMISSION");
        yellow.SetColor("_EmissionColor", new Color(0.35f, 0.28f, 0.02f));

        grey = new Material(sh);
        grey.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.6f));
        grey.SetFloat("_Smoothness", 0.1f);
    }
}
