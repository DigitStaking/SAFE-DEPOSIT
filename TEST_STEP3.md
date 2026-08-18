# SAFE DEPOSIT — TEST STEP 3

Latest playtest checklist for tether, messages, atmosphere reload, and room seal timing.

Open scene:

`Assets/_Project/Scenes/Prototype.unity`

Press **Play**.

---

## 1. T reel / no launch

1. Jump from the main rope so tether becomes around **10m**.
2. Do **not** press anything.

Expected:
- Character does **not** auto-go up.
- Rope length stays out.

3. Hold **T**.

Expected:
- Character reels up **slowly**, not instantly.
- Message shows only while actually reeling.

4. Release **T**.

Expected:
- Character does **not** jump / launch upward.
- Character should settle naturally.

---

## 2. Space locked on long line

1. Stay airborne with long tether, around **10m**.
2. Press **Space** many times.

Expected:
- Space does **not** push/jump the character.
- Only useful recovery is holding **T**.
- Message should be like: `HOLD T to reel in ... Space locked`.

---

## 3. Rope messages

### In air

1. Hang in air with tether around **10m**.

Expected:
- Do **not** show `END OF LINE` / “you can move more”.
- Show only the **T to reel in** message.

### On ground

1. Stand on the ground.
2. Walk away until rope reaches max distance.

Expected:
- `END OF LINE` can show here.
- This message should be **ground only**.

---

## 4. Walking at 10m / vibration

1. On ground, walk away until tether reaches max length.

Expected:
- Body stops smoothly.
- No heavy vibration.
- Rope should not yank/pull the player backward hard.

---

## 5. Start Over / scene darkness

1. Fail the run or use start over.
2. Click **start over**.

Expected:
- Scene stays dark/foggy.
- No bright/blue/washed-out colors after reload.
- Headlamp/fog still active.

---

## 6. Room seal timing

Default room seal timer is **600s / 10 minutes**.

For faster testing:

1. Select **RunManager** in the scene.
2. Set **Room Charge Time** to `30` or `60`.
3. Press Play.

### Leave before first timer finishes

Expected:
- Only **1 room** seals: the currently charged room seals when you leave.

### Wait until first timer finishes, then leave after second timer starts

Expected:
- **2 rooms** sealed total:
  - 1 room sealed mid-run when timer finished.
  - 1 currently charged room sealed when you left.

### Wait until two timers finish, then leave after third timer starts

Expected:
- **3 rooms** sealed total.

Important rule:
- Every completed timer seals one room.
- Leaving/extracting seals the currently charged room too.

---

## 7. Backpack slots

1. Pick up small loot.

Expected:
- Backpack shows `pack 1/2` or similar.

2. Press **1** or **2**.

Expected:
- Selected slot item comes back into hand.

3. Press **G**.

Expected:
- Backpack dumps items.

---

## Report back

Tell Hermes exactly what failed, for example:

- “T still launches me when I release.”
- “Space still works at 10m.”
- “Start over is still bright.”
- “Room seal count is wrong.”
- “Walking at 10m still vibrates.”
