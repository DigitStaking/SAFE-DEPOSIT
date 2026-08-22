// ElevatorCable.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/ElevatorCable.cs
// Goes on: the ELEVATOR root, alongside Elevator.cs.
//
// ====================================================================
// THE WIRE ROPE.
//
// A cylinder stretched from the winch at the top of the shaft to the hitch
// on the car's roof. That is the entire implementation, and it is worth
// being clear about why something this trivial gets its own file and its own
// place in the spec.
//
// The old design simulated a 32-node Verlet rope, and deleting it is what
// bought this project about five weeks and made netcode survivable. What it
// did NOT buy was permission to have nothing there. The cable is what makes
// three separate systems legible:
//
//   THE LOAD LIMIT has a physical object attached to it. 550 kg is a number
//   on a gauge; a steel rope you can see going taut is an argument.
//
//   THE FRAY TRAP survives. Overload the car and this is the thing that
//   starts coming apart, in view, above your heads.
//
//   THE SHOP SELLS THIS. More wire rope on the drum is how you reach a
//   deeper floor. You are not buying abstract depth, you are buying the
//   thing hanging over you, and a crew that has bought none this round can
//   look up and see how little is left.
//
// So it is a cylinder, and it is nearly free, and it is doing more work than
// its line count suggests.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(Elevator))]
public class ElevatorCable : MonoBehaviour
{
    [Header("Ends")]
    [Tooltip("Top of the shaft. Found by name if left empty.")]
    public Transform winchAnchor;

    [Tooltip("Hitch on the car roof. Found by name if left empty.")]
    public Transform carAnchor;

    [Header("Look")]
    public float radius = 0.035f;
    public Color colour = new Color(0.30f, 0.31f, 0.33f);

    Transform rope;
    Material ropeMat;
    float fray;

    // ------------------------------------------------------------------
    // THE FRAY, MADE VISIBLE (PHASE2_SPEC Step 10)
    //
    // "Fray is visible on the cable itself, above your heads."
    //
    // Three cues, because one is not enough at a distance in a dark shaft:
    //
    //   COLOUR   steel grey rusts toward a hot orange. Reads at a glance and
    //            in peripheral vision, which is where this thing lives.
    //   THICKNESS the rope THINS as it goes, because that is what a wire rope
    //            actually does - strands part and the remainder carries the
    //            load. It also means the cue survives for a colourblind
    //            player, who would otherwise get nothing from the first one.
    //   SHIVER   past 85% it trembles. Motion is what catches an eye that was
    //            looking somewhere else entirely, and by then it should.
    //
    // No number anywhere. The done-when for this step is "you look UP at the
    // cable before pressing GO", and a percentage on the HUD satisfies the
    // mechanic while deleting the moment it exists for.
    // ------------------------------------------------------------------

    public void SetFray(float value)
    {
        fray = Mathf.Clamp01(value);

        if (ropeMat != null)
            ropeMat.SetColor("_BaseColor",
                Color.Lerp(colour, new Color(0.62f, 0.24f, 0.10f), fray));
    }

    void Start()
    {
        if (winchAnchor == null)
        {
            // GrayboxBuilder makes this empty at the top of the shaft, for
            // exactly this purpose. It was there for the old rope and it
            // means the same thing now.
            var shaft = GameObject.Find("SHAFT");
            if (shaft != null) winchAnchor = shaft.transform.Find("Winch_Anchor");
        }

        if (carAnchor == null)
            carAnchor = transform.Find("Car/CableHitch/CableAnchor");

        if (winchAnchor == null || carAnchor == null)
        {
            Debug.LogWarning("[Cable] Missing an anchor - no rope drawn. " +
                             "Build the graybox shaft and the elevator car first.");
            enabled = false;
            return;
        }

        BuildRope();
    }

    void BuildRope()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Cable";

        // No collider. The rope is scenery: a collider stretched down the
        // middle of the shaft would catch loot thrown down it, block the
        // player's pickup cast, and give the car something to shove.
        Object.Destroy(go.GetComponent<Collider>());

        // NOT parented to the car. It has to stay still at the top while the
        // bottom moves, and a child would inherit the car's motion at both
        // ends and never appear to pay out at all.
        rope = go.transform;
        rope.SetParent(null, true);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Smoothness", 0.55f);   // steel, not string
            mat.SetFloat("_Metallic", 0.85f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            ropeMat = mat;
        }

        SetFray(Campaign.CableFray);

        Stretch();
    }

    // LateUpdate, after Elevator has finished moving the car this frame. In
    // Update the rope would lag the hitch by a frame and visibly detach from
    // the roof every time the car changed speed.
    void LateUpdate()
    {
        if (rope != null) Stretch();
    }

    void Stretch()
    {
        Vector3 top = winchAnchor.position;
        Vector3 bottom = carAnchor.position;

        Vector3 mid = (top + bottom) * 0.5f;
        float length = Vector3.Distance(top, bottom);

        rope.position = mid;

        // Unity's cylinder primitive is 2 units tall, so a scale of 1 draws
        // 2 metres. Half the length is the scale that spans it.
        //
        // Thinner as it frays - down to 45% of new at the point it parts -
        // plus a shiver in the last 15%, which is the cue that works on
        // somebody who was not looking at it.
        float thin = Mathf.Lerp(1f, 0.45f, fray);
        if (fray > 0.85f)
            thin *= 1f + Mathf.Sin(Time.time * 40f) * 0.18f;

        float r = Mathf.Max(0.004f, radius * thin);
        rope.localScale = new Vector3(r, Mathf.Max(0.01f, length * 0.5f), r);

        // up = from the car toward the winch. Guard the degenerate case:
        // when the car is parked at the very top the two anchors coincide,
        // LookRotation of a zero vector logs an error every frame.
        Vector3 dir = top - bottom;
        if (dir.sqrMagnitude > 0.0001f)
            rope.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
    }

    void OnDestroy()
    {
        if (rope != null) Destroy(rope.gameObject);
    }
}
