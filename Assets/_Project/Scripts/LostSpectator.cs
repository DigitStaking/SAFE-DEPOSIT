// LostSpectator.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LostSpectator.cs
// Goes on: the Player root.
//
// ====================================================================
// PHASE 4 STEP 9 - BEING HELD BY THE MAFIA HAS TO LOOK LIKE SOMETHING.
//
// A crewmate who bled out was still kneeling on the floor next to the crew
// afterwards, and came back with them for the next round. Reported exactly
// that way, and it is worse than untidy - it empties Step 9 out.
//
// The rescue contract asks a crew to spend two rounds of surplus buying
// somebody back. Nobody is going to argue about that price for a man they
// can see, standing right there, coming along anyway. THE MAFIA HAS TO
// ACTUALLY HAVE HIM, or the debt is a formality.
//
// SO: THE BODY GOES, AND THE PLAYER STAYS
//
// Hidden and inert - no renderers, no collider, no physics, not a rider, not
// in the way. But the PlayerMotor stays registered, because Crew rows are
// keyed on slot and the whole rescue is bookkeeping against that row. Delete
// the body and there is nobody to buy back.
//
// AND THEY WATCH
//
// Not a black screen. Being lost is meant to hurt, and watching the crew
// decide whether you are worth the money hurts far more than a loading
// screen does - it is the same reason the bleed-out lets you look around
// instead of fading out. TAB cycles whoever is still standing.
//
// This is deliberately NOT the full crew-screen work from Step 11. No
// nameplates, no chat, no lobby. One camera, following one living person,
// and a line telling you what you cost.
// ====================================================================

using UnityEngine;

[DefaultExecutionOrder(70)]      // after the cull and the hands
public class LostSpectator : MonoBehaviour
{
    [Tooltip("How far behind the person you are watching.")]
    public float distance = 3.4f;

    [Tooltip("Height above their feet.")]
    public float height = 1.9f;

    bool hidden;
    int watching;
    Renderer[] renderers;
    Collider[] colliders;
    Rigidbody body;
    FirstPersonCamera cam;

    Crew.Member Me => Crew.Of(this);

    void Update()
    {
        bool lost = Me.Lost;

        if (lost && !hidden) Hide();
        else if (!lost && hidden) Restore();

        if (!lost || !PlayerRegistry.IsLocalFor(this)) return;

        var kb = PlayerRegistry.KeysOf(this);
        if (kb != null && kb.tabKey.wasPressedThisFrame) watching++;
    }

    void LateUpdate()
    {
        if (!hidden || !PlayerRegistry.IsLocalFor(this)) return;

        var living = FindSomebodyStanding();
        if (living == null) return;

        if (cam == null) cam = Object.FindFirstObjectByType<FirstPersonCamera>();
        if (cam == null) return;

        // The camera belongs to a body that is no longer in the world, so it
        // is driven directly here rather than through FirstPersonCamera's
        // normal follow - which would keep trying to sit inside a hidden head.
        cam.enabled = false;

        Vector3 back = -living.transform.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 0.01f) back = Vector3.back;
        back.Normalize();

        Vector3 want = living.transform.position + Vector3.up * height + back * distance;

        cam.transform.position = Vector3.Lerp(cam.transform.position, want,
                                              8f * Time.deltaTime);
        cam.transform.rotation = Quaternion.Slerp(
            cam.transform.rotation,
            Quaternion.LookRotation(
                living.transform.position + Vector3.up * 1.2f - cam.transform.position),
            8f * Time.deltaTime);
    }

    /// <summary>
    /// Somebody still on their feet, cycled with TAB. Returns null when the
    /// whole crew is down - at which point the run is over anyway and
    /// RunManager is drawing its own screen over the top of this.
    /// </summary>
    PlayerMotor FindSomebodyStanding()
    {
        var standing = new System.Collections.Generic.List<PlayerMotor>();

        foreach (var p in PlayerRegistry.All)
        {
            if (p == null || p.gameObject == gameObject) continue;

            var row = Crew.Of(p.Slot);
            if (row.Lost || row.IsDowned) continue;

            standing.Add(p);
        }

        if (standing.Count == 0) return null;
        return standing[((watching % standing.Count) + standing.Count) % standing.Count];
    }

    void Hide()
    {
        hidden = true;

        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        body = GetComponent<Rigidbody>();

        foreach (var r in renderers) if (r != null) r.enabled = false;
        foreach (var c in colliders) if (c != null) c.enabled = false;

        // Kinematic as well as collider-less: a body with no colliders still
        // falls, forever, and would be a hundred metres down the shaft by the
        // time anybody paid for it.
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        Debug.Log($"[Crew] {gameObject.name} is being held by the mafia - " +
                  "body removed, spectating. TAB to change who you watch.");
    }

    void Restore()
    {
        hidden = false;

        if (renderers != null) foreach (var r in renderers) if (r != null) r.enabled = true;
        if (colliders != null) foreach (var c in colliders) if (c != null) c.enabled = true;

        // Only the owner's body is dynamic. A remote body was made kinematic
        // by NetworkPlayer on purpose, and putting it back would hand it to
        // local gravity again - the hover bug from Step 5.
        var netObj = GetComponent<Unity.Netcode.NetworkObject>();
        bool mine = netObj == null || !netObj.IsSpawned || netObj.IsOwner;

        if (body != null && mine) body.isKinematic = false;
        if (cam != null) cam.enabled = true;

        Debug.Log($"[Crew] {gameObject.name} was bought back.");
    }

    void OnGUI()
    {
        if (!hidden || !PlayerRegistry.IsLocalFor(this)) return;
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 17, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1f, 0.45f, 0.4f);

        GUI.Label(new Rect(0f, Screen.height * 0.5f - 40f, Screen.width, 30f),
                  "THE MAFIA HAS YOU", style);

        var sub = new GUIStyle(GUI.skin.label)
        { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        sub.normal.textColor = new Color(1f, 0.8f, 0.7f);

        // The price, on their screen, while they watch. They should know
        // exactly what they are worth and exactly who is deciding.
        int owed = 0;
        foreach (var m in Campaign.LostCrew)
            if (m.name == gameObject.name) owed = Campaign.RescueOwed(m);

        GUI.Label(new Rect(0f, Screen.height * 0.5f - 12f, Screen.width, 24f),
                  owed > 0
                      ? $"they want {owed} for you        TAB  watch somebody else"
                      : "TAB  watch somebody else", sub);
    }
}
