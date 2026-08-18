// ElevatorDeck.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ElevatorDeck.cs
// Goes on: the ELEVATOR root, alongside Elevator.cs and ElevatorBridge.cs.
//
// ====================================================================
// ELEVATOR_SPEC STEP 8 - CARGO AND LOAD.
//
// Load = 70kg per player currently inside the car, plus whatever they are
// personally holding or wearing, plus the mass of anything else lying loose
// anywhere inside the car - the marked deck square is a visual suggestion
// for where to pile things, not a mechanical requirement. Overloading blocks
// departure: ElevatorDashboard checks IsOverloaded before it will even start
// a Bridge request.
//
// ====================================================================
// REVISED AFTER PLAYTEST: DROPPED THE "DELIBERATE PLACEMENT" RULE.
//
// The first version of this file required carrying an item to the marked
// deck square specifically and pressing E there, on the reasoning that
// ambient position would make four players argue about where the car's
// boundary was. In practice the opposite happened - a hard placement
// requirement made the crew argue about ALIGNMENT instead, and "just let me
// drop it, it is obviously inside the elevator" is a completely reasonable
// thing to want.
//
// So: anything WHOSE RIGIDBODY Elevator's own overlap query finds inside the
// car counts, full stop - the exact same query FixedUpdate already runs
// every physics step to carry riders when the car moves (see
// Elevator.Riders). No new physics query, no deck-anchor bookkeeping, no
// separate CarryState. A dropped Carryable is State.Free, non-kinematic,
// and therefore already exactly what that overlap box was built to find.
//
// The one thing that query CANNOT see is anything a player is personally
// holding or wearing - Held and Stowed items go kinematic specifically so
// they stop colliding with anything, which also makes them invisible to an
// overlap box. So a player's own carried mass (PlayerCarry.CarriedMass) is
// added on top, once per player found riding the car.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(Elevator))]
public class ElevatorDeck : MonoBehaviour
{
    [Header("Capacity - ECONOMY_AND_CAMPAIGN.md BASE_CAPACITY")]
    [Tooltip("550 base. Step 12 wires this to Campaign's upgraded capacity; " +
             "for now it is the flat starting number and nothing else changes.")]
    public float capacity = 550f;

    [Tooltip("PLAYER_MASS from the economy doc - the player's own body, " +
             "separate from whatever they are personally carrying.")]
    public float playerMass = 70f;

    public float CurrentLoad { get; private set; }
    public bool IsOverloaded => CurrentLoad > capacity;

    Elevator elevator;
    TextMesh loadText;

    void Awake() => elevator = GetComponent<Elevator>();

    void Start()
    {
        var t = transform.Find("Car/Dashboard/Face/LoadText");
        if (t != null) loadText = t.GetComponent<TextMesh>();
    }

    void Update()
    {
        RecomputeLoad();
        UpdateReadout();
    }

    /// <summary>
    /// One pass over Elevator.Riders. Each entry is either a player's own
    /// Rigidbody (charge playerMass plus whatever PlayerCarry says they are
    /// holding or wearing) or a loose Carryable's Rigidbody (charge its own
    /// mass) - never both, since a player and their held item are two
    /// separate GameObjects. Survivors are not built yet; their mass adds
    /// here the same way once they exist - they are cargo that happens to
    /// walk itself on.
    /// </summary>
    void RecomputeLoad()
    {
        float total = 0f;

        if (elevator != null)
        {
            foreach (var rb in elevator.Riders)
            {
                if (rb == null) continue;

                var motor = rb.GetComponent<PlayerMotor>();
                if (motor != null)
                {
                    total += playerMass;
                    var carry = rb.GetComponent<PlayerCarry>();
                    if (carry != null) total += carry.CarriedMass;
                    continue;
                }

                var cargo = rb.GetComponent<Carryable>();
                if (cargo != null) total += cargo.Mass;
            }
        }

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
