// LootSpawner.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LootSpawner.cs
// Goes on: the LOOT root, added by GrayboxBuilder.
//
// ====================================================================
// THE BUDGET SPAWNER  (ECONOMY_AND_CAMPAIGN.md Part 4b)
//
// Each floor gets a VALUE budget in points. Items are drawn at random and
// their price deducted until the budget runs out. That single rule produces
// every situation the design wants without a special case for any of them:
//
//   a floor that rolls two Bulk-heavies - $300 in 200kg, take one, come
//   back for the other
//   a floor that rolls a Rare - $300 in 3kg, take everything, laugh
//   a floor of all Bulk - heavy, cheap, genuinely not worth the space
//
// ====================================================================
// WHY THE BUDGET IS IN VALUE AND NEVER IN MASS
//
// The doc is explicit and it is the whole trick: "Because mass is the thing
// you want to vary. Budget the money, let the kilos fall where they may,
// and the variance you asked for appears on its own."
//
// Budget by mass instead and every floor is worth the same amount for the
// same weight, which is the one outcome that makes the whole $/kg skill
// curve meaningless.
//
// ====================================================================
// WHY THIS IS RUNTIME AND NOT PART OF GrayboxBuilder
//
// The budget depends on the ROUND - SpawnValue(R) = LootValue(R) x 1.4 -
// and RunManager reloads the scene between rounds. Editor-time loot would
// be identical every round forever, which makes the economy impossible to
// test across a campaign. Spawning in Start() means every reload re-rolls
// against the current round's budget, which is exactly the intent.
//
// It also gets "a floor you strip stays stripped" for free: sealed rooms
// are skipped, and Campaign remembers which those are.
// ====================================================================

using System.Collections.Generic;
using UnityEngine;

public class LootSpawner : MonoBehaviour
{
    // ------------------------------------------------------------------
    // THE FIVE TIERS, straight off ECONOMY_AND_CAMPAIGN.md Part 3.
    //
    // Food and medicine, not office furniture. The doc's reasoning, which
    // is worth keeping in front of whoever tunes these next: "A crate of
    // beans is heavy and nearly worthless. A box of antibiotics is the size
    // of a book and worth six crates. Learning to read a room and take the
    // DENSE things is the mastery."
    //
    // It is also what makes the moral line land: "the medicine you're
    // selling to the mafia is medicine somebody in this building needs."
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

    [Header("Budget")]
    [Tooltip("SpawnValue(R) = LootValue(R) x this. 1.4 means roughly 40% more " +
             "value on the floor than the cable can lift - ECONOMY Part 4b. " +
             "You can never clear a round, and what is left is always the " +
             "heavy awkward thing nobody wanted to carry.")]
    public float spawnMultiplier = 1.4f;

    [Tooltip("Per-floor budget varies by this either way, so two floors worth " +
             "the same money can be completely different problems.")]
    public float floorVariance = 0.2f;

    [Header("Placement")]
    public float roomMargin = 1.2f;

    [Tooltip("Clear space between two items, on top of their own half-widths. " +
             "Stops loot spawning inside its neighbour and being shoved on top " +
             "of it by the physics solver.")]
    public float itemSpacing = 0.5f;

    readonly List<(Vector3 pos, float size)> placed = new List<(Vector3, float)>();

    void Start()
    {
        var shaft = GameObject.Find("SHAFT");
        if (shaft == null)
        {
            Debug.LogWarning("[Loot] No SHAFT - run Build Graybox Shaft.");
            return;
        }

        // Only floors the cable can actually reach. Spawning loot into rooms
        // nobody can visit would inflate what "is on the floor" means and
        // quietly break the budget maths for the floors that count.
        var open = new List<Transform>();
        for (int floor = 1; floor <= Campaign.DeepestReachableFloor; floor++)
        {
            if (Campaign.DestroyedRooms.Contains(floor)) continue;
            var level = shaft.transform.Find($"Level_{floor:00}");
            if (level != null) open.Add(level);
        }

        if (open.Count == 0) return;

        float spawnValue = Campaign.Income * spawnMultiplier;
        float perFloor = spawnValue / open.Count;

        int total = 0;
        foreach (var level in open)
            total += FillFloor(level, perFloor * Random.Range(1f - floorVariance, 1f + floorVariance));

        Debug.Log($"[Loot] round {Campaign.RunNumber}: ~${spawnValue:0} across " +
                  $"{open.Count} floors, {total} items. Cable lifts ~${Campaign.Income}.");
    }

    /// <summary>
    /// Draw tiers at random, deduct each one's price, stop when the budget
    /// is spent. Deliberately NOT "pick items until the total is closest to
    /// the budget" - a random draw against a running total is what produces
    /// the lopsided floors the design wants.
    /// </summary>
    int FillFloor(Transform level, float budget)
    {
        int spawned = 0;

        // Where this floor's items have already landed, in LEVEL-local space.
        // Without this every position was an independent random draw with no
        // memory: two items rolling near each other spawned interpenetrating,
        // and physics resolved the overlap by shoving one on top of the
        // other. That is the stack-on-a-vending-machine problem - it was
        // never a spread problem, it was a collision one.
        placed.Clear();

        // Hard cap purely as a safety net against a mis-tuned tier table
        // making this loop very long; the budget is the real terminator.
        for (int guard = 0; guard < 40 && budget > 0f; guard++)
        {
            Tier t = tiers[Random.Range(0, tiers.Length)];
            int value = Random.Range(t.minValue, t.maxValue + 1);

            // Affordable, or the first item on an empty floor - a floor that
            // rolled nothing at all would just be a bare room, which reads
            // as a bug rather than as bad luck.
            if (value > budget && spawned > 0) continue;

            if (!SpawnItem(t, value, level)) continue;
            budget -= value;
            spawned++;
        }

        return spawned;
    }

    bool SpawnItem(Tier t, int value, Transform level)
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

        string flavour = t.names[Random.Range(0, t.names.Length)];
        go.name = $"{flavour}_{t.label}";
        go.transform.SetParent(transform, true);

        // Candidate spots are drawn in the LEVEL's local space, so they work
        // whichever of the four directions that floor's doorway faces.
        const float half = 7f;        // GrayboxBuilder.ShaftInner * 0.5
        const float roomDepth = 6f;   // GrayboxBuilder.RoomDepth

        // LootPrefabBuilder authors every prefab standing ON its own origin,
        // so its pivot is the BASE. A CreatePrimitive cube's pivot is its
        // CENTRE.
        float pivotLift = fromPrefab ? 0f : t.size * 0.5f;

        // EVERY SPOT IS VERIFIED BY RAYCAST, not trusted from arithmetic.
        //
        // The previous version computed a position from ShaftInner and
        // RoomDepth and assumed the floor was underneath it. Loot still
        // ended up on the elevator roof, which means the assumption was
        // wrong somewhere - and rather than hunt for which constant drifted,
        // this now just asks the physics engine where the floor actually is.
        //
        // hit.transform.IsChildOf(level) is the important half: it is not
        // enough to hit SOMETHING, it has to be THIS floor's own geometry.
        // A spot over the shaft void hits the elevator roof or a lower
        // level, fails that test, and gets redrawn - which is precisely the
        // bug, caught by construction instead of by arithmetic.
        Vector3 world = Vector3.zero;
        bool found = false;

        for (int attempt = 0; attempt < 30 && !found; attempt++)
        {
            Vector3 local = new Vector3(
                Random.Range(half + roomMargin, half + roomDepth - roomMargin * 0.5f),
                0f,
                Random.Range(-half + roomMargin, half - roomMargin));

            if (!IsClear(local, t.size)) continue;

            // Cast from head height down through where the floor should be.
            Vector3 from = level.TransformPoint(local + Vector3.up * 2.5f);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 6f,
                                 ~0, QueryTriggerInteraction.Ignore)) continue;

            if (!hit.transform.IsChildOf(level)) continue;

            placed.Add((local, t.size));
            world = hit.point + Vector3.up * (pivotLift + 0.03f);
            found = true;
        }

        if (!found)
        {
            // Nowhere on this floor passed. Better to drop the item than to
            // leave it somewhere it does not belong - a missing crate is a
            // smaller lie than one embedded in a wall.
            Destroy(go);
            return false;
        }

        go.transform.position = world;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

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

        return true;
    }

    /// <summary>
    /// True if a box of this size at this spot clears everything already
    /// placed on the floor. Compared on X/Z only - two items at the same
    /// spot but different heights is still a stack, which is the thing being
    /// prevented.
    /// </summary>
    bool IsClear(Vector3 local, float size)
    {
        foreach (var (pos, otherSize) in placed)
        {
            float needed = (size + otherSize) * 0.5f + itemSpacing;
            float dx = local.x - pos.x;
            float dz = local.z - pos.z;
            if (dx * dx + dz * dz < needed * needed) return false;
        }
        return true;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
