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
    // CAPACITY AND PLAYER MASS BOTH COME FROM Campaign NOW.
    //
    // They used to be serialized fields on this component, set to 550 and 70
    // by ElevatorBuilder. That was fine while capacity was a constant and
    // wrong the moment it stopped being one: Phase 2 Step 1 lets the crew BUY
    // capacity, and a number baked into a prefab cannot grow. Worse, a
    // serialized copy would have silently overridden every purchase - the
    // shop would take the money and the gauge would not move.
    //
    // Reading Campaign directly means there is one capacity in the game and
    // the gauge cannot disagree with what was paid for.

    public float CurrentLoad { get; private set; }

    public float Capacity => Campaign.Capacity;
    public bool IsOverloaded => CurrentLoad > Capacity;

    // ------------------------------------------------------------------
    // OVERLOADED NOW MEANS "THIS COSTS YOU", NOT "THIS IS REFUSED".
    //
    // ELEVATOR_SPEC contradicts itself and has since Phase 1:
    //
    //   line  67  "The cable can FRAY UNDER OVERLOAD - your best trap survives"
    //   line 141  "It WILL NOT MOVE while overloaded. Alarm, red gauge,
    //              nothing happens."
    //
    // Both cannot be true. If the car never moves overloaded, the cable can
    // never fray under overload, and Step 10 is dead code guarding a state
    // the game refuses to enter. PHASE2_SPEC sides with the fray, and so does
    // the design sentence the whole step exists for: "the only place in the
    // demo where greed kills you directly rather than by running out of
    // time." Greed that is simply forbidden cannot kill anyone.
    //
    // Resolved so both sentences keep a job:
    //
    //   at or under capacity      safe
    //   over capacity             IT MOVES, and it frays - the cost is
    //                             deferred and paid in cable
    //   over WinchCeiling x cap   refused outright. This is the load at
    //                             which "nothing happens" is still true, and
    //                             it is a MOTOR limit rather than a safety
    //                             rule - the drum cannot lift it, so no
    //                             amount of shouting changes the answer.
    // ------------------------------------------------------------------

    /// <summary>1.0 exactly at capacity, 1.5 at fifty percent over.</summary>
    public float LoadRatio => Capacity > 0f ? CurrentLoad / Capacity : 0f;

    /// <summary>Past what the winch can physically lift. Departure refused.</summary>
    public bool IsUnliftable => LoadRatio > Campaign.WinchCeiling;

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
                    // A DOWNED CREWMATE IS COUNTED ONCE, AND THIS `continue`
                    // IS WHY (PHASE2_SPEC Step 6).
                    //
                    // From Step 6 a downed player carries a Carryable of their
                    // own, also 70kg, so both branches below would match them.
                    // Two cases, both already correct:
                    //
                    //   lying loose in the car - a live Rigidbody, so a rider.
                    //     Charged PlayerMass here and the `continue` skips the
                    //     cargo branch. 70 once, not 140.
                    //
                    //   in someone's arms - Carryable.PickUp made them
                    //     kinematic, and GatherRiders skips kinematic bodies,
                    //     so they are not a rider at all. Their carrier is,
                    //     and pays PlayerMass + CarriedMass = 140 for the two
                    //     of them.
                    //
                    // Anyone tempted to "simplify" these two branches into one
                    // should read that twice first.
                    total += Campaign.PlayerMass;
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

        loadText.text = $"{CurrentLoad:0}/{Capacity:0}";

        // A hard blink over capacity, matching the same alarm language
        // ElevatorBridge already uses for its own warning state - one
        // vocabulary for "something here needs your attention right now."
        bool flash = Mathf.FloorToInt(Time.time * 4f) % 2 == 0;
        float frac = Capacity > 0f ? CurrentLoad / Capacity : 0f;

        loadText.color = IsOverloaded
            ? (flash ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.6f, 0.5f))
            : frac > 0.85f
                ? new Color(1f, 0.7f, 0.2f)     // amber - getting close
                : new Color(0.4f, 0.9f, 0.55f); // green - plenty of room
    }
}
