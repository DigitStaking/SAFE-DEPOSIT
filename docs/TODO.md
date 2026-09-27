# SAFE DEPOSIT — TODO

Queued work, from the design docs and the audit. **This file does not invent
priorities** — where the roadmap has an order, that order is kept.

---

## Immediate — unblock Phase 5

1. **Compile the project.** The Phase 5 generator has never been through a
   compiler in its current form.
2. **`Tools → Rooms → Build Room Set`** — the room prefabs must be rebuilt from
   the current builder.
3. **`Tools → Rooms → Validate Twenty Seeds`** — the fast rules check.
4. **`Tools → Rooms → Preview Ten Floors`** — the look-at-it check.
5. **Put `FloorDirector` on a scene object**, or the runtime floor lifecycle
   does nothing at all (silently).

## Then — finish Phase 5

| # | Step | State |
|---|---|---|
| 4 | Room module contract | written, unverified |
| 5 | The room set | written, prefabs built |
| 6 | The floor generator | **the actual work** |
| 7 | Doors, keys, locked states | not started |
| 8 | Survivors | not started |

**Done when:** ten generated floors a stranger can navigate without a map, and
every door on every floor is a real connection.

## Deferred inside Phase 5

- **Locked / sealed floor lifecycle.** Requested 5 Sep: do not generate floors
  the crew cannot reach; show a cheap blocker; destroy a sealed floor's
  geometry and keep only its seed and state. **Design discussed, not built.**
  The existing pieces to reuse: `Campaign.CableLength` /
  `Campaign.FloorsReachable`, the sealed-room mask in `Campaign`, and
  `RoomSeal`.
- **`throwRangeLight` for the noise distraction.** Throws are capped at 4 m,
  which cannot send a cannibal down a corridor. `INHABITANTS.md` calls that
  *"the first real counterplay that is not hiding"*. Revisit when the cannibal
  is built — the dial only needs raising for Small items.

## Phase 6 and beyond — nothing started

- **Puzzles.** 25 specified, 5 Tier-1 needed for the demo. Zero files.
- **Traps.** 4 needed. Zero files.
- **The seven inhabitants.** Zero files. Order from `INHABITANTS.md` Part 5:
  fat man + survivors → 5; eyeless, passenger, cannibal, survivors-behind-
  puzzles → 6; thief, seller, gun, foreman → 7.
- **Shop UI** — Phase 7.
- **Full audio pass** — Phase 8.
- **Content to 20 floors** — Phase 9.

---

## Housekeeping the audit recommends

Each of these is small, and **none were done during the audit.**

- [ ] Update `ROADMAP.md` lines ~157 and ~177 — they still name Facepunch and
      Dissonance. This is the single highest-value doc fix: it is the file the
      project treats as the map.
- [ ] Update `MASTER.md` §4 inventory, or mark the file as historical.
- [ ] Reconcile `MASTER.md` §3 (2–3 spaces) with Phase 5 (7–14 rooms).
- [ ] Delete `Assets/_Project/Prefabs/Rooms/` once `Resources/Rooms` is
      confirmed working.
- [ ] Commit or delete the untracked `Resources/Rooms/` and
      `FloorDirector.cs.meta`.
- [ ] Delete `Assets/_Recovery/` (3.4 MB, unreferenced) after confirming
      nothing is wanted from it.
- [ ] Remove dead input actions: `ReelIn`, `ToggleTether`, `Descend`, `Ascend`
      — Shift and Ctrl especially, which players will press expecting
      sprint/crouch.
- [ ] Replace `steam_appid.txt` (currently 480, Valve's test app) before any
      public build.
- [ ] Delete `Assets/TutorialInfo/` and `Assets/Scenes/SampleScene.unity`.

## Engineering investments worth scheduling

- [ ] **An EditMode test assembly** around pure logic — `FloorGraph`,
      `Campaign` formulas, `Carryable` thresholds. Highest value per hour of
      anything in this list.
- [ ] **A plan for replacing IMGUI**, timed with Phase 7's shop or Phase 8's
      polish. Leaving it later means porting every readout at once.
- [ ] **A save system**, or an explicit decision that the demo does not need
      one. Right now it is neither built nor ruled out.
