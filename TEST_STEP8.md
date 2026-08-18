# SAFE DEPOSIT — TEST STEP 8

Facing fix + real loot props + grab/walk procedural animation.

---

## 1. Re-setup player facing

Menu:

`SAFE DEPOSIT → Player → Setup Player FBX Prefab`

Expected:
- Character faces the same way you walk/look.
- Old gray box body/arms hidden.
- `PlayerProceduralAnim` on root.

If still backwards:
1. Select Player root.
2. Find `PlayerProceduralAnim`.
3. Set `Visual Yaw Offset` to `180`.
   Or use context menu **Flip Visual Yaw 180**.

---

## 2. Rebuild graybox loot as real props

Menu:

`SAFE DEPOSIT → Props → Make Loot Prefabs`  
(only if prefabs look wrong)

Then:

`SAFE DEPOSIT → Build Graybox Shaft`

Expected loot in each room:
- **Crate** (small, ~6kg) — backpackable
- **Filing Cabinet** (heavy, ~34kg) — two hands, no climb
- **Vending Machine** (massive, ~140kg) — hug carry, very slow

No orange cubes if prefabs exist.

---

## 3. Grab animation per item

1. Pick up **crate**  
   Expected: one-hand-ish carry pose, item closer.
2. Pick up **filing cabinet**  
   Expected: two-hand front carry, item lower/farther.
3. Pick up **vending machine**  
   Expected: wide hug pose, item farther.

---

## 4. Walk animation (ground)

1. Walk on ground with empty hands.

Expected:
- Slight body bob on FBX visual.
- Arms gently swing opposite each other.

Rope/air animations are later.

---

## 5. Notes

This is **procedural** motion (code), not full FBX Animator clips yet.
When you add real walk/grab clips, we plug them into an Animator and keep the same pose API.
