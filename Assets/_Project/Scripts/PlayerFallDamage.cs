// PlayerFallDamage.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerFallDamage.cs
// Goes on: the PLAYER root, alongside PlayerMotor and PlayerHealth. Added
// automatically by SAFE DEPOSIT -> Fix First Person Setup.
//
// ====================================================================
// PHASE 2 STEP 3 - FALL DAMAGE.
//
// "Done when: stepping off a crate is free, falling down the shaft is not."
//
// This is the step that makes three things built earlier mean what they were
// always supposed to mean:
//
//   * the 4.9m shaft gap, which GrayboxBuilder widened specifically so it
//     would read as "certain death rather than an embarrassing miss"
//   * the bridge retract countdown, which now retracts whether you are
//     standing on it or not - and until this file existed, dropped you down
//     a hundred-metre shaft for no damage at all
//   * PlayerHealth, which has had a working TakeDamage and no callers
//
// ====================================================================
// NO NEW PHYSICS. THE SPEED WAS ALREADY THERE.
//
// PHASE2_SPEC: "Reuses PlayerMotor's existing grounded/velocity state - no
// new physics." So this watches two things it does not own - the raw ground
// check and the Rigidbody's own y velocity - and does arithmetic.
//
// The PEAK downward speed is what gets recorded, not the speed on the frame
// of impact. By the time grounded flips true the collision has usually
// already eaten most of the velocity, so reading it then measures the
// solver's response rather than the fall.
//
// IsGroundedStrict, not IsGrounded: the coyote-time version stays true for
// 0.15s after you leave the floor, which is exactly long enough to miss the
// start of a short fall and to mis-time the landing on a long one.
//
// ====================================================================
// WHY RIDING THE LIFT IS EXCLUDED BY NAME AND NOT BY THRESHOLD
//
// Elevator.fastSpeed is 8 m/s. A 2-metre drop lands at 8.4 m/s. Those two
// numbers are close enough that NO safe-speed threshold can tell "the floor
// is descending under me" from "I stepped off something", and picking one
// that cleared the lift would also make a two-storey fall free.
//
// So the lift is excluded because it IS the lift, by asking the Elevator
// whether this body is one of its riders. That question has a correct answer
// available for free - Elevator.GatherRiders already computes it every
// physics step for the load gauge - and a tuned threshold never would have.
// ====================================================================

using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public class PlayerFallDamage : MonoBehaviour
{
    [Header("The safe drop")]
    [Tooltip("Impact speed you can land at for free, m/s. 9 clears your own " +
             "jump (6.2 m/s) and a two-metre drop (8.4), so stepping off " +
             "anything in a room costs nothing.")]
    public float safeSpeed = 9f;

    [Tooltip("HP per m/s over the safe speed. 6.5 puts one floor at ~28, two " +
             "floors at ~63, and four floors past 100.")]
    public float damagePerSpeed = 6.5f;

    [Tooltip("A landing this expensive also plays the stagger. Below it you " +
             "just take the number - a stumble on every stubbed toe reads as " +
             "a broken control scheme.")]
    public int staggerThreshold = 20;

    PlayerMotor motor;
    PlayerHealth health;
    PlayerAnimatorDriver driver;
    Rigidbody rb;
    Elevator lift;

    bool wasGrounded = true;
    float peakFallSpeed;

    void Awake()
    {
        motor  = GetComponent<PlayerMotor>();
        health = GetComponent<PlayerHealth>();
        driver = GetComponent<PlayerAnimatorDriver>();
        rb     = GetComponent<Rigidbody>();
    }

    void Start()
    {
        lift = SceneRefs.Lift;
    }

    void FixedUpdate()
    {
        if (motor == null || rb == null) return;

        bool grounded = motor.IsGroundedStrict;

        if (Riding())
        {
            // Not falling, being carried. Forget anything accumulated - a
            // player who steps off a crate INTO the descending car should not
            // bank the crate's drop and get billed for it on arrival.
            peakFallSpeed = 0f;
            wasGrounded = grounded;
            return;
        }

        if (!grounded)
        {
            // rb.linearVelocity.y is negative while falling.
            peakFallSpeed = Mathf.Max(peakFallSpeed, -rb.linearVelocity.y);
        }
        else
        {
            if (!wasGrounded) Land(peakFallSpeed);
            peakFallSpeed = 0f;
        }

        wasGrounded = grounded;
    }

    bool Riding()
    {
        if (lift == null || rb == null) return false;

        var riders = lift.Riders;
        for (int i = 0; i < riders.Count; i++)
            if (riders[i] == rb) return true;

        return false;
    }

    void Land(float speed)
    {
        if (health == null || health.IsDowned) return;
        if (speed <= safeSpeed) return;

        int damage = Mathf.RoundToInt((speed - safeSpeed) * damagePerSpeed);
        if (damage <= 0) return;

        // Reported as a HEIGHT, because that is what the player experienced.
        // "fell 12m" is a thing you can learn from; "landed at 20.6 m/s" is
        // a physics readout. Inverted from the same v = sqrt(2gh) the motor
        // uses to launch a jump, including its fall multiplier.
        float g = -Physics.gravity.y * Mathf.Max(1f, motor.fallGravityMultiplier);
        float metres = speed * speed / (2f * g);

        health.TakeDamage(damage, $"fell {metres:0}m");

        // The stagger already exists - AnimatorBuilder wires a Stunned clip to
        // a DoStun trigger and nothing has ever pulled it. Half a second of
        // not being in control is, in that file's own words, "the difference
        // between damage being a number that changed and damage being an
        // event that happened".
        if (damage >= staggerThreshold && driver != null && !health.IsDowned)
            driver.PlayStun();
    }
}
