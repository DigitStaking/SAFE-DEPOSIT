// PlayerCarry.cs  -  SAFE DEPOSIT
// E: pickup / drop. Held items follow camera in LateUpdate.
//
// Step 8 briefly required carrying it to a marked deck square and pressing E
// there specifically. Reverted after playtest: it made the crew argue about
// exact positioning instead of just piling loot wherever there was room.
// ElevatorDeck.cs now counts anything physically inside the car, so a plain
// drop is enough - see Carryable.CarryState.Free for where that is decided.

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCarry : MonoBehaviour
{
    [Header("Hold position")]
    public Vector3 holdOffset = new Vector3(0.35f, -0.35f, 1.15f);

    [Header("Where a carried thing sits - tune these")]
    [Tooltip("Height above the feet that a SMALL item is carried at, in " +
             "metres. " +
             "This rig is short and stocky - its shoulder is at about 1.42 and " +
             "its eye at 1.55 - so 1.30 was shoulder height and put the crate " +
             "in the character's face. Chest is nearer 1.0.")]
    public float holdHeightSmall = 0.82f;

    [Tooltip("Height for a HEAVY item. Lower than small: a heavy thing is " +
             "carried against the body, not held up.")]
    public float holdHeightHeavy = 0.74f;

    [Tooltip("Height for a MASSIVE item - a safe, a vending machine. Lowest of " +
             "the three, because you hug it at waist level.")]
    public float holdHeightMassive = 0.66f;

    [Tooltip("How far in FRONT of the body a small item sits, in metres.")]
    public float holdDistanceSmall = 0.34f;

    [Tooltip("How far in front for a heavy item. Further out - a big box " +
             "cannot occupy the same space as your chest.")]
    public float holdDistanceHeavy = 0.44f;

    [Tooltip("How far in front for a massive item.")]
    public float holdDistanceMassive = 0.52f;
    public float holdSnapSpeed = 18f;

    [Header("Reach")]
    public LayerMask pickupMask = ~0;
    public float pickupRange = 2.5f;
    public float pickupRadius = 0.4f;

    public bool IsCarrying => held != null;

    /// <summary>
    /// What is in the player's hands right now, or null. Read-only on
    /// purpose - PriceScanner (Step 9) needs to inspect it, but only this
    /// script may change what is being carried.
    /// </summary>
    public Carryable Held => held;

    public float CarriedMass =>
        (held != null ? held.Mass : 0f) +
        (backpack != null ? backpack.TotalMass : 0f);

    public bool CanJump  => held == null || held.AllowsJumping;
    public float SpeedMultiplier => held != null ? held.SpeedMultiplier : 1f;

    Carryable held;
    Carryable lookingAt;
    Rigidbody rb;
    PlayerBackpack backpack;
    PlayerHealth health;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        backpack = GetComponent<PlayerBackpack>();
        health = GetComponent<PlayerHealth>();
    }

    /// <summary>
    /// My eye, asked LIVE.
    ///
    /// This was cached once in Start, and FindTarget returns null the moment
    /// it is gone - so after a round change, when the old scene's camera was
    /// destroyed with the old scene, nothing could be picked up ever again.
    /// Reported as "i can't grab items after a time, in round 2".
    ///
    /// Eleventh time this phase.
    /// </summary>
    Transform Eye => PlayerRegistry.EyeOf(this);

    void Update()
    {
        lookingAt = held == null ? FindTarget() : null;
    }

    void LateUpdate()
    {
        if (held == null) return;

        // A REMOTE BODY HAS NO CAMERA, AND STILL HAS HANDS.
        //
        // cam is my eye, and PlayerRegistry.EyeOf returns null for anybody
        // else - only the local player has a camera. So this used to return
        // early for every teammate, and a crate they were carrying just hung
        // in the air where they picked it up while they walked off with
        // nothing.
        //
        // In front of the chest, from the body's own facing. Not as precise as
        // a camera-relative hold, and it does not need to be: what matters
        // from across a dark floor is that the crate is with them.
        var cam = Eye;
        if (cam == null)
        {
            // ---- THE SAME BODY ANCHOR EVERY VIEWER USES ----
            //
            // This used to place the crate at the midpoint of the remote
            // body's HANDS, which was a good idea right up until
            // PlayerCarryArms started placing those hands under the CRATE. Two
            // systems each deriving their position from the other settle
            // nowhere.
            //
            // Both machines compute the same body-relative anchor from data
            // that already replicates, so the crate agrees everywhere without
            // being sent - and the hands now have something stable to grip
            // instead of chasing themselves.
            // ---- THE SAME ANCHOR, FOR THE SAME REASON ----
            //
            // This used to place the crate at the midpoint of the remote
            // body's HANDS, which was a good idea right up until PlayerCarryArms
            // started placing those hands under the CRATE. Two systems each
            // deriving their position from the other settle nowhere.
            //
            // Both machines now compute the same body-relative anchor from
            // replicated data, so the crate agrees everywhere without being
            // sent, and the hands have something stable to grip.
            held.transform.position = HoldAnchor();
            held.transform.rotation = HoldRotation();
            return;

        }

        // ---- ANCHORED TO THE BODY, NOT THE CAMERA ----
        //
        // This used cam.position + cam.rotation * offset, which is right for
        // exactly one viewpoint and wrong for every other. In THIRD PERSON the
        // camera is three metres behind the character, so the crate hung out
        // there in mid-air while the body walked around without it - the
        // reported "friends will see the items fixed".
        //
        // It was conceptually wrong as well as visibly wrong. A carried object
        // is a WORLD object; where it sits must not depend on where anybody's
        // camera happens to be, because a teammate's view of your crate has
        // nothing to do with your camera at all.
        //
        // The body is the anchor now - its position, and its YAW only. Yaw
        // because the body is welded to the camera horizontally, so the crate
        // still swings around as you look; pitch deliberately excluded, since
        // a box carried in two hands does not tilt when you glance at the
        // ceiling.
        Vector3 target = HoldAnchor();
        Quaternion facing = HoldRotation();

        held.transform.position = Vector3.Lerp(
            held.transform.position, target, holdSnapSpeed * Time.deltaTime);
        held.transform.rotation = Quaternion.Slerp(
            held.transform.rotation, facing, holdSnapSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Where the carried thing sits, for EVERY viewer.
    ///
    /// One answer, body-relative, so your own screen, a teammate's screen and
    /// third person all agree. Heights are chosen to land where the old
    /// camera-relative offsets did with the eye at 1.6, so the first-person
    /// framing is unchanged - it is the anchor that moved, not the result.
    ///
    /// This is also what breaks a feedback loop that had just been created:
    /// PlayerCarryArms puts the HANDS under the ITEM, and the old remote path
    /// put the ITEM at the midpoint of the HANDS. Each chased the other. The
    /// item is placed independently now and the hands follow it - one
    /// direction, no argument.
    /// </summary>
    public Vector3 HoldAnchor()
    {
        if (held == null) return transform.position + Vector3.up * 1.2f;

        // Height above the feet, and how far out in front. Fields rather than
        // constants, because the right numbers depend on the model's actual
        // proportions and this one is shorter than the hardcoded values
        // assumed - 1.30 was shoulder height on it, which put the crate in the
        // character's face.
        float up, out_;

        if (held.Weight == Carryable.WeightClass.Massive)
        {
            up = holdHeightMassive; out_ = holdDistanceMassive;
        }
        else if (held.Weight == Carryable.WeightClass.Heavy)
        {
            up = holdHeightHeavy; out_ = holdDistanceHeavy;
        }
        else
        {
            up = holdHeightSmall; out_ = holdDistanceSmall;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f) forward.Normalize();

        Vector3 anchor = transform.position + Vector3.up * up + forward * out_;

        // ---- THE ITEM'S OWN CORRECTION ----
        //
        // The weight class gets a crate roughly right and a flashlight roughly
        // wrong, because it only knows how heavy a thing is, not what shape it
        // is or which way its model points. This is where the item says the
        // rest.
        //
        // In the BODY'S space, so it means the same thing whichever way you
        // face - and so every machine computes the same answer from replicated
        // data, with nothing extra sent.
        Vector3 o = held.itemPositionOffset;

        if (o != Vector3.zero)
        {
            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude > 0.0001f) right.Normalize();

            anchor += right * o.x + Vector3.up * o.y + forward * o.z;
        }

        return anchor;
    }

    /// <summary>
    /// Which way the carried thing faces, for EVERY viewer.
    ///
    /// The body's YAW, plus whatever the item asks for on top. Yaw because the
    /// body is welded to the camera horizontally, so the item swings round as
    /// you look; pitch deliberately excluded, since a box carried in two hands
    /// does not tilt when you glance at the ceiling.
    /// </summary>
    public Quaternion HoldRotation()
    {
        Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        return held == null
            ? facing
            : facing * Quaternion.Euler(held.itemRotationOffset);
    }

    /// <summary>
    /// Q - set it down.
    ///
    /// Bound to the action that used to be HookRope, which has been dead since
    /// the rope was cut from Phase 4. The key was already on the keyboard doing
    /// nothing.
    ///
    /// Tap only, for now. Hold-to-throw is Step 2 of this phase and arrives on
    /// this same action.
    /// </summary>
    void OnPutDown(InputValue value)
    {
        if (!value.isPressed) return;
        if (!PlayerRegistry.IsLocalFor(this)) return;
        if (held == null) return;

        PlaceHeld();
    }

    void OnInteract(InputValue value)
    {
        if (!value.isPressed) return;

        // Only my hands. PlayerInput broadcasts to every body that has one.
        if (!PlayerRegistry.IsLocalFor(this)) return;

        // PHASE2_SPEC: while downed you "cannot move, look freely, or
        // interact". Dropping is allowed - if you go down holding a crate it
        // has to leave your hands, or the load gauge charges the crew for a
        // box nobody can reach.
        if (health != null && health.IsDowned)
        {
            if (held != null) DropHeld();
            return;
        }

        if (held == null)
        {
            if (lookingAt != null)
            {
                PickUp(lookingAt);
            }
            else if (backpack != null && backpack.Count > 0)
            {
                var item = backpack.TakeLast();
                if (item != null)
                {
                    item.PickUp();
                    held = item;

                    // Out of the bag is just a pickup, and pickup already
                    // travels. The receiving machines take it off that body's
                    // back and put it in its hands.
                    Announce(item, true);
                }
            }
            return;
        }

        // ---- E IS FOR TAKING. Q IS FOR GIVING UP. ----
        //
        // This used to be DropHeld(), so one key did both jobs. The failure is
        // small and constant: drop a crate with E and the very next E press
        // picks the same crate straight back up, because you are still looking
        // at it. Every mistimed press undoes itself.
        //
        // Hands full means E has nothing to do here now. Setting the thing
        // down is Q - PlaceHeld below - which also puts it somewhere chosen
        // rather than wherever your hands happened to be.
        //
        // The downed branch above KEEPS its drop, deliberately: that one is
        // not a convenience, it is the reason the load gauge cannot end up
        // charging the crew for a box nobody can reach.
    }

    Carryable FindTarget()
    {
        var cam = Eye;
        if (cam == null) return null;

        if (!Physics.SphereCast(cam.position, pickupRadius, cam.forward,
                                out RaycastHit hit, pickupRange,
                                pickupMask, QueryTriggerInteraction.Ignore))
            return null;

        var carryable = hit.collider.GetComponentInParent<Carryable>();
        if (carryable == null) return null;

        if (carryable.State == Carryable.CarryState.Held ||
            carryable.State == Carryable.CarryState.Stowed)
            return null;

        return carryable;
    }

    void PickUp(Carryable item)
    {
        // Small items auto-stow if pack has room. A person never qualifies -
        // 70kg is Massive and CanStow is Small-only - but the guard is spelled
        // out anyway, because "cannot stow a person" is a RULE and leaving it
        // implicit in a mass threshold means it silently stops being true the
        // day somebody retunes the weight classes.
        // Straight into the bag, and everyone is told - this route used to
        // send nothing at all, so a small item vanished into a pack on one
        // machine and went on lying on the floor everywhere else.
        if (!item.IsPerson && item.CanStow && backpack != null &&
            backpack.TryStow(item))
        {
            AnnounceStow(item);
            return;
        }

        item.PickUp();
        held = item;
        Announce(item, true);
    }

    // ==================================================================
    // PHASE 4 STEP 6 - TELL EVERYONE, BUT DO IT FIRST.
    //
    // My hands close IMMEDIATELY and the message goes out afterwards. Waiting
    // for a round trip before your own grab registers is the one lag a player
    // always notices, and there is nothing to be gained by it: if the host
    // refuses - somebody else got there in the same frame - the worst case is
    // that a crate briefly appeared in my hands and then did not.
    //
    // Everyone else finds out a moment later, which is fine, because for
    // everyone else this is somebody ELSE's hands.
    // ==================================================================
    void AnnounceStow(Carryable item)
    {
        var net = LootNet.Instance;
        if (net == null || !net.IsSpawned) return;

        var loot = item != null ? item.GetComponent<LootItem>() : null;
        if (loot == null || loot.RosterIndex < 0) return;

        net.RequestStowServerRpc(loot.RosterIndex,
                                 Unity.Netcode.NetworkManager.Singleton.LocalClientId);
    }

    void Announce(Carryable item, bool pickedUp)
    {
        var net = LootNet.Instance;
        if (net == null || !net.IsSpawned) return;      // offline: nobody to tell

        var loot = item != null ? item.GetComponent<LootItem>() : null;
        if (loot == null || loot.RosterIndex < 0) return;

        ulong me = Unity.Netcode.NetworkManager.Singleton.LocalClientId;

        if (pickedUp) net.RequestPickupServerRpc(loot.RosterIndex, me);
        else net.RequestDropServerRpc(loot.RosterIndex,
                                      item.transform.position,
                                      item.transform.rotation, me);
    }

    /// <summary>
    /// Somebody else's pickup, arriving. Puts the item in THIS body's hands
    /// without sending anything back - otherwise the confirmation would be
    /// re-announced and bounce around the session forever.
    /// </summary>
    public void ReceiveOverNetwork(Carryable item)
    {
        if (item == null) return;
        if (held != null && held != item) DropHeld();

        // It may be sitting in a bag on THIS machine - theirs or somebody
        // else's. PickUp would happily un-parent it and leave the bag still
        // counting it, so the pack would stay full of a crate that is now in
        // a pair of hands.
        foreach (var p in PlayerRegistry.All)
        {
            if (p == null) continue;
            var pack = p.GetComponent<PlayerBackpack>();
            if (pack != null && pack.Release(item)) break;
        }

        item.PickUp();
        held = item;
    }

    /// <summary>
    /// Let go without inheriting the carrier's motion. Used when a downed
    /// crewmate is revived in your arms: they should stand up where they are,
    /// not be thrown at whatever speed you happened to be walking.
    /// </summary>
    public void ForceDrop()
    {
        if (held == null) return;
        held.Drop(Vector3.zero);
        held = null;
    }

    // ====================================================================
    // PUTTING SOMETHING DOWN, AS OPPOSED TO LETTING GO OF IT
    //
    // DropHeld is a release: the item leaves your hands at your hands, keeping
    // whatever speed you were walking at, and physics sorts out the rest. That
    // is correct for a drop and wrong for a deliberate placement.
    //
    // The lift is why this needs to exist at all. The load gauge counts what is
    // physically in the car, so a crate that lands on its corner and rolls back
    // out through the doors is not a cosmetic problem - it is loot the crew
    // paid weight for and will not be paid for. Placement fixes that with three
    // things: flat (yaw only, so it cannot land on an edge), still
    // (Drop(Vector3.zero), so it does not inherit your walk), and somewhere
    // checked (an OverlapBox, so it is not being posted into a wall).
    //
    // Note the ORDER in PlaceHeld: moved, then announced, then dropped. A held
    // item's colliders are off - that is what lets you carry a crate through a
    // doorframe - so moving it first is free, and the colliders switch back on
    // at a spot already known to be empty. DropHeld does the opposite, and the
    // maxDepenetrationVelocity comment in Carryable is what that costs.
    // ====================================================================

    [Header("Putting it down")]
    [Tooltip("How far in front of your feet a placed item is set down, on top " +
             "of its own size and the width of your body. Small: this is " +
             "putting something down, not tossing it.")]
    public float placeReach = 0.15f;

    [Tooltip("How far down the placement looks for a floor. Anything more than " +
             "a step below you is not somewhere to put a crate, it is a hole.")]
    public float placeDrop = 2.5f;

    /// <summary>Q. Set the held thing down flat, still, and in a spot that has
    /// been checked - or fall back to a plain drop if there is no such spot,
    /// because a crate must never become impossible to put down.</summary>
    void PlaceHeld()
    {
        if (held == null) return;

        var item = held;

        if (!TryPlacement(item, out Vector3 pos, out Quaternion rot))
        {
            // Boxed into a corner, standing over a drop, or facing a wall from
            // 10cm. Letting go is worse than placing, and far better than the
            // key appearing to do nothing at all.
            DropHeld();
            return;
        }

        // Cleared before announcing, for the same reason DropHeld does it: on
        // the HOST a ServerRpc dispatches to itself, so DropClientRpc comes
        // straight back round and calls ForceDrop on this very object. If held
        // were still set, the echo would drop it a second time.
        held = null;

        item.transform.SetPositionAndRotation(pos, rot);

        // Announce reads the transform, and the transform is now the resting
        // place - so every machine puts it exactly here, rather than each
        // simulating a falling crate and arriving at its own answer.
        Announce(item, false);

        item.Drop(Vector3.zero);
    }

    static readonly Collider[] placeOverlap = new Collider[32];

    /// <summary>
    /// Where a placed item comes to rest, and how it is turned.
    ///
    /// Returns false when there is nowhere sensible, which the caller treats as
    /// "drop it instead" rather than as "do nothing".
    /// </summary>
    bool TryPlacement(Carryable item, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;

        // ---- FLAT, NOT HOWEVER IT WAS BEING CARRIED ----
        //
        // Yaw only. A crate carried tilted and released tilted lands on an edge
        // and rolls, and where it stops is then nobody's decision.
        rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        // Measured AT the placement rotation, because WorldBounds is a world
        // axis-aligned box: read it while the item is still tilted and the
        // extents describe a shape that is about to stop existing. Safe to turn
        // it here - it is kinematic with its colliders off, and it is being
        // placed this frame either way.
        Quaternion wasRot = item.transform.rotation;
        item.transform.rotation = rot;

        Bounds b = item.WorldBounds;
        Vector3 half = b.extents;

        // The pivot is not the middle of the mesh, so a desired CENTRE has to
        // be converted back into a TRANSFORM position before anything is moved.
        Vector3 pivotFromCentre = item.transform.position - b.center;

        item.transform.rotation = wasRot;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        // Clear of your own capsule, plus half the item, plus the reach. Read
        // off the collider rather than typed in, so the 0.30 -> 0.42 capsule
        // change from Phase 4 cannot leave a stale number sitting here.
        var capsule = GetComponent<CapsuleCollider>();
        float bodyRadius = capsule != null ? capsule.radius : 0.4f;
        float itemHalf = Mathf.Max(half.x, half.z);
        float reach = bodyRadius + itemHalf + placeReach;

        // Near is tried too, because the far spot is the one a wall takes away.
        // Standing in a doorway you should still be able to set a crate down at
        // your feet rather than be refused.
        float[] tries = { reach, bodyRadius + itemHalf + 0.02f };

        for (int i = 0; i < tries.Length; i++)
        {
            Vector3 over = transform.position + forward * tries[i];

            // Started above head height on the target column, so the probe
            // cannot begin inside the floor of a step you are standing on.
            if (!Physics.Raycast(over + Vector3.up * 1.2f, Vector3.down,
                                 out RaycastHit ground, placeDrop + 1.2f,
                                 ~0, QueryTriggerInteraction.Ignore))
                continue;

            // A skin, so it rests on the floor instead of starting the frame
            // fractionally inside it.
            Vector3 centre = new Vector3(over.x,
                                         ground.point.y + half.y + 0.02f,
                                         over.z);

            if (!Clear(centre, half, rot)) continue;

            pos = centre + pivotFromCentre;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Nothing solid in that space but me.
    ///
    /// The item itself never shows up here - its colliders are off while it is
    /// held - so the only thing worth filtering out is my own body. Buffer
    /// truncation is harmless in this direction: a full buffer can only mean
    /// the space is crowded, and crowded is already the answer that refuses.
    /// </summary>
    bool Clear(Vector3 centre, Vector3 half, Quaternion rot)
    {
        int n = Physics.OverlapBoxNonAlloc(centre, half * 0.95f, placeOverlap,
                                           rot, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            var c = placeOverlap[i];
            if (c == null) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            return false;
        }

        return true;
    }

    void DropHeld()
    {
        if (held == null) return;

        // TAKEN OUT OF MY HANDS FIRST, then announced, then dropped.
        //
        // On the HOST a ServerRpc is dispatched to itself, so DropClientRpc can
        // come back round and call ForceDrop on this very object - which sets
        // held to null underneath us. The next line would then be dropping a
        // null. Holding the item in a local and clearing the field before
        // announcing means the echo finds nothing to do and this method
        // finishes the job it started.
        var item = held;
        held = null;

        // Announced while it is still where I can see it. A moment later it is
        // a falling object and the position I would send is already stale.
        Announce(item, false);

        item.Drop(rb.linearVelocity);
    }

    public void ReceiveFromPack(Carryable item)
    {
        if (item == null || held != null) return;
        held = item;
    }

    void OnGUI()
    {
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        // MY HUD, not everyone's. Without this every body in the
        // scene draws its own copy on top of the same screen.
        if (!PlayerRegistry.IsLocalFor(this)) return;

        string prompt = null;
        Color colour = Color.white;

        if (held != null)
        {
            // A crewmate is not "Bottled_Water_Bulk (70kg, Massive)". The
            // numbers are identical and the sentence must not be: the whole
            // point of Step 6 is that the load gauge cannot tell the
            // difference and the crew can.
            prompt = held.IsPerson
                ? $"carrying a crewmate  ({held.Mass:0}kg)   -   TOO HEAVY TO JUMP" +
                  "\nE  put them down"
                : $"carrying {held.name}  ({held.Mass:0}kg, {held.Weight})" +
                  (held.AllowsJumping ? "" : "   -   TOO HEAVY TO JUMP OR PUSH") +
                  "\nE  drop it - counts toward the elevator's load once it is inside the car";

            colour = held.IsPerson
                ? new Color(1f, 0.45f, 0.4f)
                : held.AllowsJumping ? Color.white : new Color(1f, 0.6f, 0.25f);
        }
        else if (lookingAt != null)
        {
            prompt = lookingAt.IsPerson
                ? $"E  pick them up   ({lookingAt.Mass:0}kg - both hands)"
                : $"E  pick up {lookingAt.name}   ({lookingAt.Mass:0}kg, {lookingAt.Weight})";

            if (lookingAt.IsPerson) colour = new Color(1f, 0.45f, 0.4f);
        }

        if (prompt == null) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = colour;

        float w = 700f;
        GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f + 60f, w, 46),
                  prompt, style);
    }
}
