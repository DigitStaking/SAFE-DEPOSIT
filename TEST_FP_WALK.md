# SAFE DEPOSIT — First person view + walk check

## Why you saw full body / helmet
The camera was sitting **inside the helmet mesh**, so looking down showed a giant orange head and white lamp.

## Fix
New script: `LocalFirstPersonBodyCull`
- Hides helmet / head / glass / lamp meshes in first person
- Keeps torso / arms / legs visible (PEAK shared-body style)

Also:
- Camera eye pushed slightly forward
- Stronger walk bob so motion is easier to feel

---

## How to see if the character can walk

### Method A — first person (normal)
1. Press Play
2. Walk with **WASD** on the ground
3. You should feel:
   - camera head-bob
   - body slight bob/sway
4. Look slightly down — arms/legs should move a bit (not a blocked helmet view)

### Method B — third person check (best)
1. Press Play
2. Press **V**
3. Camera pulls behind the player
4. Walk with WASD
5. You should see the full diver bob/sway while walking
6. Press **V** again to return to first person

### Method C — Scene view while playing
1. Play mode
2. Click the **Scene** tab
3. Focus the Player
4. Watch the model walk from outside

---

## If helmet still fills the view
1. Select Player
2. Find `LocalFirstPersonBodyCull`
3. Add more hide words if needed, e.g. `helmet`, `glass`, `light`
4. Or re-run:
   `SAFE DEPOSIT → Player → Setup Player FBX Prefab`

---

## Hands note
Seeing **hands/arms** in first person is intended (shared body, no separate viewmodel).
Seeing **helmet interior** is not — that should now be hidden.
