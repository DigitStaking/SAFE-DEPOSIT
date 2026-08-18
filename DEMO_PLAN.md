# SAFE DEPOSIT — Demo Plan

**Target: Steam Next Fest, June 2027. Submission deadline 31 May 2027.**
A game can only ever appear in **one** Next Fest, so this is the only shot.

Today: **18 Aug 2026. Working time: 9.5 months.**

> **This document was rewritten on 18 Aug 2026, after the elevator decision.**
> The previous version was built around a five-week Verlet rope rewrite that no
> longer exists. Where the old plan said *"never cut the rope — it's the game,"*
> the elevator now carries that weight. See `ELEVATOR_SPEC.md`.

---

# WHAT THE DEMO IS

**We build the demo first. Nothing in the full game gets built until the demo
ships.**

| | Demo | Full game |
|---|---|---|
| Floors | **20** (one room complex each) | 100 |
| Rounds | **10** (~2.5 h) | 50 |
| Puzzles | **5** (Tier 1 only) | 25 |
| Survivors | 2 required, 3 placed | 9 of 11 |
| Documents | 1 required, 2 placed | 5 of 7 |
| Players | **3** (4 if netcode is comfortable) | 4 |
| Father | not present | deepest room |
| Threats | none — traps and collapse only | rival crew, starving one, etc. |

**Scope discipline:** the demo shows the *loop*, not the *content*. Five Tier-1
puzzles, one trap type per floor, no threats. A demo that shows your best ideas
has nothing left to sell.

---

# WHAT EXISTS TODAY — verified against the code, 18 Aug 2026

| Built | Lines | Notes |
|---|---|---|
| Graybox shaft generator | 329 | 5 floors, doors, loot cubes |
| FP camera + motor | 523 | Rigidbody + acceleration budget |
| Main rope (analytic) | 625 | **to be deleted** |
| Player tether | 988 | **to be deleted** |
| Rope hook / pin | 308 | **to be deleted** |
| Carry + weight + backpack | 802 | Small / Heavy / Massive |
| Run loop | 692 | quota, extraction, collapse timer |
| Campaign persistence | 132 | money, rope, destroyed rooms |
| Animation + hand IK | 1,395 | two-layer Animator, avatar mask |
| Atmosphere / smoke / shafts | 650 | |
| Head cull / skin / headlamp | 581 | |
| Editor tooling | 2,319 | graybox, props, animator builder |

**~6,260 runtime + 2,320 editor lines.** A working single-player vertical slice.

## What does not exist at all

Elevator (6 files) · health · damage · downed · Lost · rescue ·
**puzzles — 0 files** · **traps — 0 files** · survivors · documents · shop UI ·
doors · keys · room content · leader/voting · **netcode — the entire co-op game**

## Repo health — resolved 18 Aug 2026

`Untitled.glb`, a 942 MB unreferenced export, was committed and sat above
GitHub's 100 MB per-file hard limit. It silently blocked every push, so the
whole project existed on one disk. Removed from history; project is now on
GitHub and tagged `v0.1-rope-era`.

**Still worth cleaning:** `Player.glb` (37 MB, unused — `Player.fbx` is the one
wired into the prefab) and `Assets/_Recovery/` (two orphan scenes, ~18k lines).

---

# THE SCHEDULE

Eleven blocks. Each has a **definition of done** you can argue about, because a
milestone you can't test is a milestone you'll slip.

---

## BLOCK 0 · Lock down · **18–23 Aug 2026** (1 week)

- [x] **Commit everything and push.** Done 18 Aug — the repo is backed up
- [x] Tag `v0.1-rope-era`
- [ ] First Windows build; play it outside the editor
- [ ] **Steam page live** — wishlists compound from day one, and the page is
      required before Next Fest registration anyway

**Done when:** a stranger can wishlist the game.

---

## BLOCK 1 · The elevator · **24 Aug – 20 Sep** (4 weeks)

`ELEVATOR_SPEC.md` steps 2–12. The traversal layer and the social space, in one
object. **One step per session.**

- [ ] Step 2 — delete the six rope files (~2,470 lines)
- [ ] Steps 3–4 — the car prefab, then movement
- [ ] Steps 5–6 — the dashboard: F to zoom, UP/DOWN, then numeric entry + GO
- [ ] Step 7 — the bridge, with the 5-second retract countdown
- [ ] Step 8 — cargo deck and the load gauge
- [ ] Step 9 — the price scanner
- [ ] Step 10 — return to surface; `RunManager` extraction rewritten
- [ ] Step 11 — graybox rebuild, one room complex per floor
- [ ] Step 12 — economy retune (the constants table below)

**Done when:** a full round can be played start to finish, and the bridge
countdown makes you nervous.

---

## BLOCK 2 · Mass, health, downed · **21 Sep – 18 Oct** (4 weeks)

Four weeks, not five — no cargo bands and no Traverse to build.

- [ ] Capacity: 550 kg base, players 70 kg each, +50 kg upgrades
- [ ] **Player mass counted against the limit** — see gap #3 below
- [ ] Health, no regeneration, visible injury states
- [ ] Downed + bleed-out; med spray revive; **downed player is a `Carryable`**
- [ ] Lost state + rescue contract with banded outcomes
- [ ] **Cable fray** trap + patch kit

**Done when:** three players plus a full haul plus a 140 kg survivor genuinely
cannot all go up, and the gauge shows you why.

---

## BLOCK 3 · De-single-player · **19 Oct – 1 Nov** (2 weeks)

Not netcode. Removing assumptions while the game still runs solo.

- [ ] `Camera.main` — **9 files** — each player owns its camera
- [ ] `FindObjectsByType` — **9 files** — replace with a player registry
- [ ] `Campaign` stops being `static`
- [ ] Input, HUD, audio become per-player

**Done when:** the game plays identically and nothing references a global player.

---

## BLOCK 4 · Netcode · **2 Nov – 20 Dec** (7 weeks) ★ biggest unknown

Seven weeks, down from ten. **A moving platform is a position and a state.** A
32-node simulated rope replicated at 20 Hz was the hardest thing in the old
plan, and deleting it is the single strongest argument for the elevator.

- [ ] Netcode for GameObjects + Steam transport, host-authoritative
- [ ] Elevator state replicated: floor, moving, doors, bridge, load
- [ ] Local prediction for your own body only
- [ ] Players riding a moving platform stay in sync
- [ ] Downed / revive / Lost replicated
- [ ] Shared money, leader, **Change Leader** vote
- [ ] **Departure vote** — everyone aboard; name whoever isn't

**Done when:** three players ride together, one can carry another out, and the
departure vote correctly names the person still in a room. **Two players first.**

---

## BLOCK 5 · The room kit · **21 Dec – 17 Jan 2027** (4 weeks)

- [ ] Landing → main → side → back, the fixed 3-sub-room shape
- [ ] 6 room modules with tagged sockets (`LootAnchor`, `LockAnchor`,
      `HazardAnchor`, `SurvivorAnchor`)
- [ ] Floor generator arranging modules
- [ ] **The door on a different side per floor**, so arriving means orienting
- [ ] Doors, keys, locked states

**Done when:** ten generated floors that a stranger can navigate without a map.

---

## BLOCK 6 · Puzzles & traps · **18 Jan – 14 Feb** (4 weeks)

- [ ] The lock/key/modifier kit: 12 locks, 8 keys, 8 modifiers as
      ScriptableObjects
- [ ] **The 5 Tier-1 puzzles**
- [ ] 4 traps — floor collapse, gas, lockdown, cable fray
- [ ] **Every survivor behind a puzzle**

**Done when:** puzzle #6 can be authored in five minutes with no new code.

---

## BLOCK 7 · The economy & shop · **15 Feb – 7 Mar** (3 weeks)

- [ ] All formulas from `ECONOMY_AND_CAMPAIGN.md`
- [ ] Budget spawner — always ~1.4× what you can carry
- [ ] Shop UI — leader spends, assigns items, everyone sees what was bought
- [ ] ~20 of the ~35 shop items (demo subset)
- [ ] Survivor markers, screams, personal timers
- [ ] Mafia demand + results screen + speed bonus + rooms-lost report

**Done when:** ten rounds play end to end and the money is always tight.

---

## BLOCK 8 · Vertical slice polish · **8–28 Mar** (3 weeks)

**One floor finished to shippable quality.** Final art, final audio, no
placeholders. This is the milestone that tells you the truth.

- [ ] **First-person arms.** Decided 18 Aug 2026 and deferred to here.
      `FirstPersonHands` currently uses IK to drag the character's own hands
      to a point in front of the camera. That works for the person looking
      through it and looks wrong to everybody else — one skeleton cannot be
      posed for two viewpoints at once, and no weight or offset fixes it.
      We Were Here Together, the reference for this game, uses a **separate
      arms mesh parented to the camera on its own render layer with its own
      FOV**, driven by the same Animator parameters as the body — so a wave
      plays on your gloves *and* on your body. Build that here, with the art.
      `handWeight` is at 0.4 in the meantime so the body animates normally.
- [ ] Art pass: food-tier loot props, PEAK flat shading, colour grade
- [ ] **Audio pass** — cable creak under load, breathing that worsens with
      weight, the demolition approaching, survivors screaming through concrete,
      and the bridge alarm
- [ ] **FEATURE FREEZE: 3 May 2027.** No new systems after this date.

---

## BLOCK 9 · Content to 20 floors · **29 Mar – 11 Apr** (2 weeks)

- [ ] All 20 floors populated
- [ ] 3 survivors, 2 documents placed
- [ ] Demolition tuned across 10 rounds
- [ ] **CONTENT LOCK: 17 May 2027.**

---

## BLOCK 10 · Ship it · **12–25 Apr** (2 weeks)

- [ ] Menus, settings, key rebinding, aspect ratios, crash handling
- [ ] Steam integration, lobby, invites
- [ ] End-of-run ledger — who carried what, who got left behind
- [ ] **SUBMIT: 31 May 2027**

---

# THE BUFFER IS NOW ~5 WEEKS. THAT IS SURVIVABLE, NOT COMFORTABLE.

The blocks add up to 35 weeks and finish around **26 April 2027**, against a
31 May deadline. The old rope plan finished 10 May with three weeks of slack;
deleting the rope bought roughly five.

`MASTER.md` claims eight weeks of buffer. **The honest number is five**, and
netcode can still eat all of it.

So decide the cuts now, in advance, not in April:

**Cut in this order:**
1. **3 players → 2.** Every player is a network cost
2. **6 room modules → 4**
3. **Documents.** They're the endgame layer; the demo doesn't need them
4. **The Lost / rescue system.** Downed and revive is enough for a demo
5. **Demolition schedule item, appraiser, night vision** — shop depth

**Never cut:**
1. **The load gauge and the weight argument.** *This person or the gold* is the
   game. Without it you have a looting toy
2. **Downed players as cargo** — it's the moment people make videos about
3. **One survivor you can choose to leave behind**
4. **The bridge retract countdown** — it's the elevator's best single moment

---

# CONSISTENCY CHECK — WHAT THE DOCS SAY vs WHAT THE CODE DOES

Verified against `Campaign.cs` and `RunManager.cs` on 18 Aug 2026. **Nine
constants still contradict the design.** All of this is Block 1, Step 12.

| Where | Code today | Should be | Why |
|---|---|---|---|
| `Campaign.FloorHeight` | 4 m | **5 m** | one floor = one purchase |
| `Campaign.TotalFloors` | 5 | **20** demo / 100 full | |
| `Campaign.BaseQuota` | 800 | **200** | the mafia's round-1 demand |
| `Campaign.QuotaStep` | 600, **linear** | **× 1.072 per round** | exponential |
| `Campaign.BackpackSlotCost` | 900 | **120** | new economy |
| `Campaign.RoomsLostOnSurface` | 2 | **1** | plus 1 per 10-min tick |
| `Campaign.StartingRope` | 12 m | → **cable length 15 m** | 3 floors |
| `Campaign.RopeChunk` | 4 m | → **5 m** | one purchase = one floor |
| `Campaign.RopeCostPerMetre` | 45 | → **16** (80 per 5 m) | new economy |
| `RunManager.runTime` | **600 s hard cap** | **removed** | see gap #1 |
| — | — | **`Elevator.loadLimit = 550`** | crew 280 + haul 270 |

`Campaign.Quota` is `BaseQuota + (RunNumber-1) * QuotaStep` — **linear**. The
design needs `200 × 1.072^(R-1)`. This is not a tuning change, it's a formula
change, and it is the difficulty curve.

## Four things that are genuinely not logical yet

**1. `RunManager.runTime = 600` is a hard run timer.** The design says you may
stay as long as you like — the *rooms* die, not you. A hard 10-minute cap
contradicts the entire pressure system. **Delete it.** `roomChargeTime = 600` is
the correct clock and is already right.

**2. Room destruction is time-driven, not per-round.** `RoomsLostOnSurface = 2`
is wrong. It is `floor(runMinutes / 10) + 1` — one per 10-minute tick, witnessed
live, plus one on extraction, reported on the results screen. See
`ECONOMY_AND_CAMPAIGN.md` Part 1.

**3. Player mass is not modelled anywhere.** Nothing sums player bodies into the
load. The entire 550 kg design assumes four 70 kg bodies count against the
limit. **This is a real gap and it is the first line of Block 2.**

**4. The Demolition Schedule item (110) contradicts top-down demolition.** If
destruction is predictable top-down, knowing the order is worthless. **Fix: it
predicts the random *emergency* demolitions only** — the rare ones that jump the
queue. That makes it insurance rather than dead weight.

## Two open questions

- **`MASTER.md` §7 asks whether the mafia quota survives alongside the 30% cut.**
  Two money pressures may be one too many. Decide before Block 7.
- **Room dimensions were sized against a 10 m tether reach that no longer
  exists.** Re-check them against the bridge instead, during Block 5.

---

# THE HABITS THAT ACTUALLY GET IT DONE

**A Windows build every Friday, from Block 0.** Not when you need one.
Editor-only assumptions fail silently until they don't, and finding out in month
fourteen is how projects die.

**Playtest with strangers weekly from Block 4.** Your friends already know how it
works. Watch silently — where they hesitate is your design document.

**Track estimate vs actual for a month, then multiply.** You'll find a personal
factor, usually 1.5–3×. Applying it is the difference between a plan and a wish.

**Both freeze dates are real.** Studios that skip them ship late or not at all,
and the failure mode is always "one more system."

**One step per session, and commit after every step.** When something breaks you
want to lose twenty minutes, not two days.

---

# START HERE

Block 0 is done except the Windows build and the Steam page.

**Next: `ELEVATOR_SPEC.md` Step 2 — delete the six rope files.**

Everything else waits.
