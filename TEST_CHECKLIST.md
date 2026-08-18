# SAFE DEPOSIT — Play mode test checklist (Step 1)

Open: `Assets/_Project/Scenes/Prototype.unity`  
If the shaft is missing: menu **SAFE DEPOSIT → Build Graybox Shaft**

---

## Before Play
1. Console should be clean after scripts compile (no red errors).
2. Hierarchy has: Player, MainRope, RunManager (names may vary), camera tagged **MainCamera**.
3. After this update, on Play you should auto-get **fog + headlamp** (no manual setup).

---

## A. Atmosphere (Phase A1)
- [ ] Scene is dark with fog, not bright default Unity gray
- [ ] Headlamp cone lights walls in front of you
- [ ] Rope reads **yellow**
- [ ] Loot cubes read **amber/orange**
- [ ] Walls are cool dark gray (not shiny plastic)

## B. Movement / rope
- [ ] WASD + mouse look
- [ ] Space jumps on ground; on rope Space leaps (still clipped)
- [ ] Ctrl descend / Shift climb while clipped
- [ ] T reels in **only in air**
- [ ] F cuts tether → slower, line hidden; touch rope + F or auto after grace to reclip
- [ ] Long line in air → cannot steer; HUD says hold T
- [ ] Q near rope pins it; Q again releases (smooth, not teleport)

## C. Loot
- [ ] E pick up small (Cash) → goes to backpack slots (bottom right)
- [ ] E pick up Heavy/Massive → hands; cannot jump/climb while holding Heavy+
- [ ] Near rope + E clips cargo; cargo hangs spaced on line
- [ ] G dumps backpack
- [ ] Load kg rises on HUD; overload → countdown → anchor tears out

## D. Run / campaign
- [ ] Quota + timer visible top-left
- [ ] Go deep past ~5m → “extraction armed”
- [ ] Climb to top on rope → results / shop (solo)
- [ ] Shop: buy rope, go back down reloads scene
- [ ] **Critical:** after “go back down”, scene reloads Prototype (not empty SampleScene)

## E. Escape
- [ ] Esc frees mouse; click recaptures

---

## If something fails
Write down: what you pressed, what you expected, what happened, and any Console error.  
Then we fix that **one** thing before Phase A2 (loot silhouettes).

## Known OK for now
- Solo only (no 2nd player yet)
- Cube loot, cube arms
- OnGUI prototype HUD
- Massive can still be hand-carried (Collector later)
