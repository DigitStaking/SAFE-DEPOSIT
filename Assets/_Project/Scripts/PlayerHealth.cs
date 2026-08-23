// PlayerHealth.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerHealth.cs
// Goes on: the PLAYER root, alongside PlayerMotor. Added automatically by
// SAFE DEPOSIT -> Fix First Person Setup, on both the prefab and the scene
// instance, so there is nothing to drag.
//
// ====================================================================
// PHASE 2 STEPS 2 AND 4 - HEALTH, AND THE LIMP.
// 100 HP, NO REGENERATION, EVER.
//
// PHASE2_SPEC Part 2 is blunt about why: "damage is permanent within a run,
// so a bad fall on floor 3 is still with you on floor 12. The only way back
// is a bandage you had to buy with money you wanted for cable."
//
// That sentence is an ECONOMY mechanic wearing a health bar. Getting hurt is
// not a setback you wait out, it is a BILL - and the money that pays it is
// the same money that buys cable, which is the same money the mafia is
// already taking. Every regenerating health system in every other game
// quietly deletes that decision, which is why there is no Update() in this
// file that adds a single point back.
//
// ====================================================================
// WHERE THE NUMBER LIVES, AND WHY IT IS NOT HERE
//
// The HP itself is Campaign.Health, not a field on this component.
//
// RunManager.ReloadScene() rebuilds the scene between runs. A serialized int
// on a MonoBehaviour is therefore back at 100 every single round, which would
// make "no regeneration" true only inside one run and false across the
// campaign - surfacing would be a full heal. ECONOMY Part 5 sells a Bandage
// for 10 that "heals 40"; a shop item that heals you is worthless if the ride
// up already did it for free.
//
// So this component owns the RULES and the readout. Campaign owns the number,
// exactly like Money. Same reasoning as Step 1's capacity, and the same trap
// avoided: a value that has to survive a reload cannot be stored in something
// that does not.
//
// ====================================================================
// WHAT THIS STEP DELIBERATELY DOES NOT DO
//
// Falling is now a real damage source - see PlayerFallDamage, Step 3. The
// debug keys below stay anyway: they are the only way to reach an exact HP
// value on demand, which tuning the limp and testing the downed state both
// need and a four-metre drop cannot give you.
//
// Step 4's limp IS here (SpeedFactor below), pulled forward because the
// readout was claiming DOWNED at 0 HP while you walked around at full speed,
// and a HUD that lies is worse than one that says nothing. The same
// multiplier that makes Hurt slow makes Downed motionless.
//
// At 0 HP you now also KNEEL, and cannot emote, pick anything up, or pull
// from the pack. None of that is new code in the animator: the Downed bool,
// the kneel state, the emote guard and the arm-IK release were all built in
// Phase 1 and had simply never been told when. PlayerHealth is the when.
//
// The bleed-out clock and the restricted view are DownedPlayer's, Step 5.
// What is still missing is Step 6: being a Carryable, so somebody can pick
// you up and spend your remaining seconds carrying you to the lift.
// ====================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("H damages you. Shift+H restores you. Kept past Step 3 because " +
             "falling cannot put you at an exact HP value on demand, and " +
             "tuning the limp needs exactly that.")]
    public bool debugKeys = true;
    public int debugDamage = 10;

    /// <summary>Health as the spec's states. Drives SpeedFactor below;
    /// the heavy breathing and the vignette are still to come.</summary>
    public enum Condition { Fine, Hurt, Critical, Downed }

    public int Current => Campaign.Health;
    public int Max => Campaign.MaxHealth;
    public float Fraction => Mathf.Clamp01((float)Campaign.Health / Campaign.MaxHealth);
    public bool IsDowned => Campaign.Health <= 0;

    /// <summary>PHASE2_SPEC's table: 100-51 Fine, 50-26 Hurt, 25-1 Critical,
    /// 0 Downed.</summary>
    public Condition State =>
        Campaign.Health <= 0 ? Condition.Downed :
        Campaign.Health <= 25 ? Condition.Critical :
        Campaign.Health <= 50 ? Condition.Hurt :
                                Condition.Fine;

    // ---- THE LIMP (PHASE2_SPEC Step 4) ----
    //
    // "Done when: you can tell someone is hurt without looking at a number."
    //
    // STEPPED, not a smooth ramp, and deliberately so. A continuous curve is
    // more realistic and completely unreadable - you cannot tell 0.91 from
    // 0.87, so nothing ever announces itself. A step change is a MOMENT: the
    // one where crossing 50 makes your own body feel different, which is the
    // only warning you get before Critical.
    //
    // PlayerMotor reads this every frame rather than having it pushed, so it
    // cannot be overwritten by the dashboard releasing you or by putting a
    // crate down. See the note above PlayerMotor.externalSpeedLock.

    [Header("The limp")]
    [Tooltip("Speed at 50-26 HP. A limp you notice.")]
    [Range(0.3f, 1f)] public float hurtSpeed = 0.78f;

    [Tooltip("Speed at 25-1 HP. Slow enough that the collapse clock becomes " +
             "a real problem.")]
    [Range(0.2f, 1f)] public float criticalSpeed = 0.52f;

    /// <summary>
    /// Multiplied into PlayerMotor's top speed. 0 while downed - being downed
    /// is not a speed penalty, it is the absence of standing up, and it is
    /// what makes the DOWNED readout tell the truth before Step 5 builds the
    /// bleed-out on top of it.
    /// </summary>
    public float SpeedFactor => State switch
    {
        Condition.Downed   => 0f,
        Condition.Critical => criticalSpeed,
        Condition.Hurt     => hurtSpeed,
        _                  => 1f,
    };

    // ---- THE SEAMS FOR STEPS 3, 4 AND 5 ----
    //
    // Written now, with no subscribers, on purpose. Step 5 needs to know the
    // exact frame you reach 0 in order to start the 90-second bleed-out, and
    // an event is how it finds that out without this file having to know that
    // downing exists at all. Step 4 wants Damaged for the hit reaction.

    /// <summary>(amount actually taken, HP remaining, what caused it).</summary>
    public event System.Action<int, int, string> Damaged;

    /// <summary>Fired once, on the frame HP first reaches 0.</summary>
    public event System.Action Downed;

    float lastHitTime = -99f;
    string lastCause = "";

    // Diagnostic only. Two guesses at why the kneel sinks have now cost more
    // than one measurement will - the same lesson the loot bug wrote into
    // ROADMAP's KNOWN ISSUES. So: measure, then set the number.
    Animator anim;
    Transform hips, footL, footR;

    // --------------------------------------------------------------------

    /// <summary>
    /// The only way HP goes down. Returns true if this hit is what downed you,
    /// so a caller can react to the hit it landed without re-reading state.
    /// </summary>
    public bool TakeDamage(int amount, string cause = "")
    {
        if (amount <= 0) return false;
        if (IsDowned) return false;          // already at 0; nothing left to take

        int before = Campaign.Health;
        Campaign.Health = Mathf.Max(0, before - amount);

        int taken = before - Campaign.Health;
        lastHitTime = Time.time;
        lastCause = cause;

        Damaged?.Invoke(taken, Campaign.Health, cause);

        if (Campaign.Health == 0)
        {
            Downed?.Invoke();
            return true;
        }
        return false;
    }

    // NOTE: there is no Heal() and no Update() that adds HP. Step 7 adds
    // healing, and it arrives attached to a thing you bought - the med spray
    // and the bandage - rather than as a method sitting here waiting for
    // someone to call it on a timer. Leaving the file with no upward path at
    // all is what makes "the number never climbs back" testable rather than a
    // promise.

    void Awake()
    {
        anim = GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            hips  = anim.GetBoneTransform(HumanBodyBones.Hips);
            footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        }
    }

    /// <summary>
    /// How far the lowest foot is BELOW the player's pivot, in metres. The
    /// pivot is at floor level, so a positive number is how far the pose has
    /// sunk through the floor. 0 means the clip is sitting correctly.
    /// </summary>
    public float SinkDepth
    {
        get
        {
            float lowest = float.MaxValue;
            if (footL != null) lowest = Mathf.Min(lowest, footL.position.y);
            if (footR != null) lowest = Mathf.Min(lowest, footR.position.y);
            if (lowest == float.MaxValue) return 0f;
            return transform.position.y - lowest;
        }
    }

    void Update()
    {
        if (!debugKeys) return;
        if (!PlayerRegistry.IsLocalFor(this)) return;   // one keyboard, one body

        var kb = Keyboard.current;
        if (kb == null) return;

        if (!kb.hKey.wasPressedThisFrame) return;

        // Shift+H is a TEST HARNESS, not a game mechanic. Without it, taking
        // yourself to 0 once would leave you at 0 across every scene reload
        // for the rest of the campaign - correct behaviour, useless for
        // checking the readout twice in one sitting.
        bool restore = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        if (restore)
        {
            Campaign.Health = Campaign.MaxHealth;
            lastHitTime = -99f;
            lastCause = "";
        }
        else
        {
            TakeDamage(debugDamage, "debug key");
        }
    }

    // --------------------------------------------------------------------
    // THE READOUT
    //
    // Top-left, ABOVE the quota. The quota is the crew's number and the
    // collapse clock is the building's; this one is yours, and it is the only
    // line on screen nobody else can do anything about.
    // --------------------------------------------------------------------

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        // MY HUD, not everyone's. Without this every body in the
        // scene draws its own copy on top of the same screen.
        if (!PlayerRegistry.IsLocalFor(this)) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 15 };

        Color colour = State switch
        {
            Condition.Downed   => new Color(1f, 0.2f, 0.15f),
            Condition.Critical => new Color(1f, 0.35f, 0.25f),
            Condition.Hurt     => new Color(1f, 0.75f, 0.3f),
            _                  => new Color(0.55f, 0.95f, 0.6f),
        };

        // A short white flash on the frame you are hit, so damage reads as an
        // EVENT and not just a smaller number you might not have looked at.
        float since = Time.time - lastHitTime;
        if (since < 0.25f) colour = Color.Lerp(Color.white, colour, since / 0.25f);

        string label = IsDowned
            ? $"HP  0 / {Max}   DOWNED"
            : $"HP  {Current} / {Max}" +
              (State == Condition.Fine ? "" : $"   {State.ToString().ToUpper()}");

        // The speed penalty, spelled out. Feeling slower is the point, but
        // while tuning hurtSpeed and criticalSpeed you need the number too.
        if (!IsDowned && SpeedFactor < 1f)
            label += $"   speed {SpeedFactor * 100f:0}%";

        if (!string.IsNullOrEmpty(lastCause) && since < 3f)
            label += $"   ({lastCause})";

        style.normal.textColor = colour;
        GUI.Label(new Rect(24f, 14f, 520f, 24f), label, style);

        if (debugKeys)
        {
            var hint = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.35f);
            string line = $"debug:  H  -{debugDamage} HP     Shift+H  restore";

            // While downed, print how far the pose has sunk through the floor.
            // Read this off the screen and the clip's Y offset is no longer a
            // guess - it is that number.
            if (IsDowned && (footL != null || footR != null))
            {
                float sink = SinkDepth;
                line += $"     sink {sink:0.000}m";
                if (hips != null)
                    line += $"   hips {hips.position.y - transform.position.y:0.000}m";
            }

            GUI.Label(new Rect(24f, Screen.height - 96f, 520f, 18f), line, hint);
        }
    }
}
