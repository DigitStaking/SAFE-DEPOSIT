// PriceScanner.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PriceScanner.cs
// Goes on: the Scanner plinth inside the car, built by ElevatorBuilder.
//
// ====================================================================
// ELEVATOR_SPEC STEP 9 - THE PRICE SCANNER.
//
// Hold an item to the station: value, mass, and $/kg. Per the spec, "that
// last number is the one that matters" - it is the difference between a
// crate of beans and a box of antibiotics, and it is the entire skill curve
// of what to carry out.
//
// ====================================================================
// WHY IT READS WHAT YOU ARE HOLDING, NOT WHAT IS SITTING ON IT
//
// "Hold an item to the station" is the spec's own wording, and the
// alternative - put it down on the pad, read the number, pick it back up -
// is three actions to answer one question you are asking mid-argument with
// three other people. Walking up while already carrying something and just
// having the answer appear is one action. It also means the scanner works
// on something in your hands that you have not committed to the car yet,
// which is exactly when the "is this worth the space" decision happens.
//
// ====================================================================
// WHY $/KG IS BANDED AND NOT JUST PRINTED
//
// The raw number alone is not readable at a glance mid-run: is 12 good?
// The tier table in ECONOMY_AND_CAMPAIGN.md answers that - Bulk sits near
// 1, Common near 3, Good near 12, Rare near 100 - so the scanner names the
// band as well as the figure. That turns a number you have to remember a
// table for into a word you can shout across the car.
//
// The bands are DERIVED from that table's own $/kg column, deliberately
// placed between tiers rather than on them, so an item sitting slightly
// off its nominal density still reads as the tier it belongs to.
// ====================================================================

using UnityEngine;

public class PriceScanner : MonoBehaviour
{
    [Header("Range")]
    [Tooltip("How close the player has to be for the scanner to read what " +
             "they are carrying.")]
    public float useRange = 1.8f;

    [Header("Readout")]
    [Tooltip("Metres above the pad the floating readout sits.")]
    public float textHeight = 0.55f;

    public float textSize = 0.0035f;

    PlayerCarry carry;
    Transform player;
    TextMesh readout;

    void Start()
    {
        // Single-player lookup, same caveat as every other script that does
        // this: Phase C replaces it with a player registry.
        var motor = PlayerRegistry.Local;
        if (motor != null)
        {
            player = motor.transform;
            carry = motor.GetComponent<PlayerCarry>();
        }

        BuildReadout();
    }

    /// <summary>
    /// Built here rather than in ElevatorBuilder because it is pure runtime
    /// feedback - there is nothing to position by hand and nothing to see in
    /// the editor. The plinth and pad it sits above ARE builder geometry.
    /// </summary>
    void BuildReadout()
    {
        var go = new GameObject("ScannerReadout");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, textHeight, 0f);

        // Faces +Z like every other TextMesh in this project - see
        // ElevatorBuilder.Label for the note about which way TextMesh
        // actually builds its quads.
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        readout = go.AddComponent<TextMesh>();
        readout.fontSize = 256;              // resolution, per ElevatorBuilder.FontRes
        readout.characterSize = textSize;    // physical size
        readout.anchor = TextAnchor.MiddleCenter;
        readout.alignment = TextAlignment.Center;
        readout.richText = false;
        readout.fontStyle = FontStyle.Bold;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            readout.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }

    void Update()
    {
        if (readout == null) return;

        Carryable item = InRange() && carry != null ? carry.Held : null;

        if (item == null)
        {
            readout.text = "";
            return;
        }

        float perKg = item.Mass > 0.01f ? item.value / item.Mass : 0f;
        (string band, Color colour) = Band(perKg);

        readout.text = $"{item.name}\n${item.value}   {item.Mass:0.#}kg\n${perKg:0.#}/kg   {band}";
        readout.color = colour;
    }

    bool InRange()
    {
        if (player == null) return false;
        return Vector3.Distance(player.position, transform.position) <= useRange;
    }

    /// <summary>
    /// Bands straight off the $/kg column of ECONOMY_AND_CAMPAIGN.md's tier
    /// table: Bulk ~1, Common ~3, Good ~12, Rare ~100. Thresholds sit
    /// BETWEEN those nominal values, not on them, so an item priced slightly
    /// off its tier still reads as the tier it belongs to.
    /// </summary>
    static (string, Color) Band(float perKg)
    {
        if (perKg >= 40f) return ("RARE",   new Color(1f, 0.85f, 0.25f));
        if (perKg >= 7f)  return ("GOOD",   new Color(0.45f, 0.95f, 0.5f));
        if (perKg >= 2f)  return ("COMMON", new Color(0.75f, 0.85f, 0.9f));
        return ("BULK", new Color(0.85f, 0.5f, 0.35f));
    }
}
