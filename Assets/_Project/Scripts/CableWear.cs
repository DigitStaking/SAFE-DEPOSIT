// CableWear.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/CableWear.cs
// Goes on: the ELEVATOR root, alongside Elevator, ElevatorDeck and
// ElevatorCable. Added by SAFE DEPOSIT -> Build Elevator Car.
//
// ====================================================================
// PHASE 2 STEP 10 - OVERLOAD KILLS. TEN SECONDS TO FIX IT.
//
// Over capacity the lift will not move AND a ten-second countdown starts.
// Get the load back under 550kg and the alarm stops. Do not, and the cable
// parts: everyone aboard is Lost and the run is over.
//
// ====================================================================
// WHY THIS REPLACED THE SLOW-FRAY VERSION
//
// The first build of this step let an overloaded car depart and billed it
// slowly in rope - a few percent of wear per trip, snapping after five or ten
// greedy hauls. It worked, and it was the wrong shape.
//
// Deferred wear is a thing ONE person notices, three trips later, reading a
// rope on their own. A ten-second alarm with the doors shut is FOUR PEOPLE
// looking at a pile of loot and having to say out loud which crate goes back.
// That argument is the game; the elevator is where it belongs; and a
// mechanic that produces it beats a more elegant one that does not.
//
// It also makes ELEVATOR_SPEC line 141 - "it will not move while overloaded"
// - literally true again, rather than something reinterpreted to make room
// for a trap. The spec did contradict itself and still does not, but the
// resolution now favours the simpler sentence.
//
// ====================================================================
// THE CLOCK RUNS WHEREVER THE CAR IS
//
// Parked at the surface, sitting on a floor, mid-descent - the car hangs off
// that cable the whole time, so the strain does not care. This is deliberate
// and it is the reason loading is tense: you are not safe to stack crates
// past the line just because you have not pressed GO yet.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(Elevator))]
public class CableWear : MonoBehaviour
{
    [Tooltip("Seconds of overload before the cable parts. Long enough to get " +
             "two hands on the nearest crate, short enough that nobody " +
             "finishes the sentence they started.")]
    public float grace = Campaign.OverloadGrace;

    /// <summary>Seconds left before it parts. Full while the load is legal.</summary>
    public float TimeLeft { get; private set; }

    /// <summary>0 fine, 1 parting. Drives the rope's appearance.</summary>
    public float Strain => grace > 0f ? 1f - Mathf.Clamp01(TimeLeft / grace) : 0f;

    public bool Snapped { get; private set; }
    public bool Straining => !Snapped && TimeLeft < grace;

    Elevator lift;
    ElevatorDeck deck;
    ElevatorCable cable;
    RunManager run;

    void Awake()
    {
        lift = GetComponent<Elevator>();
        deck = GetComponent<ElevatorDeck>();
        cable = GetComponent<ElevatorCable>();
        TimeLeft = grace;
    }

    void Start()
    {
        run = SceneRefs.Run;
        Push();
    }

    void Update()
    {
        if (Snapped || deck == null) return;

        if (deck.IsOverloaded)
        {
            TimeLeft -= Time.deltaTime;

            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                Snap();
            }
        }
        else if (TimeLeft < grace)
        {
            // RECOVERS INSTANTLY, NOT GRADUALLY.
            //
            // A slow refill would punish a crew for a mistake they already
            // fixed, and worse, it would make the second overload of a run
            // shorter than the first for reasons nobody can see. The rule has
            // to be one sentence: under the line, you are fine.
            TimeLeft = grace;
        }

        Push();
    }

    void Snap()
    {
        Snapped = true;
        Push();
        if (run != null) run.OnCableSnapped(lift != null ? lift.CurrentFloor : 0);
    }

    void Push()
    {
        Campaign.CableStrain = Strain;
        if (cable != null) cable.SetFray(Strain);
    }

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;
        if (!Straining) return;

        var big = new GUIStyle(GUI.skin.label)
        { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        bool flash = Mathf.FloorToInt(Time.time * 6f) % 2 == 0;
        big.normal.textColor = flash
            ? new Color(1f, 0.2f, 0.15f)
            : new Color(1f, 0.85f, 0.3f);

        // The number is the whole point of this one, unlike the fray version.
        // A countdown somebody can shout - "FOUR" - is what turns a load
        // problem into four people moving at once.
        GUI.Label(new Rect(0f, Screen.height * 0.26f, Screen.width, 40f),
                  $"CABLE OVERLOADED   {Mathf.CeilToInt(TimeLeft)}", big);

        var sub = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        sub.normal.textColor = new Color(1f, 1f, 1f, 0.8f);

        float over = deck != null ? deck.CurrentLoad - deck.Capacity : 0f;
        GUI.Label(new Rect(0f, Screen.height * 0.26f + 42f, Screen.width, 24f),
                  $"get {over:0}kg out of the car", sub);
    }
}
