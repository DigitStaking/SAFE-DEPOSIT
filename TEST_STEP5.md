# SAFE DEPOSIT — TEST STEP 5

Latest checklist: brighter smoky atmosphere + real-FBX character loader.

Open:

`Assets/_Project/Scenes/Prototype.unity`

Press **Play**.

---

## 1. Scene is less dark

Expected:
- You can see walls/room shapes without the screen being pure black.
- It is still moody/dangerous, but not unreadable.
- Headlamp still matters.

If still too dark:
- Select `SceneAtmosphere` if present.
- Increase `Ambient Intensity` toward `1.0`.
- Lower `Fog Density` toward `0.018`.

---

## 2. Smoke / blurry haze

Expected:
- Soft drifting smoke/dust appears in the shaft.
- It should make the place feel hazy/blurry, not like hard white clouds.
- It should not fully block vision.

If too much smoke:
- On `SceneAtmosphere`, lower `Smoke Columns` or `Smoke Color` alpha.

---

## 3. Character visual now

Expected:
- The old huge block/cylinder body is reduced.
- Arms are slimmer and should not block the camera as much.
- Temporary fallback still exists only because we do not yet have the real FBX.

---

## 4. Real diver FBX import path

To use a real character model, place this file:

`Assets/Resources/Characters/PrototypeDiver.fbx`

Then press Play.

Expected:
- `PrototypeDiverVisuals` loads the FBX automatically using:
  `Resources.Load<GameObject>("Characters/PrototypeDiver")`
- FBX appears under the player visual root.
- Runtime script removes any colliders/Rigidbodies inside the FBX so player physics stays clean.

---

## 5. Regression quick check

Also check:
- Shift/Ctrl does not work on ground.
- Shift/Ctrl only works while hanging with short tether.
- T reel still works.
- Start over stays less-dark/smoky.
