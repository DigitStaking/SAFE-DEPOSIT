// ElevatorButton.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ElevatorButton.cs
// Goes on: each physical button on the dashboard fascia.
//
// ====================================================================
// A BUTTON THAT EXISTS IN THE ROOM.
//
// This could have been a UI Canvas and it would have been half the code.
// The reason it is a box with a collider is that three other people are
// standing behind you.
//
// A screen-space menu is invisible to everyone but the person holding it.
// A physical button can be watched, reached for, and argued with - somebody
// leaning toward GO while a crewmate is still in the room is a THING THAT
// HAPPENS IN FRONT OF YOU, and that moment is most of what the elevator was
// built for. It also means the panel is lit by the cage light, occluded by
// bodies, and stays put when the car moves, all for free.
//
// The cost is that hover and press have to be animated by hand. That is
// eleven lines, below.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ElevatorButton : MonoBehaviour
{
    public enum Kind { Up, Down, Digit, Go, Clear, Return }

    [Tooltip("What pressing this does. ElevatorDashboard reads it.")]
    public Kind kind = Kind.Up;

    [Tooltip("Only meaningful when kind is Digit.")]
    public int digit;

    [Header("Feel")]
    [Tooltip("How far the button sinks when pressed, in metres.")]
    public float travel = 0.014f;

    public float pressSpeed = 14f;
    public float hoverSpeed = 10f;

    [Tooltip("How much brighter the button gets under the cursor.")]
    public float hoverBoost = 0.5f;

    public bool Interactable { get; set; } = true;

    Renderer rend;
    MaterialPropertyBlock mpb;
    Color baseColour;
    Vector3 restPos;

    float hover;      // 0..1 under the cursor
    float press;      // 0..1 pushed in
    bool held;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        rend = GetComponent<Renderer>();

        // A MaterialPropertyBlock overrides one value on one renderer without
        // cloning the material. Assigning .material here would leave a
        // duplicate material per button, which is how a dashboard with twelve
        // buttons ends up with twelve materials nobody can find. PlayerSkin
        // uses the same approach for crew colours.
        mpb = new MaterialPropertyBlock();

        baseColour = rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId)
            ? rend.sharedMaterial.GetColor(BaseColorId)
            : Color.grey;

        restPos = transform.localPosition;
    }

    public void SetHover(bool on)
    {
        held = on && Interactable;
    }

    /// <summary>Visual only. ElevatorDashboard performs the actual action.</summary>
    public void Poke()
    {
        press = 1f;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        hover = Mathf.MoveTowards(hover, held ? 1f : 0f, hoverSpeed * dt);
        press = Mathf.MoveTowards(press, 0f, pressSpeed * dt);

        // Straight down local -Z, which is "into the fascia" by construction:
        // the buttons are children of the panel and stick out along its +Z.
        //
        // Doing it in LOCAL space matters. A world-space direction cached at
        // Awake would be wrong the moment the car turned, and would have to
        // be recomputed every frame to stay right - whereas local -Z is
        // correct forever, for free, however the dashboard is angled.
        transform.localPosition = restPos + Vector3.back * (travel * press);

        Color c = Interactable
            ? Color.Lerp(baseColour, baseColour + Color.white * hoverBoost, hover)
            : baseColour * 0.45f;

        // GetPropertyBlock first, or setting one property wipes any others
        // already on this renderer.
        rend.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, c);
        rend.SetPropertyBlock(mpb);
    }
}
