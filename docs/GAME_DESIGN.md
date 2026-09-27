# SAFE DEPOSIT — Design Map

**This file is a map, not a design document.** The real design lives in the
Markdown files at the repository root and they are far more detailed than
anything that belongs here. Restating them would create a second source of
truth, which is the exact failure this project has already been bitten by —
see `DECISIONS.md`.

Use this to find the right document fast, and to know how far to trust it.

---

## Where each subject is specified

| Subject | Document | Trust |
|---|---|---|
| World, story, crew, the three cargoes, art direction | `GAME_DESIGN.md` | current, **except §6 rope, §7 Loot Collector, §11 shop — all superseded** |
| Every number: rooms, rounds, loot tiers, mass, shop prices, survivors, rescue | `ECONOMY_AND_CAMPAIGN.md` | current and authoritative |
| The 25 puzzles, lock/key/modifier kit, placement rules | `PUZZLES.md` | current; **0 of it is built** |
| The elevator, and its 12-step build order | `ELEVATOR_SPEC.md` | current; all 12 steps done |
| The seven demo inhabitants, and the two new verbs | `INHABITANTS.md` | current |
| Phase-by-phase map of the whole project | `ROADMAP.md` | current for phases; **stale on transport and voice** |
| Schedule, dates, cut list | `DEMO_PLAN.md` | current |
| Animation system, clip list, two-layer Animator | `ANIMATIONS.md` | current |
| Per-phase build orders | `PHASE2/3/4/5_SPEC.md` | current for their phase |
| Document index and working rules | `MASTER.md` | **inventory is stale (14 Aug)**; rules and doc-status still good |
| The rope traversal system | `ROPE_AND_PLATFORM.md` | **DEPRECATED** — replaced by the elevator |
| Older schedule | `BUILD_PLAN.md` | **DEPRECATED** — replaced by `DEMO_PLAN.md` |

---

## The floor shape, as reconciled in `MASTER.md` §3

```
ELEVATOR ──bridge──► DOOR ──► ROOM COMPLEX (2–3 connected spaces)
```

- **One door per floor.** The bridge extends to it.
- **Which of the four sides** the door is on varies by floor, so arriving means
  orienting yourself. IMPLEMENTED — `Grayboxbuilder` rotates each level.
- **One floor in four** has a sealed sub-room behind a puzzle. PLANNED.

**Note a live tension:** `MASTER.md` describes a complex of 2–3 spaces. The
Phase 5 generator being built now produces **7–14 rooms** per floor with
branching. That is a deliberate evolution recorded in `PHASE5_SPEC.md`, not an
error — but `MASTER.md` §3 has not been updated to match, and the two will
confuse anyone reading only one.

---

## The three cargoes — the mechanical heart

Implemented as weight classes on `Carryable`, derived from rigidbody mass and
never set by hand:

| Class | Mass | Effect |
|---|---|---|
| Small | ≤ 8 kg | one hand, can jump, can go in the backpack |
| Heavy | ≤ 60 kg | two hands, no jumping, 0.7× speed |
| Massive | > 60 kg | 0.45× speed; a downed person is 70 kg and therefore Massive |

These thresholds were set in Phase 2 for speed and jumping, and later turned
out to land exactly right for throwing — a 1-door detail worth knowing, because
**four separate systems now depend on them.** Changing them is not a local edit.

---

## What is designed but not built

Every one of these has a written specification and no code:

- **All 25 puzzles** and the lock/key/modifier kit — `PUZZLES.md`
- **All seven inhabitants** — `INHABITANTS.md`. Named: the fat man, the seller,
  the cannibal, the thief, the eyeless, **the foreman** (also called the
  shapeshifter / mimic — he wears a crewmate's colour), the passenger
- **Traps**
- **The shop UI** — the prices exist in the economy doc and in `Campaign`
- **Doors, keys and locked states**
- **Survivors** as placeable content
