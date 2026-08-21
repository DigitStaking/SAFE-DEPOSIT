// DownedPlayer.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/DownedPlayer.cs
// Goes on: the PLAYER root, alongside PlayerHealth. Added automatically by
// SAFE DEPOSIT -> Fix First Person Setup.
//
// ====================================================================
// PHASE 2 STEP 5 - DOWNED AND BLEED-OUT.
//
// "At 0 HP you do not die. You drop where you stood and start a 90-second
// bleed-out."
//
// The kneel, the frozen legs and the locked hands arrived early, when the HUD
// was claiming DOWNED while you walked around at full speed. What was still
// missing is the only part that makes any of it a GAME: a number counting
// down that somebody else can do something about.
//
// ====================================================================
// THE CLOCK DOES NOT STOP WHEN SOMEBODY PICKS YOU UP.
//
// PHASE2_SPEC is explicit and it is the single most important line in this
// file: "The clock does not stop because someone picked you up. Carrying you
// to the lift is a race, not a rescue."
//
// A timer that pauses on rescue turns the whole thing into an escort with no
// stakes - grab them, walk carefully, nothing was ever at risk. A timer that
// keeps running means the person carrying you is spending YOUR seconds on
// their route choice, which is the argument this phase exists to create. So
// nothing in this file has an "if being carried" branch, and that absence is
// deliberate rather than unfinished.
//
// ====================================================================
// WHY THE REMAINING SECONDS LIVE IN Campaign
//
// Same reason as Health and the loot roster: RunManager.ReloadScene destroys
// every runtime object between rounds, and Campaign.Health persists at 0. A
// player who is down when the scene rebuilds has to come back down, with the
// time they had left - not with a fresh ninety seconds because the component
// holding the number was new.
//
// ====================================================================
// STEP 6 - AND THE CARRYABLE APPEARS AND DISAPPEARS WITH THE STATE.
//
// A downed player IS a Carryable. The component is ADDED when you go down
// and DESTROYED when you are revived, rather than sitting on the player all
// the time switched off.
//
// That is not tidiness. A Carryable that exists on a healthy player is a
// healthy player who can be picked up: PlayerCarry finds targets by
// raycasting for the component, so a permanent one would let the crew carry
// each other around the building at full health, and the two-handed penalty
// would become a griefing tool rather than a rescue. Existence IS the
// eligibility check, and there is no second flag to keep in sync with it.
//
// Everything else about being cargo falls out of PlayerMotor's mass, which
// Step 1 already set to Campaign.PlayerMass = 70:
//
//     70kg  ->  Weight.Massive  ->  two hands, no jumping, 0.45 speed
//
// No special case anywhere in Carryable computes those. They are the same
// three rules a water tank gets, which is exactly what the design wanted:
// "every Phase 1 system handles them without knowing they are a person."
//
// ====================================================================
// WHAT IS STILL OWED
//
// Step 7 adds the med spray, which calls Revive() below. Steps 8 and 9 turn
// BledOut into the Lost roster and the rescue contract; for now it ends the
// run with its own outcome, which is what Step 5's "does something distinct
// from dying" asks for.
// ====================================================================

using UnityEngine;

public class DownedPlayer : MonoBehaviour
{
    [Tooltip("Seconds from hitting 0 HP to being Lost. PHASE2_SPEC: 90.")]
    public float bleedOutTime = 90f;

    [Tooltip("HP you come back on. Enough to move, not enough to relax - " +
             "Critical, so a revived crewmate is still limping and one more " +
             "mistake from going down again.")]
    public int reviveHealth = 20;

    [Header("The view while down")]
    [Tooltip("Degrees either side of where you fell. PHASE2_SPEC says you " +
             "cannot look FREELY - not that you cannot look. Wide enough to " +
             "watch someone come for you, narrow enough that you cannot scan " +
             "the room like nothing happened.")]
    public float lookArc = 100f;

    public float downedMinPitch = -40f;
    public float downedMaxPitch = 25f;

    /// <summary>Seconds left before Lost. Only meaningful while downed.</summary>
    public float TimeLeft => Campaign.BleedOutLeft;

    public bool IsDowned => health != null && health.IsDowned;

    /// <summary>Fired once, when the clock runs out. Step 8 listens.</summary>
    public event System.Action BledOut;

    PlayerHealth health;
    FirstPersonCamera fpCam;
    RunManager run;
    Carryable cargo;

    bool clockRunning;
    bool bledOut;

    // The camera's own limits, so the downed clamp can be handed back
    // untouched rather than reset to a guess about what they used to be.
    float restMinPitch, restMaxPitch;
    float anchorYaw;
    bool anchored;

    void Awake() => health = GetComponent<PlayerHealth>();

    void Start()
    {
        run = Object.FindFirstObjectByType<RunManager>();
        if (Camera.main != null) fpCam = Camera.main.GetComponent<FirstPersonCamera>();
        if (fpCam != null)
        {
            restMinPitch = fpCam.minPitch;
            restMaxPitch = fpCam.maxPitch;
        }

        // Already down when the scene loaded - Campaign.Health survives a
        // reload, so pick the clock back up where it was rather than starting
        // a fresh ninety seconds.
        if (IsDowned) BeginOrResume();
    }

    void Update()
    {
        if (health == null) return;

        if (!IsDowned)
        {
            if (clockRunning) End();
            return;
        }

        if (!clockRunning) BeginOrResume();

        // No "if being carried" branch. See the note at the top of the file.
        Campaign.BleedOutLeft -= Time.deltaTime;
        ClampLook();

        if (Campaign.BleedOutLeft <= 0f && !bledOut)
        {
            Campaign.BleedOutLeft = 0f;
            bledOut = true;
            Campaign.PlayerLost = true;
            BledOut?.Invoke();
            if (run != null) run.OnBleedOut();
        }
    }

    void BeginOrResume()
    {
        clockRunning = true;
        bledOut = Campaign.PlayerLost;

        // A fresh downing, rather than a reload of one already in progress.
        if (Campaign.BleedOutLeft <= 0f && !Campaign.PlayerLost)
            Campaign.BleedOutLeft = bleedOutTime;

        anchorYaw = transform.eulerAngles.y;
        anchored = true;

        BecomeCargo();
    }

    void End()
    {
        clockRunning = false;
        anchored = false;
        Campaign.BleedOutLeft = 0f;

        StopBeingCargo();

        if (fpCam != null)
        {
            fpCam.minPitch = restMinPitch;
            fpCam.maxPitch = restMaxPitch;
        }
    }

    /// <summary>
    /// Step 7's med spray calls this. Public now because the seam is what
    /// makes Step 7 a small change rather than a rewrite - and because
    /// without it there is no way to test coming back.
    /// </summary>
    public void Revive()
    {
        if (!IsDowned || Campaign.PlayerLost) return;
        Campaign.Health = Mathf.Clamp(reviveHealth, 1, Campaign.MaxHealth);
        End();
    }

    /// <summary>
    /// 70kg of Massive cargo. The value, uniquely, is 0 - a person is not
    /// loot, and RunManager.CountRecoveredValue sums Carryable.value for
    /// everything aboard. Carrying a crewmate out must not pay the mafia.
    /// </summary>
    void BecomeCargo()
    {
        if (cargo != null) return;

        cargo = GetComponent<Carryable>();
        if (cargo == null) cargo = gameObject.AddComponent<Carryable>();
        cargo.value = 0;
    }

    /// <summary>
    /// Revived, so no longer luggage. Destroyed rather than disabled: a
    /// disabled Carryable is still found by GetComponent, and the next thing
    /// to check "can I pick this up" would say yes.
    ///
    /// If they are being CARRIED at the moment of revival, drop them first -
    /// otherwise the carrier is left holding a reference to a component that
    /// is about to vanish, and the player stands up still kinematic and
    /// stuck to somebody's chest.
    /// </summary>
    void StopBeingCargo()
    {
        if (cargo == null) cargo = GetComponent<Carryable>();
        if (cargo == null) return;

        if (cargo.State != Carryable.CarryState.Free)
        {
            var carrier = Object.FindFirstObjectByType<PlayerCarry>();
            if (carrier != null && carrier.Held == cargo) carrier.ForceDrop();
            else cargo.Drop(Vector3.zero);
        }

        Destroy(cargo);
        cargo = null;
    }

    /// <summary>
    /// You can turn your head, not scan the room. The arc is anchored to the
    /// direction you were facing when you went down, so it is a fixed window
    /// on the world rather than a slow drift you could walk around the whole
    /// room with.
    /// </summary>
    void ClampLook()
    {
        if (fpCam == null || !anchored) return;

        fpCam.minPitch = downedMinPitch;
        fpCam.maxPitch = downedMaxPitch;

        float delta = Mathf.DeltaAngle(anchorYaw, fpCam.Yaw);
        if (Mathf.Abs(delta) > lookArc)
            fpCam.Yaw = anchorYaw + Mathf.Sign(delta) * lookArc;
    }

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;
        if (!IsDowned) return;

        var big = new GUIStyle(GUI.skin.label)
        { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        if (Campaign.PlayerLost)
        {
            big.normal.textColor = new Color(0.75f, 0.15f, 0.15f);
            GUI.Label(new Rect(0f, Screen.height * 0.34f, Screen.width, 44f),
                      "LOST", big);
            return;
        }

        // Counts in SECONDS all the way down rather than switching to m:ss.
        // Ninety of anything is a quantity; "47" is a number somebody can
        // shout across a room, which is the entire point of the state.
        float left = Campaign.BleedOutLeft;
        bool panic = left <= 20f;

        big.normal.textColor = panic && Mathf.FloorToInt(Time.time * 4f) % 2 == 0
            ? new Color(1f, 0.9f, 0.9f)
            : new Color(1f, 0.25f, 0.2f);

        GUI.Label(new Rect(0f, Screen.height * 0.34f, Screen.width, 44f),
                  $"BLEEDING OUT   {Mathf.CeilToInt(left)}", big);

        var sub = new GUIStyle(GUI.skin.label)
        { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        sub.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        GUI.Label(new Rect(0f, Screen.height * 0.34f + 46f, Screen.width, 22f),
                  "you can still talk", sub);
    }
}
