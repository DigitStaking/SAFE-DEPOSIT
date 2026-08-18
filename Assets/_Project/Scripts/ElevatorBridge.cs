// ElevatorBridge.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ElevatorBridge.cs
// Goes on: the ELEVATOR root, alongside Elevator.cs and ElevatorCable.cs.
//
// ====================================================================
// ELEVATOR_SPEC STEP 7 - THE BRIDGE.
//
// On arrival: the shutter rolls up, then a steel bridge extends across the
// gap the shaft widening bought (see GrayboxBuilder.ShaftInner) to the
// doorway. Selecting another floor does NOT move the car immediately - it
// starts a 5-second countdown with an alarm, and only once that countdown
// finishes AND nobody is standing on the bridge does it actually retract
// and let the car go.
//
// ====================================================================
// WHY DEPARTURE IS GATED HERE, NOT IN Elevator.cs
//
// Elevator.GoToFloor()'s own doc comment already says as much: "the
// dashboard gets to change its mind in Step 7, once the bridge exists to
// make that decision cost something." Elevator.cs knows nothing about
// bridges. This script intercepts BEFORE the request reaches it, holds it
// for the warning period, and only then hands it through - a one-way
// dependency, Bridge on Elevator and never the reverse, so Elevator.cs did
// not need a single line changed for this step to exist.
//
// ElevatorDashboard.cs now calls RequestGoUp / RequestGoDown /
// RequestGoToFloor on THIS script instead of on Elevator directly - that is
// the one line that changed there.
//
// ====================================================================
// WHY THE COUNTDOWN HAPPENS BEFORE THE RETRACT, NOT DURING IT
//
// The spec says retraction happens "with a 5-second countdown and an
// alarm." Blending the two - a bridge that slowly shrinks over 5 seconds -
// reads as a loading bar, not a threat. Held stationary and CROSSABLE for
// the full 5 seconds, alarm blaring, THEN a fast retract: that is what
// makes "somebody is still in the room" a real, ticking problem instead of
// a progress bar you glance at.
//
// ====================================================================
// WHY OCCUPANCY BLOCKS THE RETRACT, NOT THE COUNTDOWN
//
// The spec is explicit: "the bridge cannot retract while a player is
// standing on it... being in the room is fair game. Being on the bridge is
// not - that's a bug, not a moment." Being IN THE ROOM when the timer hits
// zero is the intended cost of being late - the point of the whole system.
// Being physically ON the steel deck when it starts sliding out from under
// you is not tension, it is a collision bug. So the countdown always runs
// the full 5 seconds no matter who is where; only the RETRACT ACTION
// itself checks the deck, and holds at zero - alarm still blaring - until
// it is clear.
//
// ====================================================================
// NO AUDIO YET. The audio pass is Block 8 per DEMO_PLAN.md - the alarm here
// is entirely visual: the deck flashes red and the HUD counts down. Swap in
// a real siren when sound exists; nothing else here should need to change.
// ====================================================================

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Elevator))]
public class ElevatorBridge : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to extend on arrival, or to retract once the warning ends.")]
    public float travelTime = 2f;

    [Tooltip("Seconds of alarm before a departure actually retracts the bridge.")]
    public float warningTime = 5f;

    [Header("Size - set by ElevatorBuilder to match the shaft's gap")]
    public float length = 4.9f;
    public float width = 1.6f;

    enum State { Retracted, Extending, Extended, Warning, Retracting }
    State state = State.Retracted;

    /// <summary>
    /// True unless the bridge is stably extended and idle - so a departure
    /// is already in flight (Warning/Retracting), or it is still swinging
    /// out from the last arrival (Extending). ElevatorDashboard dims UP /
    /// DOWN / GO while this is true, the same "make the refusal visible"
    /// rule the rest of the panel already follows.
    /// </summary>
    public bool IsBusy => state != State.Extended;

    Elevator elevator;
    Transform deck;      // the CURRENT side's Bridge child
    float deckAnchorZ;    // where deck's near edge (the shutter line) sits
    float t;              // 0..1 through Extending/Retracting
    float warningLeft;

    // Every side's pristine anchor Z, read ONCE before anything ever
    // animates. See the note on CacheAnchors() for why this cannot just be
    // re-read from the deck's live position on every arrival.
    readonly Dictionary<string, float> anchors = new Dictionary<string, float>();

    int pendingFloor;
    bool pendingFast;
    bool wasMoving;

    MaterialPropertyBlock mpb;
    Color deckRestColor = Color.grey;   // overwritten from the material once a deck is found

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Collider[] Overlap = new Collider[16];

    void Awake()
    {
        elevator = GetComponent<Elevator>();
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        CacheAnchors();
        FindDeck();

        // Start already extended, not retracted. Steps 4-6 were tested with
        // the car sitting at a floor, shutter open - a bridge that begins
        // retracted under an already-open door reads as broken on frame one.
        if (deck != null)
        {
            state = State.Extended;
            t = 1f;
            ApplyDeckTransform();
        }

        wasMoving = elevator.IsMoving;
    }

    /// <summary>
    /// Read every side's Bridge deck at its PRISTINE, builder-placed
    /// position - z = wallMid, the shutter line - before anything has ever
    /// scaled or moved one.
    ///
    /// This must happen exactly once, up front. ApplyDeckTransform() clamps
    /// the collapsed length to 0.02 rather than a true 0 (a zero-size
    /// collider is a real Unity edge case, not just an ugly number), so a
    /// fully "retracted" deck sits 0.01m off its true anchor. Re-reading the
    /// anchor from the deck's CURRENT position on every arrival - which the
    /// first version of this method did - would bake that 1cm error in a
    /// little further each time the same side was ever visited twice.
    /// Reading it once, before the drift can start, avoids the problem
    /// rather than bounding it.
    /// </summary>
    void CacheAnchors()
    {
        var car = transform.Find("Car");
        if (car == null) return;

        foreach (Transform side in car)
        {
            if (!side.name.StartsWith("Side_")) continue;
            var d = side.Find("Bridge");
            if (d != null) anchors[side.name] = d.localPosition.z;
        }
    }

    /// <summary>
    /// Point at the current side's deck and pick up its real colour from
    /// the material - not a second hardcoded guess at the hazard colour
    /// ElevatorBuilder already baked in. Called on Start and every arrival,
    /// since the active side changes floor to floor.
    /// </summary>
    void FindDeck()
    {
        deck = transform.Find($"Car/{elevator.activeSide}/Bridge");
        if (deck == null)
        {
            Debug.LogWarning($"[Bridge] No Bridge child under {elevator.activeSide} - " +
                             "run Build Elevator Car.");
            return;
        }

        // Falls back to whatever is on the object right now if this side was
        // somehow missed by CacheAnchors (should not happen - defensive only).
        deckAnchorZ = anchors.TryGetValue(elevator.activeSide, out float z) ? z : deck.localPosition.z;

        var rend = deck.GetComponent<Renderer>();
        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId))
            deckRestColor = rend.sharedMaterial.GetColor(BaseColorId);
    }

    // ------------------------------------------------------------------
    // THE GATE. ElevatorDashboard calls these instead of the matching
    // Elevator methods.
    // ------------------------------------------------------------------

    public void RequestGoUp() => RequestGoToFloor(elevator.TargetFloor - 1, false);
    public void RequestGoDown() => RequestGoToFloor(elevator.TargetFloor + 1, false);

    public void RequestGoToFloor(int floor, bool fast)
    {
        if (elevator.IsMoving) return;

        // The ONE real fallback: no Bridge object exists for this side at
        // all (missing geometry). Go straight there rather than strand the
        // crew over an object that was never built.
        //
        // This must check `deck == null` specifically, NOT `state !=
        // Extended` - the first version of this method used the latter, and
        // Warning/Retracting/Extending are also states where state != Extended
        // is true. That meant pressing a button AGAIN mid-countdown, or while
        // the bridge was still swinging out on arrival, fell into this same
        // branch and called elevator.GoToFloor directly - moving the car with
        // the bridge still extended, which is exactly the bug the whole gate
        // exists to prevent.
        if (deck == null)
        {
            elevator.GoToFloor(floor, fast);
            return;
        }

        // Anything other than a stable, fully-extended bridge means a
        // departure is already queued (Warning/Retracting) or the bridge has
        // not finished arriving yet (Extending). Ignore the press rather than
        // let a second request race the first one to the elevator.
        if (state != State.Extended) return;

        pendingFloor = floor;
        pendingFast = fast;
        state = State.Warning;
        warningLeft = warningTime;
    }

    // ------------------------------------------------------------------

    void Update()
    {
        // Arrival: Elevator just stopped. Re-find the deck for whatever
        // side is now active and swing it out.
        bool moving = elevator.IsMoving;
        if (wasMoving && !moving)
        {
            FindDeck();
            state = State.Extending;
            t = 0f;
        }
        wasMoving = moving;

        switch (state)
        {
            case State.Extending:
                t += Time.deltaTime / Mathf.Max(0.01f, travelTime);
                if (t >= 1f) { t = 1f; state = State.Extended; }
                ApplyDeckTransform();
                break;

            case State.Warning:
                warningLeft -= Time.deltaTime;
                FlashAlarm();

                if (warningLeft <= 0f)
                {
                    warningLeft = 0f;
                    if (DeckIsClear())
                    {
                        state = State.Retracting;
                        // t is already 1 (fully extended) - retract shrinks
                        // it back down over the same travelTime.
                    }
                    // else: held at zero, alarm keeps flashing, checked
                    // again next frame. This is the rule, not a bug.
                }
                break;

            case State.Retracting:
                t -= Time.deltaTime / Mathf.Max(0.01f, travelTime);
                ApplyDeckTransform();

                if (t <= 0f)
                {
                    t = 0f;
                    state = State.Retracted;
                    ResetDeckColor();
                    elevator.GoToFloor(pendingFloor, pendingFast);
                }
                break;
        }
    }

    /// <summary>
    /// Anyone standing on the deck right now. Same OverlapBox technique as
    /// Elevator.GatherRiders, scoped to the bridge's own footprint instead
    /// of the car's.
    /// </summary>
    bool DeckIsClear()
    {
        if (deck == null) return true;

        Vector3 centre = deck.TransformPoint(new Vector3(0f, 0.6f, 0f));
        Vector3 half = new Vector3(width * 0.5f, 0.7f, length * 0.5f + 0.1f);

        int n = Physics.OverlapBoxNonAlloc(centre, half, Overlap, deck.rotation,
                                           ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
            if (Overlap[i].GetComponentInParent<PlayerMotor>() != null)
                return false;

        return true;
    }

    // ------------------------------------------------------------------
    // GEOMETRY - near edge FIXED at the anchor, far edge moves.
    //
    // A Unity primitive cube's pivot is its CENTRE, so scaling length alone
    // would grow the deck symmetrically in both directions - half of it
    // pushing back through the car's own wall. The anchor (deckAnchorZ, the
    // shutter line) is where the NEAR edge must always stay, so the centre
    // has to move outward by half the current length as that length grows -
    // the same "position tracks half the length" relationship
    // ElevatorCable.cs uses for the hoist rope, just anchored at one end
    // instead of centred between two.
    // ------------------------------------------------------------------

    void ApplyDeckTransform()
    {
        if (deck == null) return;

        float currentLength = Mathf.Max(0.02f, length * t);

        var scale = deck.localScale;
        scale.z = currentLength;
        deck.localScale = scale;

        var pos = deck.localPosition;
        pos.z = deckAnchorZ + currentLength * 0.5f;
        deck.localPosition = pos;
    }

    void FlashAlarm()
    {
        if (deck == null) return;
        var rend = deck.GetComponent<Renderer>();
        if (rend == null) return;

        // A hard blink, not a smooth pulse - alarms interrupt, they do not breathe.
        bool on = Mathf.FloorToInt(Time.time * 4f) % 2 == 0;

        rend.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, on ? new Color(1f, 0.15f, 0.1f) : deckRestColor);
        rend.SetPropertyBlock(mpb);
    }

    void ResetDeckColor()
    {
        if (deck == null) return;
        var rend = deck.GetComponent<Renderer>();
        if (rend == null) return;

        rend.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, deckRestColor);
        rend.SetPropertyBlock(mpb);
    }

    // ------------------------------------------------------------------
    // THROWAWAY HUD - the countdown itself. Nobody should have to glance at
    // a corner readout to feel five seconds; this is loud on purpose, the
    // same way RunManager's room-charge warning is.
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;
        if (state != State.Warning) return;

        var big = new GUIStyle(GUI.skin.label)
        { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        big.normal.textColor = Mathf.FloorToInt(Time.time * 4f) % 2 == 0
            ? new Color(1f, 0.25f, 0.2f)
            : new Color(1f, 0.7f, 0.2f);

        GUI.Label(new Rect(0f, Screen.height * 0.14f, Screen.width, 44f),
                  $"BRIDGE RETRACTING - {Mathf.CeilToInt(warningLeft):0}", big);
    }
}
