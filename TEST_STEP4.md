# SAFE DEPOSIT — TEST STEP 4

Latest checklist: rope depth controls + prototype character visual.

Open:

`Assets/_Project/Scenes/Prototype.unity`

Press **Play**.

---

## 1. Shift / Ctrl on ground

1. Stand on the ground while clipped to the main rope.
2. Press **Shift** and **Ctrl**.

Expected:
- Rope depth does **not** move up/down.
- Tether should not become a weird horizontal line because of Shift/Ctrl.
- You may see: `climb controls only work while hanging`.

---

## 2. Shift / Ctrl at 10m tether

1. Jump/drop so tether is around **10m**.
2. While airborne, press **Shift** or **Ctrl**.

Expected:
- Depth does **not** move.
- Message says to reel in first, e.g. `reel in to 2.5m before climbing`.

---

## 3. Shift / Ctrl at short tether

1. Hold **T** until tether is around **2.5m**.
2. While airborne/hanging, press **Shift** and **Ctrl**.

Expected:
- Now depth movement works.
- Shift climbs / Ctrl descends as before.

---

## 4. Character visual

Expected at Play start:
- Player has an orange PEAK-style diver body.
- Visible black gloves/boots.
- Harness / chest clip / oxygen tank on body.
- First-person arms are visible and move with carry/climb poses.
- Helmet should not block the camera.

Notes:
- This is runtime prototype art made from primitives, not final FBX.
- Final version should be a proper rigged low-poly diver model.

---

## 5. Regression quick check

Also retest quickly:
- Hold **T** in air: reels slower.
- Release **T**: no launch.
- Space at long tether: locked.
- Start over: scene stays dark.
