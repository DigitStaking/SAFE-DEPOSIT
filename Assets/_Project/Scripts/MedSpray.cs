// MedSpray.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/MedSpray.cs
// Goes on: the Player root.
//
// ====================================================================
// PHASE 4 STEP 7 - THE MOMENT SOMEBODY GETS SAVED.
//
// PHASE2_SPEC deferred this on purpose, and the reasoning is worth repeating
// because it is the whole shape of the step: "Both ways of saving someone
// need a second player - you cannot spray yourself, and carrying yourself out
// is not a thing. Building it now would mean shipping a rescue verified only
// by reading the code, in the one phase whose entire point is the moment
// somebody gets saved."
//
// DownedPlayer.Revive() has been finished since Phase 2. What was missing was
// a way for another person to ask for it.
//
// HELD, NOT TAPPED
//
// Two seconds with the button down, standing still next to somebody who is
// bleeding out. It is not a fail state and it is not a minigame - it is the
// only mechanic in the game that asks you to stop moving in a building that
// is being demolished, while a timer you can see is running down.
//
// A tap would be free. The hold is the price, and the price is what makes
// going back for somebody a decision instead of a reflex.
//
// WHY THE REVIVE IS PERFORMED BY THE PERSON BEING REVIVED
//
// Step 4 made each Crew row owner-written: your machine decides what happens
// to your body. So I cannot set your health, even to help you - and that is
// not an obstacle to work around, it is the rule working. The spray sends a
// request; the host checks the kit and spends from it; the DOWNED PLAYER'S
// OWN MACHINE calls Revive.
//
// Three machines, three jobs, and each one does the thing it is the authority
// on. It reads like ceremony until you notice that the alternative is four
// machines all deciding somebody else's health.
// ====================================================================

using UnityEngine;
using Unity.Netcode;

public class MedSpray : MonoBehaviour
{
    [Header("Reach")]
    [Tooltip("How close you have to be to spray somebody. Short on purpose - " +
             "you have to be standing over them, not shouting from a doorway.")]
    public float range = 2.6f;

    [Tooltip("Unused since the aim check became a proximity check - kept so " +
             "the Inspector does not lose a value somebody may have tuned.")]
    public float radius = 0.7f;

    [Header("Cost")]
    [Tooltip("Seconds held. The only mechanic that asks you to stand still in " +
             "a building that is being demolished.")]
    public float useTime = 2f;

    public KeyCode key = KeyCode.R;

    float progress;
    DownedPlayer target;

    void Update()
    {
        if (!PlayerRegistry.IsLocalFor(this)) return;

        // Reviving while downed yourself is the one case that has to be
        // impossible rather than merely difficult - two people on the floor
        // spraying each other back up is not a rescue, it is a loop.
        var mine = Crew.Of(this);
        if (mine.IsDowned) { target = null; progress = 0f; return; }

        target = FindDowned();

        var kb = PlayerRegistry.KeysOf(this);
        bool holding = kb != null && kb[UnityEngine.InputSystem.Key.R].isPressed;

        // IT HAS TO BE IN YOUR HAND. Taking it out of the pack is a separate,
        // deliberate act - the same one crates need - so R is never the answer
        // to a prompt somebody has not prepared for.
        var pack = GetComponent<PlayerBackpack>();
        bool ready = pack != null && pack.SprayReady && mine.MedSprays > 0;

        if (target == null || !holding || !ready)
        {
            progress = 0f;
            return;
        }

        progress += Time.deltaTime;
        if (progress < useTime) return;

        progress = 0f;
        Ask(target);

        // Used, so it is no longer in hand. Carrying another one means taking
        // it out again deliberately, which is the right amount of friction for
        // spending something this scarce.
        if (pack != null) pack.PutSprayAway();
    }

    /// <summary>
    /// The downed crewmate in front of me, if any.
    ///
    /// AN OVERLAP, NOT A SPHERECAST. The first version cast a sphere forward
    /// from the eye - and the eye sits INSIDE the player's own capsule, so the
    /// sweep began already touching my own collider, returned that at distance
    /// zero, and the "is it me?" guard then correctly answered "yes" and
    /// returned nothing. Standing directly over a dying friend produced no
    /// prompt at all, which is exactly what was reported.
    ///
    /// A single cast can only ever report the nearest thing, and the nearest
    /// thing is always me. So this asks a different question: WHO IS NEARBY,
    /// then filters. Nobody has to be aimed at precisely - they are on the
    /// floor, in the dark, and a kneeling body is a small target.
    /// </summary>
    DownedPlayer FindDowned()
    {
        var eye = PlayerRegistry.EyeOf(this);
        if (eye == null) return null;

        var hits = Physics.OverlapSphere(transform.position, range, ~0,
                                         QueryTriggerInteraction.Ignore);

        DownedPlayer best = null;
        float bestDist = float.MaxValue;

        foreach (var c in hits)
        {
            var downed = c.GetComponentInParent<DownedPlayer>();
            if (downed == null || !downed.IsDowned) continue;
            if (downed.gameObject == gameObject) continue;

            float d = Vector3.Distance(transform.position, downed.transform.position);
            if (d < bestDist) { bestDist = d; best = downed; }
        }

        return best;
    }

    void Ask(DownedPlayer who)
    {
        var motor = PlayerRegistry.OwnerOf(who);
        var netObj = motor != null ? motor.GetComponent<NetworkObject>() : null;

        // OFFLINE, or a body with no network identity: do it here and now.
        // Single player must keep working, and this is the whole of what that
        // costs.
        if (netObj == null || !netObj.IsSpawned || CampaignNet.Instance == null)
        {
            var me = Crew.Of(this);
            if (me.MedSprays > 0)
            {
                me.MedSprays = me.MedSprays - 1;
                who.Revive();
            }
            return;
        }

        // MY slot, not the target's. The person who runs in is the person who
        // pays - that is what makes carrying the sprays a job somebody has to
        // volunteer for.
        int mySlot = PlayerRegistry.OwnerOf(this) is PlayerMotor m ? m.Slot : 0;
        CampaignNet.Instance.ReviveServerRpc(netObj.OwnerClientId, mySlot);
    }

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;
        if (!PlayerRegistry.IsLocalFor(this)) return;
        if (target == null) return;

        string msg;
        Color colour;

        var mine = Crew.Of(this);
        var pack = GetComponent<PlayerBackpack>();

        if (mine.MedSprays > 0 && (pack == null || !pack.SprayReady))
        {
            // Have one, not holding it. Name the key that actually helps.
            msg = "PRESS 1 TO TAKE OUT YOUR MED SPRAY";
            colour = new Color(0.6f, 1f, 0.6f);
        }
        else if (mine.MedSprays <= 0)
        {
            // Said plainly, because the alternative is a player holding R at a
            // dying friend and learning nothing from the silence.
            //
            // "YOU have none" rather than "there are none": sprays are carried
            // by a PERSON now, so somebody else in the crew may still have one
            // and the right move is to shout rather than give up. Carrying
            // them out is not an option either - that was cut on 26 Aug 2026,
            // see DownedPlayer.BecomeCargo - so this must not suggest it.
            // NOT a call to press R. Somebody with no spray was being shown
            // the same prompt as the medic, so they held R at a dying friend
            // and nothing happened. This says what is true and what would
            // help, and names no key at all.
            msg = "THEY NEED A MED SPRAY\nyou have none - somebody else may";
            colour = new Color(1f, 0.45f, 0.4f);
        }
        else if (progress > 0f)
        {
            int pips = Mathf.CeilToInt((useTime - progress) * 10f);
            msg = $"HOLD R   {new string('|', Mathf.Max(0, pips))}";
            colour = new Color(0.6f, 1f, 0.6f);
        }
        else
        {
            msg = $"HOLD R  to spray them back up   ({mine.MedSprays} on you)";
            colour = Color.white;
        }

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = colour;

        float w = 700f;
        GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f + 100f, w, 48),
                  msg, style);
    }
}
