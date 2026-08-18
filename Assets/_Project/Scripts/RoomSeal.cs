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
        var root = new GameObject("RubbleSeal");
        root.transform.SetParent(level, false);
        root.transform.localPosition = new Vector3(4.0f, 1.25f, 0f);
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

        // Room extends +X from shaft inner wall (~4) by RoomDepth 6 + walls.
        // Level origin is floor centre of shaft slice.
        Vector3 local = level.InverseTransformPoint(player.position);
        bool inHeight = local.y > -0.5f && local.y < 4.2f;
        bool inRoomX = local.x > 3.6f && local.x < 11.5f;
        bool inRoomZ = local.z > -4.2f && local.z < 4.2f;
        return inHeight && inRoomX && inRoomZ;
    }

    static void DestroyLootInRoom(Transform level)
    {
        Vector3 origin = level.position;
        foreach (var c in Object.FindObjectsByType<Carryable>(FindObjectsSortMode.None))
        {
            if (c == null || c.State != Carryable.CarryState.Free) continue;
            Vector3 local = level.InverseTransformPoint(c.transform.position);
            if (local.y > -0.5f && local.y < 4.2f &&
                local.x > 3.6f && local.x < 11.5f &&
                local.z > -4.2f && local.z < 4.2f)
            {
                Object.Destroy(c.gameObject);
            }
        }
    }
}
