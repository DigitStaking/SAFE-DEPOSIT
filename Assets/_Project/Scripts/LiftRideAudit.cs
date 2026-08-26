// LiftRideAudit.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/LiftRideAudit.cs
// Goes on: nothing. Runs itself on load. Silent unless something is wrong.
//
// ====================================================================
// WHY THIS EXISTS
//
// A joining player keeps ending up in the shaft instead of in the car, and it
// has now survived three fixes: the world-space replication, the parent that
// does not carry a Rigidbody, and the delta measured from the origin. Each of
// those was a real bug and each was found by reasoning. This one is not going
// to be.
//
// ROADMAP's KNOWN ISSUES records the rule from the loot bug, which cost three
// wrong fixes and was solved in one run by an audit that printed positions:
// WHEN YOU ARE ABOUT TO REASON ABOUT WHETHER SOMETHING IS RIGHT, LOG IT
// INSTEAD. That rule has been earned twice today already - the two-body bug
// was named by four lines of editor log after two wrong guesses.
//
// So this prints, from the machine that has the problem, the six numbers that
// between them can only have one explanation:
//
//   car Y          where this machine thinks the floor is
//   observed       how far the car moved this step, as this machine saw it
//   my Y           where my body actually is
//   gap            my Y minus the car floor - THE SYMPTOM, as a number
//   parent         whether the car has adopted me
//   riders         whether the car can even see me to carry me
//
// If gap grows while observed is zero, nothing is carrying me and the car is
// moving without telling this machine. If gap grows while observed is
// correct, the carry is running and something undoes it. If riders is 0, the
// overlap box never found me and no carry was ever attempted. Those are
// different bugs with different fixes and they look identical from inside the
// game.
//
// Silent when the gap is small, so a working ride costs nothing.
// ====================================================================

using UnityEngine;

public static class LiftRideAudit
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        var go = new GameObject("~LiftRideAudit");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        Vector3 lastCar;
        bool haveLast;
        float nextLog;

        void FixedUpdate()
        {
            var lift = SceneRefs.Lift;
            var me = PlayerRegistry.Local;
            if (lift == null || me == null) { haveLast = false; return; }

            Vector3 car = lift.transform.position;
            Vector3 observed = haveLast ? car - lastCar : Vector3.zero;
            lastCar = car;
            haveLast = true;

            // MEASURED FROM THE STANDING SURFACE, NOT THE ROOT.
            //
            // This used to compare against the elevator's origin, on the
            // builder's word that the two are the same. In this scene they are
            // 1.2m apart, so a player standing perfectly still on the floor
            // read GAP=+1.20 and this audit reported a fault on every frame of
            // a working single-player game.
            //
            // Which is worse than useless: I read those lines as evidence of a
            // networking bug and went looking for one. An audit that cries
            // wolf costs more than no audit at all.
            float deck = car.y + lift.StandLocalY;
            float gap = me.transform.position.y - deck;

            // Standing on the floor of the car is a gap of roughly zero. Half
            // a metre is already "not in the lift any more".
            if (Mathf.Abs(gap) < 0.6f) return;
            if (Time.time < nextLog) return;
            nextLog = Time.time + 0.25f;

            bool inRiders = false;
            var myRb = me.GetComponent<Rigidbody>();
            foreach (var r in lift.Riders) if (r == myRb) { inRiders = true; break; }

            var nm = Unity.Netcode.NetworkManager.Singleton;
            string role = nm == null || !nm.IsListening
                ? "OFFLINE"
                : (nm.IsHost ? "HOST" : "CLIENT " + nm.LocalClientId);

            string parent = me.transform.parent == null
                ? "none"
                : me.transform.parent.name;

            // ---- THE THREE THAT NARROW IT TO ONE ANSWER ----
            //
            // 1. THE CAR'S BODY vs THE CAR'S TRANSFORM.
            //    observed is computed in Elevator from rb.position, but
            //    NetworkTransform writes transform.position. The car is
            //    KINEMATIC. If those two have drifted apart on a client, then
            //    observed is measuring a body that never moves while the
            //    visible car travels the whole shaft - which would explain
            //    observed=+0.000 on a car that demonstrably reached -15.
            //
            // 2. WHAT IS UNDERNEATH ME.
            //    A gap that is perfectly stable across twenty seconds is not
            //    floating, it is RESTING on something. Naming that collider
            //    ends the guessing: the car floor is one answer, another
            //    player's shoulders is a very different one.
            //
            // 3. AM I EVEN FALLING.
            //    Zero velocity and not kinematic means supported. Kinematic
            //    means something else is driving this body entirely.
            var liftRb = lift.GetComponent<Rigidbody>();
            float bodyY = liftRb != null ? liftRb.position.y : float.NaN;

            string under = "nothing";
            if (Physics.Raycast(me.transform.position + Vector3.up * 0.1f,
                                Vector3.down, out RaycastHit hit, 4f,
                                ~0, QueryTriggerInteraction.Ignore))
                under = $"{hit.collider.name}@{hit.distance:0.00}m";

            Debug.Log($"[Ride2] carBodyY={bodyY:0.00}  carTfY={car.y:0.00}" +
                      $"  drift={(car.y - bodyY):+0.00;-0.00}" +
                      $"  under={under}" +
                      $"  myVelY={(myRb != null ? myRb.linearVelocity.y : float.NaN):0.00}" +
                      $"  kinematic={(myRb != null && myRb.isKinematic)}");

            Debug.Log($"[Ride] {role}  decides={ElevatorNet.Decides}" +
                      $"  deckY={deck:0.00}  observed={observed.y:+0.000;-0.000}" +
                      $"  myY={me.transform.position.y:0.00}  GAP={gap:+0.00;-0.00}" +
                      $"  rbY={(myRb != null ? myRb.position.y : float.NaN):0.00}" +
                      $"  parent={parent}  inRiders={inRiders}" +
                      $"  riders={lift.Riders.Count}  moving={lift.IsMoving}" +
                      $"  floor={lift.CurrentFloor}->{lift.TargetFloor}");
        }
    }
}
