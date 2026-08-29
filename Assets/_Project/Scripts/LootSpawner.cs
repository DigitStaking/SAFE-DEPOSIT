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
// STOCKED ONCE PER CAMPAIGN, THEN THE BUILDING REMEMBERS.
//
// Loot is generated on the FIRST load of a campaign and never again. After
// that it is restored from Campaign.LootRoster, which survives the scene
// reload between rounds: every item comes back with the same tier, the same
// price, the same weight, and the exact position and rotation it was left
// in. Take three crates off floor 4 and floor 4 has three fewer crates for
// the rest of the campaign. Shove a pallet into a corner and it is still in
// that corner next round.
//
// This is not a convenience, it is what makes the demolition a LOSS. A
// floor that refills is a floor you never really lost, and ECONOMY assumes
// the opposite - "fall behind on upgrades and you start leaving loot on the
// floor of a building that's being demolished" only bites if what you left
// behind is gone for good.
//
// It also means the ONLY reason this is runtime rather than part of
// GrayboxBuilder is the first roll. Everything after it is replay.
// ====================================================================

using System.Collections;
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

    // ------------------------------------------------------------------
    // THE AUDIT
    //
    // Loot keeps ending up on the elevator roof. Three fixes have been tried
    // by reasoning about local space and all three missed, which is what
    // ROADMAP's KNOWN ISSUES already says to stop doing: log the WORLD
    // position and compare it against the room's real bounds.
    //
    // The decisive question is not "where does it spawn" - that arithmetic
    // has been checked twice - but "does it STAY there". So every item is
    // recorded at spawn and re-checked once the physics has settled. The two
    // numbers separate the only two possible causes:
    //
    //   spawn wrong  -> the placement maths is wrong after all
    //   spawn right, settle wrong -> something MOVES it, and the log says
    //                               how far and in which direction
    // ------------------------------------------------------------------

    [Header("Diagnostics")]
    [Tooltip("Log every item's world position at spawn and again once " +
             "physics has settled. Turn off when the roof bug is dead.")]
    public bool auditPlacement = true;

    public float auditDelay = 4f;

    [Tooltip("Report an item that has moved further than this from where it " +
             "was placed.")]
    public float auditMoveTolerance = 1.5f;

    class Placed
    {
        public Transform t;
        public Vector3 spawn;
        public string floor;
        public int slot;
    }

    readonly List<Placed> placed = new List<Placed>();

    void Start()
    {
        // THE BUILDING IS STOCKED ONCE, AT THE START OF A CAMPAIGN.
        //
        // Everything after that is restored from Campaign.LootRoster, which
        // survives the scene reload between rounds. Take three crates off
        // floor 4 and floor 4 has three fewer crates forever; shove a pallet
        // into a corner and it is still in that corner next round.
        //
        // Respawning was making the demolition meaningless. A floor that
        // refills is a floor you never really lost, and ECONOMY assumes the
        // opposite - "you start leaving loot on the floor of a building
        // that's being demolished" only bites if the loot left behind is
        // gone for good.
        if (Campaign.LootSeeded)
        {
            RestoreRoster();
            return;
        }

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

        Campaign.LootSeeded = true;
        CaptureRemaining(null);      // the opening state of the building

        if (auditPlacement) StartCoroutine(Audit());
    }

    // ------------------------------------------------------------------
    // RESTORE
    // ------------------------------------------------------------------

    /// <summary>
    /// PHASE 4 STEP 6. Demolish my building and put up the host's.
    ///
    /// A joining client has already stocked a whole building of its own -
    /// LootSpawner.Start ran before anything connected, and it used
    /// Random.Range with no shared seed, so not one crate of it matches
    /// anybody else's. That is what "each one have 3 items that he is the only
    /// one can see them" was.
    ///
    /// So this is a REPLACEMENT, not a merge. Everything currently in the
    /// world goes, including anything already in somebody's hands - which is
    /// safe only because this runs during the join, before anyone has had a
    /// chance to pick anything up.
    /// </summary>
    public void ClearAndRebuild()
    {
        int removed = 0;
        foreach (var item in FindObjectsByType<LootItem>(FindObjectsSortMode.None))
        {
            if (item == null) continue;
            Destroy(item.gameObject);
            removed++;
        }

        RestoreRoster();

        Debug.Log($"[Loot] cleared {removed} of my own items and rebuilt " +
                  $"{Campaign.LootRoster.Count} from the host.");
    }

    void RestoreRoster()
    {
        // Stamped with its place in the roster as it is built. Every machine
        // walks this same list in this same order, so index 17 is the same
        // crate everywhere - which is what lets a pickup be sent as a number.
        for (int i = 0; i < Campaign.LootRoster.Count; i++)
        {
            var r = Campaign.LootRoster[i];
            var go = BuildItem(r.tier, r.value, r.mass, r.name, r.position, r.rotation);

            var item = go != null ? go.GetComponent<LootItem>() : null;
            if (item != null) item.SetRosterIndex(i);
        }

        Debug.Log($"[Loot] round {Campaign.RunNumber}: restored " +
                  $"{Campaign.LootRoster.Count} items exactly where the last " +
                  "crew left them. Nothing respawned.");

        if (auditPlacement) StartCoroutine(Audit());
    }

    // ------------------------------------------------------------------
    // CAPTURE
    //
    // Called by RunManager the moment a run is banked. Rebuilt from the LIVE
    // objects rather than edited in place, which gets two things for free:
    // loot destroyed by a room seal is simply not found, and loot the crew
    // moved is recorded wherever it actually ended up.
    //
    // 'sold' is the exact set RunManager just paid out for - held, stowed,
    // or loose inside the car. Passing the same set that produced the money
    // is what stops the two ever disagreeing about whether a crate came home.
    // ------------------------------------------------------------------

    public static void CaptureRemaining(HashSet<Carryable> sold)
    {
        Campaign.LootRoster.Clear();

        foreach (var item in FindObjectsByType<LootItem>(FindObjectsSortMode.None))
        {
            if (item == null) continue;

            var c = item.GetComponent<Carryable>();
            if (sold != null && c != null && sold.Contains(c)) continue;

            // STAMPED AS IT IS CAPTURED, so the live item and its roster
            // entry carry the same number.
            //
            // Without this the host's own first-seed items never got an index
            // - only RestoreRoster stamps, and the host that STOCKS a building
            // never restores it - so the host could see a crate, pick it up,
            // and be unable to say which crate it was. Clients would have
            // watched it float away in nobody's hands.
            item.SetRosterIndex(Campaign.LootRoster.Count);

            Campaign.LootRoster.Add(new Campaign.LootRecord {
                tier = item.tier,
                value = c != null ? c.value : item.value,
                mass = item.mass,
                name = item.gameObject.name,
                position = item.transform.position,
                rotation = item.transform.rotation,
            });
        }
    }

    IEnumerator Audit()
    {
        // Report the spawn positions immediately, so the log survives even if
        // something later destroys the items.
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Loot audit] {placed.Count} items placed. " +
                      "Room interior is x 7.5..13.5, z -7..7 in each LEVEL's " +
                      "own space; world x/z depend on that level's rotation.");

        foreach (var p in placed)
        {
            if (p.t == null) continue;
            sb.AppendLine($"  {p.floor} slot {p.slot}  world {p.spawn:F2}  {p.t.name}");
        }
        Debug.Log(sb.ToString());

        yield return new WaitForSeconds(auditDelay);

        var moved = new System.Text.StringBuilder();
        int n = 0, gone = 0;

        // WHERE they end up is the diagnosis, so classify it rather than
        // making someone read sixty coordinates:
        //   above ground   - something lifted them; the elevator is the only
        //                    thing in this game that lifts
        //   inside the shaft - they left the room sideways and fell
        int above = 0, inShaft = 0, elsewhere = 0;

        foreach (var p in placed)
        {
            if (p.t == null) { gone++; continue; }

            float d = Vector3.Distance(p.spawn, p.t.position);
            if (d <= auditMoveTolerance) continue;

            Vector3 now = p.t.position;
            float axis = new Vector2(now.x, now.z).magnitude;
            if (now.y > 0f) above++;
            else if (axis < 7f) inShaft++;
            else elsewhere++;

            n++;
            Vector3 delta = p.t.position - p.spawn;
            moved.AppendLine($"  {p.floor} slot {p.slot}  {p.t.name}");
            moved.AppendLine($"      spawned {p.spawn:F2}");
            moved.AppendLine($"      now     {p.t.position:F2}");
            moved.AppendLine($"      moved   {d:F2}m   delta {delta:F2}   " +
                             $"parent now '{(p.t.parent != null ? p.t.parent.name : "none")}'");
        }

        if (n == 0 && gone == 0)
        {
            Debug.Log($"[Loot audit] after {auditDelay}s: nothing moved more " +
                      $"than {auditMoveTolerance}m. Placement is not the bug.");
        }
        else
        {
            // LogError, not LogWarning. This is the one line that decides
            // which bug this is, and a warning is easy to scroll past.
            var report = new System.Text.StringBuilder();
            report.AppendLine($"[Loot audit] after {auditDelay}s: " +
                              $"{n} of {placed.Count} moved, {gone} destroyed. " +
                              $"Landed: {above} above ground (y>0), " +
                              $"{inShaft} inside the shaft (within 7m of the " +
                              $"axis), {elsewhere} other.");
            report.Append(moved);
            Debug.LogError(report.ToString());
        }
    }

    int FillFloor(Transform level, float budget)
    {
        int spawned = 0;
        int slots = Mathf.Min(itemsPerFloor, Slots.Length);

        for (int slot = 0; slot < slots; slot++)
        {
            int tierIndex = PickTier(budget);
            Tier t = tiers[tierIndex];
            int value = Random.Range(t.minValue, t.maxValue + 1);

            SpawnItem(tierIndex, value, level, slot);
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
    int PickTier(float budget)
    {
        var affordable = new List<int>();
        int cheapest = 0;

        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].minValue <= budget) affordable.Add(i);
            if (tiers[i].minValue < tiers[cheapest].minValue) cheapest = i;
        }

        return affordable.Count > 0
            ? affordable[Random.Range(0, affordable.Count)]
            : cheapest;
    }

    void SpawnItem(int tierIndex, int value, Transform level, int slot)
    {
        Tier t = tiers[tierIndex];
        float mass = Random.Range(t.minMass, t.maxMass);
        string name = $"{t.names[Random.Range(0, t.names.Length)]}_{t.label}";

        // A prefab's pivot is its BASE (LootPrefabBuilder authors them
        // standing on their own origin); a fallback cube's pivot is its
        // CENTRE. The room floor's top surface is local y = 0.
        float y = t.prefab != null ? 0.05f : t.size * 0.5f + 0.05f;

        Vector2 sl = Slots[slot];
        Vector3 local = new Vector3(
            sl.x + Random.Range(-slotJitter, slotJitter),
            y,
            sl.y + Random.Range(-slotJitter, slotJitter));

        // Random spin about the level's own up, so items look dropped rather
        // than placed - but built on the LEVEL's rotation, so a rotated floor
        // does not tip its loot over.
        Vector3 world = level.TransformPoint(local);
        Quaternion spin = level.rotation * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        BuildItem(tierIndex, value, mass, name, world, spin);
    }

    /// <summary>
    /// Makes one item at an exact pose. The ONLY place loot is constructed,
    /// so a restored crate is identical to a freshly rolled one - the first
    /// spawn just decides the numbers, and every round afterwards replays
    /// them.
    /// </summary>
    GameObject BuildItem(int tierIndex, int value, float mass, string name,
                         Vector3 world, Quaternion spin)
    {
        Tier t = tiers[Mathf.Clamp(tierIndex, 0, tiers.Length - 1)];

        GameObject go = t.prefab != null ? Instantiate(t.prefab) : null;

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

        go.name = name;
        go.transform.SetParent(transform, true);

        // LootPrefabBuilder authors every prefab standing ON its own origin,
        // so its pivot is the BASE. A CreatePrimitive cube's pivot is its
        // ==============================================================
        // MOVE THE RIGIDBODY, NOT THE TRANSFORM. THIS IS THE ROOF BUG.
        //
        // The loot prefabs already carry a Rigidbody, so Instantiate
        // registers a physics body at the PREFAB's authored pose - the
        // origin - before this method touches anything. Writing
        // go.transform.position afterwards moved the TRANSFORM and left
        // the physics body at the origin.
        //
        // On its own that would still have been corrected on the next
        // physics step. What made it permanent was setting
        // RigidbodyInterpolation.Interpolate immediately afterwards:
        // interpolation makes Unity WRITE THE TRANSFORM every frame from
        // the body's own pose history, and that history said origin. So
        // the transform was stomped straight back and every item fell
        // down the shaft from y = 0 - landing on the elevator roof, which
        // is the only wide flat thing on the way down.
        //
        // The audit is unambiguous about it: all 60 items, from all 20
        // floors, ended up at x = 0, z = 0 within centimetres of each
        // other, several having risen ninety-odd metres to get there.
        // Nothing pushes 60 objects onto one axis - but the origin is
        // exactly where they had never really left.
        //
        // rb.position / rb.rotation write the PHYSICS pose and reset the
        // interpolation history, so there is nothing stale to snap back
        // to. Interpolation is enabled afterwards, on a body that is
        // already in the right place.
        // ==============================================================
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        go.transform.SetPositionAndRotation(world, spin);
        rb.position = world;
        rb.rotation = spin;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var carryable = go.GetComponent<Carryable>();
        if (carryable == null) carryable = go.AddComponent<Carryable>();
        carryable.value = value;

        // The tag that survives the campaign. Tier and mass are what a
        // Carryable cannot tell you, and both are needed to rebuild this
        // exact item after the scene is destroyed.
        var tag = go.GetComponent<LootItem>();
        if (tag == null) tag = go.AddComponent<LootItem>();
        tag.tier = tierIndex;
        tag.value = value;
        tag.mass = mass;

        int lootLayer = LayerMask.NameToLayer("Loot");
        if (lootLayer >= 0) SetLayerRecursive(go, lootLayer);

        if (auditPlacement)
            placed.Add(new Placed {
                t = go.transform,
                spawn = go.transform.position,
                floor = go.name,
                slot = 0,
            });

        return go;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
