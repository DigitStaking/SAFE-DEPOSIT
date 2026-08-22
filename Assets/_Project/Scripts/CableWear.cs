// CableWear.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/CableWear.cs
// Goes on: the ELEVATOR root, alongside Elevator and ElevatorCable. Added by
// SAFE DEPOSIT -> Build Elevator Car.
//
// ====================================================================
// PHASE 2 STEP 10 - CABLE FRAY.
//
// "Done when: you look up at the cable before pressing GO."
//
// PHASE2_SPEC: "This is the only place in the demo where greed kills you
// directly rather than by running out of time." Everything else that can end
// a run is a clock - the collapse, the bleed-out. This one is a choice you
// made three trips ago.
//
// ====================================================================
// WHY OVERLOADING HAD TO STOP BEING FORBIDDEN
//
// ELEVATOR_SPEC contradicts itself, and has since Phase 1:
//
//   line  67   "The cable can FRAY UNDER OVERLOAD - your best trap survives"
//   line 141   "It WILL NOT MOVE while overloaded."
//
// A car that never moves overloaded can never fray under overload. Kept as
// written, this entire file is dead code guarding a state the game refuses to
// enter, and "greed kills you directly" describes a greed the lift will not
// permit.
//
// So overload now DEPARTS and bills you in cable, and the refusal moved to a
// load the winch physically cannot lift (Campaign.WinchCeiling). See the long
// note on ElevatorDeck.IsUnliftable. Both of the spec's sentences still have a
// job; they just describe different loads.
//
// ====================================================================
// WEAR IS PER METRE, NOT PER TRIP
//
// A trip is not a unit of anything. One floor and twelve floors are the same
// number of button presses and wildly different amounts of rope over the
// drum, and a per-trip cost would make twelve short hops safer than one long
// haul - which is exactly backwards from how you would actually break a
// cable, and exactly the strategy nobody should be rewarded for finding.
//
// Distance is measured from the car's own movement rather than from floor
// numbers, so a trip interrupted halfway still charges for the half.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(Elevator))]
public class CableWear : MonoBehaviour
{
    [Tooltip("Fray above this and the cable is visibly going. The warning " +
             "band exists so the snap is never the first thing you hear.")]
    [Range(0f, 1f)] public float warnAt = 0.6f;

    public float Fray => Mathf.Clamp01(Campaign.CableFray);
    public bool Frayed => Fray >= warnAt;
    public bool Snapped { get; private set; }

    Elevator lift;
    ElevatorDeck deck;
    ElevatorCable cable;
    RunManager run;

    float lastY;
    bool haveLastY;

    void Awake()
    {
        lift = GetComponent<Elevator>();
        deck = GetComponent<ElevatorDeck>();
        cable = GetComponent<ElevatorCable>();
    }

    void Start()
    {
        run = Object.FindFirstObjectByType<RunManager>();
        PushVisual();
    }

    void FixedUpdate()
    {
        if (lift == null || deck == null) return;

        float y = transform.position.y;

        if (!haveLastY) { lastY = y; haveLastY = true; return; }

        float metres = Mathf.Abs(y - lastY);
        lastY = y;

        if (Snapped || metres <= 0f) return;

        // Only the part of the load ABOVE capacity does damage. At exactly
        // capacity the rope is rated for it and wears nothing, which is what
        // makes the gauge's amber band meaningful rather than decorative.
        float over = deck.LoadRatio - 1f;
        if (over <= 0f) return;

        Campaign.CableFray = Mathf.Clamp01(
            Campaign.CableFray + over * metres * Campaign.FrayPerMetrePerOverload);

        PushVisual();

        if (Campaign.CableFray >= 1f) Snap();
    }

    /// <summary>
    /// It parts. Everyone aboard is Lost and the run is over - not Buried,
    /// because they are in the pit rather than under a slab, and ECONOMY's
    /// distinction between the two is what the rescue contract is priced on.
    /// </summary>
    void Snap()
    {
        Snapped = true;
        Campaign.CableFray = 1f;
        PushVisual();

        if (run != null) run.OnCableSnapped(lift != null ? lift.CurrentFloor : 0);
    }

    void PushVisual()
    {
        if (cable != null) cable.SetFray(Fray);
    }

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;
        if (Fray < warnAt) return;

        // Deliberately NOT a permanent gauge. The done-when for this step is
        // "you look UP at the cable before pressing GO" - a number on the HUD
        // would satisfy the mechanic and kill the moment it exists for. This
        // only speaks once the rope is already visibly wrong, and it points
        // at the rope rather than replacing it.
        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        bool critical = Fray >= 0.85f;
        style.normal.textColor = critical && Mathf.FloorToInt(Time.time * 4f) % 2 == 0
            ? new Color(1f, 0.9f, 0.85f)
            : new Color(1f, 0.35f, 0.2f);

        GUI.Label(new Rect(0f, Screen.height * 0.08f, Screen.width, 24f),
                  critical ? "THE CABLE IS ABOUT TO GO" : "LOOK UP", style);
    }
}
