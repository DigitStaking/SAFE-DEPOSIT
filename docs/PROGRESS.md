# SAFE DEPOSIT — Progress

From `ROADMAP.md` (the current phase map) cross-checked against the code.
Audited 9 Sep 2026.

---

## Phase board

```
  PHASE 1  ████████████  the elevator ...................... DONE  12/12
  PHASE 2  ████████████  mass, health, downed .............. DONE   8/8
  PHASE 3  ████████████  de-single-player .................. DONE   7/7
  PHASE 4  ████████████  netcode + PROXIMITY VOICE ......... DONE  11/11
  PHASE 5  ████░░░░░░░░  the room kit .............. IN PROGRESS   3/8
  PHASE 6  ░░░░░░░░░░░░  puzzles and traps
  PHASE 7  ░░░░░░░░░░░░  economy and shop
  PHASE 8  ░░░░░░░░░░░░  polish + FULL AUDIO PASS
  PHASE 9  ░░░░░░░░░░░░  content to 20 floors
  PHASE 10 ░░░░░░░░░░░░  ship it
```

**Target: submit 31 May 2027 for Steam Next Fest, June 2027.**

---

## Finished phases

**Phase 1 — The elevator** (12/12, closed 19 Aug 2026). Rope deleted; car,
movement, dashboard, bridge, deck, price scanner, return-to-surface, graybox
rebuild, economy retune. A full round plays start to finish.

**Phase 2 — Mass, health, downed** (8/8). Health with no regeneration, downed
with bleed-out, **a downed player is a `Carryable`**, Lost state, rescue
contracts. Steps 7 and 9 were moved into Phase 4.

**Phase 3 — De-single-player** (7/7). `Camera.main` and `FindObjectsByType`
removed from the hot paths; `PlayerRegistry` and `SceneRefs` introduced;
per-player input, HUD and audio. The game still ran solo throughout.

**Phase 4 — Netcode and proximity voice** (11/11, written 30 Aug, **signed off
5 Sep 2026 after testing with a crew**). NGO 2.13.1, hand-written Steam
transport, lobby, replicated campaign/crew/loot/elevator, Steam Voice with
occlusion, walkie-talkie.

Worth preserving: this phase distinguished **"written and compiles"** from
**"verified"**, kept a table of the five done-whens that had never been seen
working with two people, and did not close until they were run. That table is
still in `ROADMAP.md`.

---

## Phase 5 — the room kit (current)

Eight steps. `PHASE5_SPEC.md`.

| # | Step | Status |
|---|---|---|
| 1 | Q — put it down | **DONE** |
| 2 | Q — hold to throw | **DONE** |
| 3 | Make the shove reliable | **DONE** |
| 4 | Room module contract (sockets) | written, **not verified** |
| 5 | The room set | written, prefabs built |
| 6 | The floor generator | written, **never successfully run** |
| 7 | Doors, keys, locked states | not started |
| 8 | Survivors | not started |

The ordering is deliberate: `INHABITANTS.md` puts the verbs first because four
of the seven demo inhabitants have an answer that is "push it", and building
creatures before the verb means building them twice.

**Step 6 is where the work actually is.** It has been rewritten several times
in a single day (5 Sep) as the requirements sharpened — from a spine, to a
tree, to a door-connectivity graph with backtracking and validation. The most
recent version has **not been compiled or run**.

**Done when:** ten generated floors a stranger can navigate without a map, and
every door on every floor is a real connection.

---

## Not started

- **Phase 6 — puzzles and traps.** `PUZZLES.md` specifies 25 puzzles and the
  lock/key/modifier kit; the demo needs 5 Tier-1. **Zero files exist.**
- **Phase 6 — the inhabitants.** All seven specified in `INHABITANTS.md`.
  Zero files.
- **Phase 7 — economy and shop.** Formulas live in `Campaign`; the shop has no
  interface.
- **Phase 8 — polish and the full audio pass.**
- **Phase 9 — content to 20 floors.**
- **Phase 10 — ship.**

---

## Owed from earlier phases

- **Phase 2's 25-puzzle redesign** is referenced as still outstanding.
- **First-person viewmodel has no carry grip** — parked deliberately.
- **The elbow/IK-hint system** — controls exist, `useElbowHint` is off on all
  items, section labelled "(parked)".
- **Emotes** — parked.
