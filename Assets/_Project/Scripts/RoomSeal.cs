// RoomSeal.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/RoomSeal.cs
//
// Seals a graybox room doorway with rubble instead of hiding the whole floor.
// If a player is inside when it seals, the run is lost.

using UnityEngine;

public static class RoomSeal
{
    /// <summary>
    /// Build a rock plug in the east doorway of a Level_XX transform and
    /// destroy free loot still sitting in that room volume.
    /// </summary>
    public static void SealDoorway(Transform level, Material rubbleMat)
    {
        if (level == null) return;

        // Graybox doorway faces +X in local space (Wall_East opening).
        // Plug sits on the threshold so you cannot walk in from the shaft.
        //
        // ---- THIS WAS 4.0, AND THAT WAS 3.25m TOO SHORT ----
        //
        // Grayboxbuilder's ShaftInner was widened to 14, which puts the east
        // wall at x 7.0-7.5 and the doorway plane at 7.25. The 4.0 written
        // here was right for the narrower shaft this was authored against, and
        // nothing failed when the shaft grew - the rubble simply hung in
        // mid-air inside the shaft, three metres in front of the door it was
        // meant to plug. Reported from a screenshot on 10 Sep.
        //
        // Derived from FloorDirector.ShaftDoorwayLocal (the outer wall face)
        // rather than typed again, so widening the shaft a second time cannot
        // do this a second time.
        var root = new GameObject("RubbleSeal");
        root.transform.SetParent(level, false);
        root.transform.localPosition =
            new Vector3(FloorDirector.ShaftDoorwayLocal.x - 0.25f, 1.25f, 0f);
        root.transform.localRotation = Quaternion.identity;

        int env = LayerMask.NameToLayer("Environment");
        System.Random rng = new System.Random(level.GetInstanceID() ^ 0x5EED);

        // Chunk pile — readable silhouette, blocks capsule
        for (int i = 0; i < 14; i++)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{i:00}";
            rock.transform.SetParent(root.transform, false);

            float sx = 0.45f + (float)rng.NextDouble() * 0.7f;
            float sy = 0.35f + (float)rng.NextDouble() * 0.9f;
            float sz = 0.45f + (float)rng.NextDouble() * 0.7f;
            rock.transform.localScale = new Vector3(sx, sy, sz);

            float x = -0.3f + (float)rng.NextDouble() * 0.9f;
            float y = -0.9f + (i % 5) * 0.45f + (float)rng.NextDouble() * 0.15f;
            float z = -0.9f + (float)rng.NextDouble() * 1.8f;
            rock.transform.localPosition = new Vector3(x, y, z);
            rock.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 40f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 40f);

            if (rubbleMat != null)
                rock.GetComponent<MeshRenderer>().sharedMaterial = rubbleMat;

            if (env >= 0) rock.layer = env;
            rock.isStatic = true;
        }

        // Solid blocker matching door opening (2m wide x 2.5m high)
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "DoorBlocker";
        block.transform.SetParent(root.transform, false);
        block.transform.localPosition = new Vector3(0.15f, 0.0f, 0f);
        block.transform.localScale = new Vector3(0.7f, 2.6f, 2.15f);
        if (rubbleMat != null)
            block.GetComponent<MeshRenderer>().sharedMaterial = rubbleMat;
        if (env >= 0) block.layer = env;
        block.isStatic = true;

        DestroyLootInRoom(level);
    }

    public static bool IsPlayerInside(Transform level, Transform player)
    {
        if (level == null || player == null) return false;

        return InRoomVolume(level.InverseTransformPoint(player.position));
    }

    // ====================================================================
    // WHERE THE ROOM ACTUALLY IS
    //
    // Read off Grayboxbuilder rather than remembered:
    //
    //   ShaftInner 14              -> half 7, east wall x 7.0 to 7.5
    //   roomBackX 13.75            -> back wall centre, interior face 13.5
    //   Room_Wall_N/S at z +/-7.25 -> interior z -7 to +7
    //   FloorHeight 5              -> interior y 0 to 5
    //
    // The old bounds were x 3.6 to 11.5, z +/-4.2, y up to 4.2. That set was
    // wrong at BOTH ends, and in a way that never announced itself: it counted
    // three metres of open shaft as "inside the room", so a player standing
    // safely in the lift could be taken by a seal; and it missed the back two
    // metres and the outer thirds, so a player who really was in the room
    // could be missed. Same miscalibration as the 4.0 above - written for the
    // narrow shaft, never revisited when it was widened.
    // ====================================================================

    static bool InRoomVolume(Vector3 local) =>
        local.y > -0.5f && local.y < 5.0f &&
        local.x > 7.4f && local.x < 13.5f &&
        local.z > -7.0f && local.z < 7.0f;

    static void DestroyLootInRoom(Transform level)
    {
        foreach (var c in Object.FindObjectsByType<Carryable>(FindObjectsSortMode.None))
        {
            if (c == null || c.State != Carryable.CarryState.Free) continue;
            if (InRoomVolume(level.InverseTransformPoint(c.transform.position)))
                Object.Destroy(c.gameObject);
        }
    }
}
