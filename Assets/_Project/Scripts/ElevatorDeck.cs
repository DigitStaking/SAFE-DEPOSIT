// ElevatorDeck.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ElevatorDeck.cs
// Goes on: the ELEVATOR root, alongside Elevator.cs and ElevatorBridge.cs.
//
// ====================================================================
// ELEVATOR_SPEC STEP 8 - CARGO AND LOAD.
//
// Load = 70kg per player currently inside the car, plus the mass of every
// item deliberately placed on the deck (Carryable.PlaceOnDeck, called from
// PlayerCarry when you press E near DeckAnchor). Overloading blocks
// departure: ElevatorDashboard checks IsOverloaded before it will even
// start a Bridge request.
//
// ====================================================================
// WHY "ON DECK" IS A DELIBERATE ACTION, NOT AMBIENT POSITION
//
// The obvious alternative was: anything physically inside the car counts,
// full stop - checked the same way Elevator already checks who is riding
// it. That is wrong for the reason ElevatorBuilder already called out when
// the deck markings themselves were built, back in Step 3: "so that nobody
// argues about whether the crate they dumped in a doorway counts." If mere
// presence counts, four players carrying a heavy statue around a 4x4m room
// have to agree in real time on where the car's boundary even is. Placing
// it - E, near the marked square - is a clear, deliberate, UNDOABLE action.
// You always know whether something counts, because you are the one who
// put it there, and you can take it back.
//
// This falls out almost for free, mechanically: OnDeck cargo is kinematic
// and PARENTED under DeckAnchor (see Carryable.PlaceOnDeck), so it rides
// the car through ordinary transform hierarchy - it needs no help from
// Elevator's rider-teleport system. This script just enumerates
// DeckAnchor's own children every frame rather than running a second
// physics query for the same information.
//
// Crew mass is different: a PLAYER cannot be "placed", so that half of the
// total is read from Elevator.Riders - the same overlap query FixedUpdate
// already runs every physics step to carry people when the car moves, now
// also read here for the far cheaper question of who is simply present.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(Elevator))]
public class ElevatorDeck : MonoBehaviour
{
    [Header("Capacity - ECONOMY_AND_CAMPAIGN.md BASE_CAPACITY")]
    [Tooltip("550 base. Step 12 wires this to Campaign's upgraded capacity; " +
             "for now it is the flat starting number and nothing else changes.")]
    public float capacity = 550f;

    [Tooltip("PLAYER_MASS from the economy doc - charged per player currently " +
             "inside the car, regardless of what they are personally carrying.")]
    public float playerMass = 70f;

    public float CurrentLoad { get; private set; }
    public bool IsOverloaded => CurrentLoad > capacity;

    /// <summary>Where PlayerCarry parents anything placed as cargo.</summary>
    public Transform DeckAnchor { get; private set; }

    Elevator elevator;
    TextMesh loadText;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake() => elevator = GetComponent<Elevator>();

    void Start()
    {
        DeckAnchor = transform.Find("Car/Deck/DeckAnchor");
        if (DeckAnchor == null)
            Debug.LogWarning("[Deck] No DeckAnchor - run Build Elevator Car.");

        var t = transform.Find("Car/Dashboard/Face/LoadText");
        if (t != null) loadText = t.GetComponent<TextMesh>();
    }

    void Update()
    {
        RecomputeLoad();
        UpdateReadout();
    }

    void RecomputeLoad()
    {
        float total = 0f;

        if (elevator != null)
            foreach (var rb in elevator.Riders)
                if (rb != null && rb.GetComponent<PlayerMotor>() != null)
                    total += playerMass;

        // Survivors are not built yet. Their mass adds here the same way
        // once they exist - they are cargo that happens to walk itself on.
        if (DeckAnchor != null)
            foreach (var c in DeckAnchor.GetComponentsInChildren<Carryable>())
                if (c != null) total += c.Mass;

        CurrentLoad = total;
    }

    void UpdateReadout()
    {
        if (loadText == null) return;

        loadText.text = $"{CurrentLoad:0}/{capacity:0}";

        // A hard blink over capacity, matching the same alarm language
        // ElevatorBridge already uses for its own warning state - one
        // vocabulary for "something here needs your attention right now."
        bool flash = Mathf.FloorToInt(Time.time * 4f) % 2 == 0;
        float frac = capacity > 0f ? CurrentLoad / capacity : 0f;

        loadText.color = IsOverloaded
            ? (flash ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.6f, 0.5f))
            : frac > 0.85f
                ? new Color(1f, 0.7f, 0.2f)     // amber - getting close
                : new Color(0.4f, 0.9f, 0.55f); // green - plenty of room
    }
}
