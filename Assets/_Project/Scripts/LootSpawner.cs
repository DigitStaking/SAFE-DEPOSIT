// LootSpawner.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LootSpawner.cs
// Goes on: the LOOT root, added by GrayboxBuilder.
//
// ====================================================================
// THREE ITEMS PER FLOOR, AT THREE FIXED SLOTS.
//
// This is the shape GrayboxBuilder used before this file existed, restored
// on request after two attempts at scattering loot randomly both failed:
//
//   attempt 1  independent random draws with no memory of each other, so
//              items spawned interpenetrating and the physics solver
//              resolved the overlap by stacking them
//   attempt 2  rejection sampling plus a raycast to verify the floor -
//              which STILL stacked, because each fix only traded one
//              failure mode for another
//
// Fixed slots cannot overlap, cannot miss the floor, and cannot stack.
// Those three bugs are impossible by construction rather than avoided by
// checking for them, and "impossible" is worth more here than "clever".
//
// What stays random is WHAT lands in each slot, which is where the variety
// actually mattered: three Bulk is a cheap heavy floor you might skip, a
// Rare is a light rich one you strip in seconds.
//
// ====================================================================
// WHY THIS IS RUNTIME AND NOT PART OF GrayboxBuilder
//
// The tier draw depends on the ROUND - richer floors as income grows - and
// RunManager reloads the scene between rounds. Editor-time loot would be
// identical every round forever, which makes the economy impossible to test
// across a campaign. Spawning in Start() re-rolls per round, and skipping
// sealed rooms gets "a floor you strip stays stripped" for free.
// ====================================================================

using System.Collections.Generic;
using UnityEngine;

public class LootSpawner : MonoBehaviour
{
    // ------------------------------------------------------------------
    // THE FIVE TIERS, straight off ECONOMY_AND_CAMPAIGN.md Part 3.
    //
    // Food and medicine, not office furniture. The doc's reasoning, worth
    // keeping in front of whoever tunes these next: "A crate of beans is
    // heavy and nearly worthless. A box of antibiotics is the size of a book
    // and worth six crates. Learning to read a room and take the DENSE
    // things is the mastery."
    // ------------------------------------------------------------------

    [System.Serializable]
    public struct Tier
    {
        public string label;
        public string[] names;      // flavour, picked at random
        public int minValue, maxValue;
        public float minMass, maxMass;
        public float size;          // metres, roughly cube-shaped
        public Color colour;

        [Tooltip("Asset name in Assets/_Project/Prefabs/Loot, or empty for a " +
                 "tier-coloured box. This is the AUTHORING key; GrayboxBuilder " +
                 "resolves it into the reference below.")]
        public string prefabName;

        [Tooltip("Resolved by GrayboxBuilder at build time. It has to be a " +
                 "real reference and not a runtime lookup: Resources.Load only " +
                 "reads from a folder literally named Resources, and these " +
                 "prefabs live in Assets/_Project/Prefabs/Loot.")]
        public GameObject prefab;
    }

    public Tier[] tiers =
    {
        new Tier {
            label = "Bulk", minValue = 15, maxValue = 30, minMass = 20f, maxMass = 35f,
            size = 0.75f, colour = new Color(0.62f, 0.42f, 0.28f),
            prefabName = "Prop_LootBulk",
            names = new[] { "Canned_Goods", "Flour_Sacks", "Bottled_Water", "Dried_Beans" },
        },
        new Tier {
            label = "Common", minValue = 35, maxValue = 60, minMass = 10f, maxMass = 20f,
            size = 0.55f, colour = new Color(0.70f, 0.68f, 0.60f),
            prefabName = "Prop_LootCommon",
            names = new[] { "Dried_Stores", "Cooking_Fuel", "Salt", "Coffee" },
        },
        new Tier {
            label = "Good", minValue = 70, maxValue = 120, minMass = 4f, maxMass = 10f,
            size = 0.40f, colour = new Color(0.45f, 0.80f, 0.50f),
            prefabName = "Prop_LootGood",
            names = new[] { "Vitamins", "Sealed_Rations", "Water_Purifier_Tabs" },
        },
        new Tier {
            label = "Rare", minValue = 150, maxValue = 300, minMass = 1f, maxMass = 3f,
            size = 0.26f, colour = new Color(1f, 0.82f, 0.25f),
            prefabName = "Prop_LootRare",
            names = new[] { "Antibiotics", "Insulin", "Seed_Bank_Vials", "Baby_Formula" },
        },
        new Tier {
            label = "BulkHeavy", minValue = 250, maxValue = 400, minMass = 120f, maxMass = 250f,
            size = 1.5f, colour = new Color(0.40f, 0.44f, 0.52f),
            prefabName = "Prop_LootBulkHeavy",
            names = new[] { "Ration_Pallet", "Water_Tank", "Sealed_Freezer_Unit" },
        },
    };

    [Header("How much")]
    [Tooltip("Items per floor. Three is what the graybox always used and what " +
             "reads as 'a room with things in it' without becoming a warehouse.")]
    public int itemsPerFloor = 3;

    [Tooltip("SpawnValue(R) = LootValue(R) x this - ECONOMY Part 4b. Above 1 " +
             "means more value on the floor than the cable can lift, so you can " +
             "never clear a round and what is left is always the heavy awkward " +
             "thing nobody wanted to carry.")]
    public float spawnMultiplier = 1.4f;

    [Tooltip("Per-floor budget varies by this either way, so two floors worth " +
             "roughly the same money can still be different problems.")]
    public float floorVariance = 0.2f;

    // ------------------------------------------------------------------
    // THE THREE SLOTS, in the LEVEL's own local space.
    //
    // The room runs x 7.5..13.5 and z -7..7 (GrayboxBuilder: ShaftInner 14,
    // RoomDepth 6), so these sit comfortably inside it with metres to spare
    // on every side, and 4m apart down its length - far enough that even a
    // 1.5m pallet next to another 1.5m pallet cannot touch.
    //
    // Local space, not world, so they land correctly whichever of the four
    // directions that floor's doorway happens to face.
    // ------------------------------------------------------------------
    static readonly Vector2[] Slots =
    {
        new Vector2(10.2f, -4f),
        new Vector2( 9.4f,  0f),
        new Vector2(11.0f,  4f),
    };

    [Tooltip("Random offset applied to each slot so a room looks arranged " +
             "rather than laid out on a grid. Kept well under the 4m gap " +
             "between slots, so jitter can never close it.")]
    public float slotJitter = 0.9f;

    void Start()
    {
        var shaft = GameObject.Find("SHAFT");
        if (shaft == null)
        {
            Debug.LogWarning("[Loot] No SHAFT - run Build Graybox Shaft.");
            return;
        }

        // EVERY floor gets loot, not only the reachable ones. The building is
        // full of food whether or not your cable is long enough yet, and a
        // floor you finally reach in round 8 should have something in it.
        var levels = new List<Transform>();
        for (int floor = 1; floor <= 99; floor++)
        {
            var level = shaft.transform.Find($"Level_{floor:00}");
            if (level == null) break;                                  // ran out of floors
            if (Campaign.DestroyedRooms.Contains(floor)) continue;     // sealed - stays stripped
            levels.Add(level);
        }

        if (levels.Count == 0) return;

        // Budget is divided by the REACHABLE floor count, not the total, so
        // the money actually within reach this round matches the economy's
        // LootValue(R) rather than being spread thin across twenty floors
        // the crew cannot visit.
        int reachable = Mathf.Max(1, Campaign.DeepestReachableFloor);
        float perFloor = Campaign.Income * spawnMultiplier / reachable;

        int total = 0;
        foreach (var level in levels)
            total += FillFloor(level, perFloor * Random.Range(1f - floorVariance, 1f + floorVariance));

        Debug.Log($"[Loot] round {Campaign.RunNumber}: {total} items across " +
                  $"{levels.Count} floors, ~${perFloor:0} per floor. " +
                  $"Cable reaches floor {Campaign.DeepestReachableFloor}, lifts ~${Campaign.Income}.");
    }

    int FillFloor(Transform level, float budget)
    {
        int spawned = 0;
        int slots = Mathf.Min(itemsPerFloor, Slots.Length);

        for (int slot = 0; slot < slots; slot++)
        {
            Tier t = PickTier(budget);
            int value = Random.Range(t.minValue, t.maxValue + 1);

            SpawnItem(t, value, level, slot);
            budget -= value;
            spawned++;
        }

        return spawned;
    }

    /// <summary>
    /// A tier this floor can still afford, or the cheapest one if it cannot
    /// afford any. The floor always gets its three items - the budget decides
    /// how GOOD they are, never how many, so a poor floor is three sacks of
    /// flour rather than an empty room.
    /// </summary>
    Tier PickTier(float budget)
    {
        var affordable = new List<int>();
        int cheapest = 0;

        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].minValue <= budget) affordable.Add(i);
            if (tiers[i].minValue < tiers[cheapest].minValue) cheapest = i;
        }

        return affordable.Count > 0
            ? tiers[affordable[Random.Range(0, affordable.Count)]]
            : tiers[cheapest];
    }

    void SpawnItem(Tier t, int value, Transform level, int slot)
    {
        float mass = Random.Range(t.minMass, t.maxMass);

        GameObject go = t.prefab != null ? Instantiate(t.prefab) : null;
        bool fromPrefab = go != null;

        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = Vector3.one * t.size;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", t.colour);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        go.name = $"{t.names[Random.Range(0, t.names.Length)]}_{t.label}";
        go.transform.SetParent(transform, true);

        // LootPrefabBuilder authors every prefab standing ON its own origin,
        // so its pivot is the BASE. A CreatePrimitive cube's pivot is its
        // CENTRE. The room floor's top surface is local y = 0, so a prefab
        // sits at ~0 and a cube has to be lifted by half its height.
        float y = fromPrefab ? 0.05f : t.size * 0.5f + 0.05f;

        Vector2 s = Slots[slot];
        Vector3 local = new Vector3(
            s.x + Random.Range(-slotJitter, slotJitter),
            y,
            s.y + Random.Range(-slotJitter, slotJitter));

        go.transform.position = level.TransformPoint(local);

        // Random spin about the level's own up, so items look dropped rather
        // than placed - but built on the LEVEL's rotation, so a rotated floor
        // does not tip its loot over.
        go.transform.rotation = level.rotation * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var carryable = go.GetComponent<Carryable>();
        if (carryable == null) carryable = go.AddComponent<Carryable>();
        carryable.value = value;

        int lootLayer = LayerMask.NameToLayer("Loot");
        if (lootLayer >= 0) SetLayerRecursive(go, lootLayer);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
