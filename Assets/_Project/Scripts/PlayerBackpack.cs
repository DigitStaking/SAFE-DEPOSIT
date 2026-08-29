// PlayerBackpack.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerBackpack.cs
//
// Convenience only — mass still counts on the winch.
// Keys 1 / 2 / 3 pull that slot into your hands.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBackpack : MonoBehaviour
{
    [Header("Capacity")]
    [Tooltip("Start at 2. Shop sells more slots.\n\n" +
             "FALLBACK ONLY. Once there is a player to ask, Capacity below " +
             "reads the Crew row instead, so this value is only used before " +
             "one exists.")]
    public int slots = 2;

    /// <summary>
    /// How many things fit, asked live.
    ///
    /// This used to be a field that RunManager pushed once at the start of a
    /// round. That was already fragile - buy a pack slot mid-round and the
    /// bag would not know until next round - and sprays made it wrong outright,
    /// because using one has to hand the space straight back for loot.
    ///
    /// Eighth time the same answer in this phase: ask the owner, every frame.
    /// The row is the truth; a copy of it is a bug waiting for a reason.
    /// </summary>
    public int Capacity
    {
        get
        {
            var owner = PlayerRegistry.OwnerOf(this);
            return owner != null ? Crew.Of(owner.Slot).LootSlots : slots;
        }
    }

    // ================================================================
    // SPRAYS FILL SLOTS. THEY DO NOT SHRINK THE BAG.
    //
    // The first version just reduced Capacity, so buying a spray made a slot
    // VANISH from the HUD. Reported straight away, and correctly: "it must
    // fill the spot not take it - i can see 2, an image of medicals, and
    // click on it".
    //
    // The bag is the same size it always was. A spray is a thing IN it, drawn
    // in a slot, occupying that slot the way a crate would. That is both what
    // a player expects and what actually communicates the trade - a shrinking
    // row of boxes reads as a bug, while a box with MED in it reads as a
    // decision you made at the shop.
    //
    // Sprays take the FIRST slots, so their number keys never move as loot
    // comes and goes. The medic learns "1 is my spray" and it stays true.
    // ================================================================

    /// <summary>Sprays this player carries, and therefore slots 0..n-1.</summary>
    public int SprayCount
    {
        get
        {
            var owner = PlayerRegistry.OwnerOf(this);
            return owner != null ? Crew.Of(owner.Slot).MedSprays : 0;
        }
    }

    /// <summary>Every slot, sprays and loot together. The bag's real size.</summary>
    public int TotalSlots
    {
        get
        {
            var owner = PlayerRegistry.OwnerOf(this);
            return owner != null ? Crew.Of(owner.Slot).BackpackSlots : slots;
        }
    }

    /// <summary>
    /// A spray is in hand, ready to use.
    ///
    /// Taking it out is a deliberate act, like withdrawing a crate. It means
    /// the R prompt only appears for somebody who has actually reached for
    /// one - so a crewmate with no sprays is never told to press a key that
    /// will do nothing.
    /// </summary>
    public bool SprayReady { get; private set; }

    public void PutSprayAway() => SprayReady = false;

    [Header("Placement")]
    public Transform backAnchor;
    public float itemSpacing = 0.22f;

    [Header("Visual pack")]
    public Vector3 packLocalPosition = new Vector3(0f, 1.15f, -0.32f);
    public Vector3 packLocalScale = new Vector3(0.38f, 0.48f, 0.22f);
    public Color packColor = new Color(0.18f, 0.18f, 0.2f);

    public int Count => items.Count;
    public int Slots => Capacity;
    public bool HasRoom => items.Count < Capacity;
    public int SelectedSlot { get; private set; } = -1;

    public float TotalMass
    {
        get
        {
            float sum = 0f;
            foreach (var i in items) if (i != null) sum += i.Mass;
            return sum;
        }
    }

    public int TotalValue
    {
        get
        {
            int sum = 0;
            foreach (var i in items) if (i != null) sum += i.value;
            return sum;
        }
    }

    readonly List<Carryable> items = new List<Carryable>();
    Rigidbody rb;
    PlayerCarry carry;
    PlayerHealth health;
    Transform packVisual;
    float lastSelectFlash;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carry = GetComponent<PlayerCarry>();
        health = GetComponent<PlayerHealth>();

        if (backAnchor == null)
        {
            var go = new GameObject("BackAnchor");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.25f, -0.28f);
            backAnchor = go.transform;
        }

        BuildPackVisual();
    }

    void BuildPackVisual()
        {
            // Real diver FBX already has a tank/pack silhouette. Do not slap a
            // gray cube on the back (shows up in third person as a black box).
            if (transform.Find("PlayerModel_FBX_VISUAL") != null)
            {
                packVisual = null;
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BackpackVisual";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = packLocalPosition;
            go.transform.localScale = packLocalScale;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null)
            {
                var mat = new Material(sh);
                mat.SetColor("_BaseColor", packColor);
                mat.SetFloat("_Smoothness", 0.15f);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            packVisual = go.transform;
            RefreshPackVisual();
        }

    void Update()
    {
        // Number keys 1-6 select / withdraw that slot.
        var kb = PlayerRegistry.KeysOf(this);
        if (kb == null) return;

        // Nothing comes out of the pack while you are on the floor. Same rule
        // as PlayerCarry: downed means no interaction of any kind.
        if (health != null && health.IsDowned) return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) UseSlot(0);
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) UseSlot(1);
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) UseSlot(2);
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) UseSlot(3);
        if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) UseSlot(4);
        if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) UseSlot(5);
    }

    /// <summary>Select slot; if filled and hands free, withdraw into hands.</summary>
    public void UseSlot(int index)
    {
        if (index < 0 || index >= TotalSlots) return;

        SelectedSlot = index;
        lastSelectFlash = Time.time;

        // A SPRAY SLOT. Taking it out readies it; pressing the same key again
        // puts it back, so the key is a toggle rather than a one-way door.
        if (index < SprayCount)
        {
            SprayReady = !SprayReady;
            return;
        }

        // Loot lives after the sprays, so the visible slot and the list index
        // are not the same number once somebody is carrying a spray.
        index -= SprayCount;

        if (index >= items.Count || items[index] == null) return;

        // Hands full — cannot withdraw.
        if (carry != null && carry.IsCarrying) return;

        var item = TakeAt(index);
        if (item == null) return;

        item.PickUp();
        // Hand off to PlayerCarry via public inject if available.
        if (carry != null) carry.ReceiveFromPack(item);
    }

    public bool TryStow(Carryable item)
    {
        if (item == null || !HasRoom || !item.CanStow) return false;

        item.Stow(backAnchor);
        items.Add(item);
        Reposition();
        RefreshPackVisual();
        return true;
    }

    public Carryable TakeLast()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null) { items.RemoveAt(i); continue; }
            return TakeAt(i);
        }
        return null;
    }

    /// <summary>
    /// Take one SPECIFIC item out, wherever it is in the bag.
    ///
    /// PHASE 4 STEP 6. TakeLast is what a player uses - they reach in and get
    /// whatever is on top. The network needs the other question: somebody
    /// pulled item 17 out of their pack, and this machine has to take THAT one
    /// out of its copy. Without it the item leaves the bag visually and the
    /// list still counts it, so the pack stays full of a crate that is now in
    /// somebody's hands.
    /// </summary>
    public bool Release(Carryable item)
    {
        if (item == null) return false;

        int i = items.IndexOf(item);
        if (i < 0) return false;

        TakeAt(i);
        return true;
    }

    /// <summary>
    /// Forget items that were just handed over at extraction.
    ///
    /// The objects are destroyed by RunManager a moment later, and a list of
    /// destroyed references is worse than an empty one: Count still includes
    /// them, so the bag reports itself full and refuses the next round's loot
    /// while showing empty slots.
    /// </summary>
    public void ClearSold(System.Collections.Generic.HashSet<Carryable> sold)
    {
        if (sold == null) return;

        for (int i = items.Count - 1; i >= 0; i--)
            if (items[i] == null || sold.Contains(items[i])) items.RemoveAt(i);

        Reposition();
        RefreshPackVisual();
    }

    public Carryable TakeAt(int index)
    {
        if (index < 0 || index >= items.Count) return null;
        var item = items[index];
        if (item == null)
        {
            items.RemoveAt(index);
            return null;
        }

        items.RemoveAt(index);
        item.Unstow();
        Reposition();
        RefreshPackVisual();
        return item;
    }

    public int DropAll()
    {
        int dropped = 0;
        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null) continue;
            items[i].transform.SetParent(null, true);
            items[i].Drop(velocity + Random.insideUnitSphere * 1.5f);
            dropped++;
        }

        items.Clear();
        RefreshPackVisual();
        return dropped;
    }

    void OnDropPack(InputValue value)
    {
        if (!value.isPressed) return;
        if (!PlayerRegistry.IsLocalFor(this)) return;

        // The one exception: the pack CAN come off while downed. If it could
        // not, the crew would be carrying your loot as well as you, and the
        // point of the load gauge is that they get to choose.
        DropAll();
    }

    void Reposition()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            items[i].transform.localPosition = new Vector3(0f, i * itemSpacing, 0f);
        }
    }

    void RefreshPackVisual()
    {
        if (packVisual == null) return;
        // Scale pack slightly with load so others can read your weight at a glance.
        float load = Capacity > 0 ? (float)items.Count / Capacity : 0f;
        packVisual.localScale = packLocalScale * (0.85f + 0.35f * load);
        packVisual.gameObject.SetActive(true);
    }

    public string SlotLabel(int i)
    {
        // Spray slots come first and are not in `items` at all, so the visible
        // index and the list index diverge the moment somebody carries one.
        if (i < SprayCount)
            return SprayReady ? "MED SPRAY - in hand" : "MED SPRAY";

        i -= SprayCount;
        if (i < 0 || i >= items.Count || items[i] == null) return "empty";
        // Value shown alongside the mass, because "the item came back with no
        // value" was reported and there was no way to see a value anywhere in
        // the pack UI to check it against. A number on screen turns the next
        // report into evidence.
        return $"{items[i].name} ({items[i].Mass:0}kg, ${items[i].value})";
    }

    void OnGUI()
    {
        // Don't draw gameplay chrome over the results/shop screen.
        if (!RunHudGate.ShouldDrawGameplayHud()) return;

        // MY HUD, not everyone's. Without this every body in the
        // scene draws its own copy on top of the same screen.
        if (!PlayerRegistry.IsLocalFor(this)) return;

        const float box = 40f;
        const float gap = 8f;

        int cap = TotalSlots;
        int sprays = SprayCount;
        float totalWidth = cap * box + (cap - 1) * gap;
        float x = Screen.width - totalWidth - 28f;
        float y = Screen.height - 100f;

        for (int i = 0; i < cap; i++)
        {
            var r = new Rect(x + i * (box + gap), y, box, box);

            bool isSpray = i < sprays;
            int lootIndex = i - sprays;
            bool filled = isSpray ||
                          (lootIndex >= 0 && lootIndex < items.Count &&
                           items[lootIndex] != null);
            bool selected = SelectedSlot == i && Time.time - lastSelectFlash < 0.6f;

            // Green for a spray, and brighter still when it is in hand, so the
            // medic can tell at a glance whether they are ready to use it.
            GUI.color = selected
                ? new Color(0.45f, 0.85f, 1f, 0.95f)
                : isSpray
                    ? (SprayReady ? new Color(0.4f, 1f, 0.5f, 0.95f)
                                  : new Color(0.25f, 0.7f, 0.35f, 0.9f))
                    : (filled
                        ? new Color(0.85f, 0.55f, 0.15f, 0.92f)
                        : new Color(1f, 1f, 1f, 0.18f));
            GUI.Box(r, GUIContent.none);

            if (isSpray)
            {
                var med = new GUIStyle(GUI.skin.label)
                { fontSize = 11, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                med.normal.textColor = Color.white;
                GUI.color = Color.white;
                GUI.Label(r, "MED", med);
            }

            var num = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            num.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(r, (i + 1).ToString(), num);
        }

        GUI.color = Color.white;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleRight
        };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.55f);

        string tip = items.Count > 0
            ? $"{TotalMass:0}kg pack   1-{TotalSlots} withdraw   G dump"
            : $"pack {items.Count}/{Capacity} loot" +
              (SprayCount > 0 ? $"  +{SprayCount} MED" : "") +
              $"   keys 1-{TotalSlots}";

        GUI.Label(new Rect(x - 280f, y + 10f, 270f, 20f), tip, style);

        if (SelectedSlot >= 0 && SelectedSlot < TotalSlots && Time.time - lastSelectFlash < 1.5f)
        {
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(0.7f, 0.9f, 1f, 0.9f);
            GUI.Label(new Rect((Screen.width - 420f) * 0.5f, y - 28f, 420f, 20f),
                $"slot {SelectedSlot + 1}: {SlotLabel(SelectedSlot)}", style);
        }
    }
}
