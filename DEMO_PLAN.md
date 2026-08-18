# SAFE DEPOSIT — Demo Plan

**Target: Steam Next Fest, June 2027. Submission deadline 31 May 2027.**
A game can only ever appear in **one** Next Fest, so this is the only shot.

Today: 14 Aug 2026. **Working time: 9.5 months.**

---

# WHAT THE DEMO IS

| | Demo | Full game |
|---|---|---|
| Rooms | **20** | 100 |
| Rounds | **10** (~2.5 h) | 50 |
| Puzzles | **5** (Tier 1 only) | 25 |
| Survivors | 2 required, 3 placed | 9 of 11 |
| Documents | 1 required, 2 placed | 5 of 7 |
| Players | **3** (4 if netcode is comfortable) | 4 |
| Father | not present | deepest room |
| Loot Collector | not present | after room 20 |
| Threats | none — traps and collapse only | rival crew, starving one, etc. |

**Scope discipline:** the demo shows the *loop*, not the *content*. Five Tier-1
puzzles, one trap type per floor, no threats. A demo that shows your best ideas
has nothing left to sell.

---

# WHAT EXISTS TODAY — verified in the code

| Built | Lines | Notes |
|---|---|---|
| Graybox shaft generator | 328 | 5 floors, doors, loot cubes |
| FP camera / motor | 521 | Rigidbody + acceleration budget |
| Main rope (analytic) | 624 | anchor + length + bend. **To be replaced** |
| Player tether | 987 | 2.5 m swing, 10 m room reach, cut |
| Rope hook / pin | 308 | doorway kink |
| Carry + weight + backpack | 801 | Small / Heavy / Massive |
| Run loop | 692 | quota, extraction, collapse timer |
| Campaign persistence | 132 | money, rope, destroyed rooms |
| Animation + hand IK | 893 | two-layer Animator, avatar mask |
| Atmosphere / smoke / shafts | 490 | |
| Editor tooling | 2,318 | graybox, props, animator builder |

**~6,250 runtime + 2,300 editor lines.** Solid single-player vertical slice.

## What does not exist at all

Health · damage · downed · Lost · rescue · **puzzles (0 files)** ·
**traps (0 files)** · survivors · documents · shop UI · doors · keys ·
room content · leader/voting · **netcode — the entire co-op game**

---

# THE SCHEDULE

Twelve blocks. Each has a **definition of done** you can argue about, because a
milestone you can't test is a milestone you'll slip.

---

## BLOCK 0 · Lock down · **Aug 18–24, 2026** (1 week)

- [ ] **Commit everything.** ~6,000 lines are untracked right now
- [ ] Delete dead code: `AnimationSetupTool`, `PlayerProceduralAnim`,
      `PlayerArms`, `Arm_L`/`Arm_R`
- [ ] First Windows build; play it outside the editor
- [ ] **Steam page live** — wishlists compound from day one, and the page is
      required before Next Fest registration anyway

**Done when:** a stranger can wishlist the game, and `git log` shows today's work.

---

## BLOCK 1 · The rope rewrite · **Aug 25 – Sep 28** (5 weeks)

The identity of the game, and the one system everything else attaches to.

- [ ] Verlet chain, 24–32 nodes, position-based constraints, 6–8 solve
      iterations in `FixedUpdate`
- [ ] **`PointAtDepth()` / `Length` / `AnchorPosition` preserved exactly** so
      `PlayerTether`, `RopeHook` and `Carryable` need no changes (~2,000 lines
      saved)
- [ ] Players pin nodes; pulls propagate and sum
- [ ] Cargo pins two nodes, adds mass, forms the hitch
- [ ] **Capacity = 400 kg including players.** Sag and fray replace the number
- [ ] Rope tug signals — two tugs "coming up", three "help"

**Done when:** three heavy weights hang on it, four pullers swing it, and it
runs ten minutes without jitter or explosion.

---

## BLOCK 2 · Mass, health, downed · **Sep 29 – Nov 2** (5 weeks)

- [ ] Capacity system: 400 kg base, players 70 kg each, +50 kg upgrades
- [ ] Cargo bands + the **Traverse** move
- [ ] Health, no regeneration, visible injury states
- [ ] Downed + bleed-out; med spray revive; **downed player is a `Carryable`**
- [ ] Lost state + rescue contract with banded outcomes
- [ ] Rope fray trap + patch kit

**Done when:** a solo run where clipping loot badly makes your own climb worse,
and where four players plus a 140 kg survivor genuinely cannot all go up.

---

## BLOCK 3 · De-single-player · **Nov 3–16** (2 weeks)

Not netcode. Removing assumptions while the game still runs solo.

- [ ] `Camera.main` — **9 files** — each player owns its camera
- [ ] `FindObjectsByType` — **9 files** — replace with a player registry
- [ ] `Campaign` stops being `static`
- [ ] Input, HUD, audio become per-player

**Done when:** the game plays identically to before and nothing references a
global player.

---

## BLOCK 4 · Netcode · **Nov 17 – Jan 25, 2027** (10 weeks) ★ biggest unknown

Ten weeks, not eight. This is the part that eats schedules.

- [ ] Netcode for GameObjects + Steam transport, host-authoritative
- [ ] Host simulates the rope; clients get node positions ~20 Hz, interpolated
- [ ] Local prediction for your own body only
- [ ] Downed / revive / Lost replicated
- [ ] Shared money, leader, **Change Leader** vote
- [ ] **Ascent vote** — everyone must be on the rope; name whoever isn't

**Done when:** three players, one rope, opposite pulls cancel, one can carry
another out, and the ascent vote correctly names the person still in a room.

---

## BLOCK 5 · The room kit · **Jan 26 – Feb 22** (4 weeks)

- [ ] Landing + main + side + back, the fixed 3-sub-room shape
- [ ] 6 room modules with tagged sockets (`LootAnchor`, `LockAnchor`,
      `HazardAnchor`, `SurvivorAnchor`)
- [ ] Floor generator arranging modules
- [ ] Doors, keys, locked states

**Done when:** ten generated floors that a stranger can navigate without a map.

---

## BLOCK 6 · Puzzles & traps · **Feb 23 – Mar 22** (4 weeks)

- [ ] The lock/key/modifier kit: 12 locks, 8 keys, 8 modifiers as
      ScriptableObjects
- [ ] **The 5 Tier-1 puzzles** — three fuses, key-with-a-triangle, ledger,
      light plate, shutter relay
- [ ] 4 traps — floor collapse, gas, lockdown, rope fray
- [ ] **Every survivor behind a puzzle**

**Done when:** puzzle #6 can be authored in five minutes with no new code.

---

## BLOCK 7 · The economy & shop · **Mar 23 – Apr 12** (3 weeks)

- [ ] All formulas from `ECONOMY_AND_CAMPAIGN.md`
- [ ] Shop UI — leader spends, assigns items, everyone sees what was bought
- [ ] ~20 of the ~35 shop items (demo subset)
- [ ] Survivor markers, screams, personal timers
- [ ] Mafia demand + results screen + speed bonus

**Done when:** ten rounds can be played end to end and the money is always tight.

---

## BLOCK 8 · Vertical slice polish · **Apr 13 – May 3** (3 weeks)

**One floor finished to shippable quality.** Final art, final audio, no
placeholders. This is the milestone that tells you the truth — and it lands with
four weeks still in hand rather than four days.

- [ ] Art pass: food-tier loot props, PEAK flat shading, colour grade
- [ ] **Audio pass** — you asked to defer sound and this is where it lands:
      rope creak under load, breathing that worsens with weight, the collapse
      approaching, survivors screaming through concrete
- [ ] **FEATURE FREEZE: 3 May 2027.** No new systems after this date.

---

## BLOCK 9 · Content to 20 rooms · **May 4–17** (2 weeks)

- [ ] All 20 rooms populated
- [ ] 3 survivors, 2 documents placed
- [ ] Demolition schedule tuned across 10 rounds
- [ ] **CONTENT LOCK: 17 May 2027.**

---

## BLOCK 10 · Ship it · **May 18–31** (2 weeks)

- [ ] Menus, settings, key rebinding, aspect ratios, crash handling
- [ ] Steam integration, lobby, invites
- [ ] End-of-run ledger — who carried what, who got left behind
- [ ] **SUBMIT: 31 May 2027**

---

# THE SCHEDULE HAS 3 WEEKS OF SLACK. THAT IS NOT ENOUGH.

Add up the blocks and they finish around 10 May. The remaining three weeks are
your entire buffer for 9.5 months of work, and **the honest expectation is that
netcode alone will eat them.**

So decide the cuts now, in advance, not in May:

**Cut in this order:**
1. **3 players → 2.** Every player is a network cost, and the rope traffic jam
   works with two
2. **6 room modules → 4**
3. **Documents.** They're the endgame layer; the demo doesn't need them
4. **The Lost / rescue system.** Downed and revive is enough for a demo; being
   Lost for two runs needs a campaign to mean anything
5. **The demolition schedule item, appraiser, night vision** — shop depth

**Never cut:**
1. **The rope.** It's the game
2. **Downed players as cargo** — it's the moment people make videos about
3. **One survivor you can choose to leave.** The weight argument must be in the
   demo or the demo is a physics toy

---

# THE HABITS THAT ACTUALLY GET IT DONE

**A Windows build every Friday, from Block 0.** Not when you need one. Editor-only
assumptions fail silently until they don't, and finding out in month fourteen is
how projects die.

**Playtest with strangers weekly from Block 4.** Your friends already know how the
rope works. Watch silently — where they hesitate is your design document.

**Track estimate vs actual for a month, then multiply.** You'll find a personal
factor, usually 1.5–3×. Applying it is the difference between a plan and a wish.

**Both freeze dates are real.** Studios that skip them ship late or not at all,
and the failure mode is always "one more system."

---

# CONSISTENCY CHECK — WHAT THE DOCS SAY vs WHAT THE CODE DOES

I read the constants. Nine of them now contradict the design.

| Where | Code today | Should be | Why |
|---|---|---|---|
| `Campaign.StartingRope` | 12 m | **15 m** | 3 floors × 5 m |
| `Campaign.RopeChunk` | 4 m | **5 m** | one purchase = one floor |
| `Campaign.RopeCostPerMetre` | 45 | **16** (80 per 5 m) | new economy |
| `Campaign.BaseQuota` | 800 | **200** | the mafia's round-1 demand |
| `Campaign.QuotaStep` | 600, linear | **× 1.072 per round** | exponential, not linear |
| `Campaign.TotalFloors` | 5 | **20** demo / **100** full | |
| `Campaign.RoomsLostOnSurface` | 2 | **1** | plus 1 per 10-min tick |
| `Campaign.BackpackSlotCost` | 900 | **120** | new economy |
| `MainRope.loadLimit` | 400 | **550** | crew (280) + round-1 haul (270) |
| `MainRope.maxRopeLength` | 60 m | **500 m** | 100 rooms × 5 m |
| `RunManager.runTime` | **600 s hard limit** | **removed** | ⚠ see below |

## ⚠ Four things that are genuinely not logical yet

**1. `RunManager.runTime = 600` is a hard run timer.** The design says you can
stay as long as you like — the *rooms* die, not you. A hard 10-minute cap
contradicts the entire pressure system. **Delete it.** `roomChargeTime = 600` is
the correct clock and it's already right.

**2. The Loot Collector and the Platform are the same object.** Both are "a cage
on the rope that carries cargo and blocks climbers." Having both is one system
too many. **Merge them: the Platform *is* the Collector**, bought at round 5,
and it's what makes Bulk-heavy items extractable. Delete the 600 Collector from
the shop.

**3. The Demolition Schedule item (110) contradicts top-down demolition.** If
destruction is predictable top-down, knowing the order is worthless. **Fix: it
predicts the random *emergency* demolitions only** — the rare ones that jump the
queue. That makes it a genuine insurance purchase rather than dead weight.

**4. Player mass is not modelled anywhere.** `MainRope` sums cargo but not
people. The entire 550 kg design assumes four 70 kg bodies count against the
limit. **This is a real gap and it's the first line of Block 1.**

## Two things to keep an eye on

- **The platform costs 250; round-5 surplus is 259.** Buying it means zero rope
  that round while the demolition keeps running. That's a real decision, but it
  might be too punishing — consider allowing it to be saved for across two
  rounds, or dropping it to 200.
- **`PlayerTether.maxTether = 10 m`** was tuned when floors were 4 m apart. With
  5 m spacing a 10 m tether reaches two floors down, which may be too generous.
  Re-tune after the rope rewrite, not before.

---

# START HERE — THE NEXT THREE SESSIONS

Where we actually stopped: animations play, the two-layer Animator works, hand
IK holds the hands in frame. The game is a working single-player slice.

## Session 1 — Safety (1 hour)

1. **Commit everything.** ~6,000 lines untracked. Nothing else matters until
   this is done.
2. Delete `AnimationSetupTool.cs`, `PlayerProceduralAnim.cs`, `PlayerArms.cs`,
   and the `Arm_L` / `Arm_R` objects
3. Build for Windows, run it outside the editor, note what breaks
4. Tag the commit `v0.1-solo-slice`

## Session 2 — Make the code agree with the design (2–3 hours)

Retune the eleven constants in the table above. No new systems — just make the
existing game speak the new economy.

Then **play three rounds** and answer one question:

> *Does the money feel tight?*

You'll know within twenty minutes. If it doesn't feel tight, we change `g`, not
the systems — and we find that out now rather than in April.

## Session 3 — Begin the rope (this is the big one)

1. **Add player mass to the rope's load calculation** — the missing piece from
   the check above. Small change, immediately makes the 550 kg limit real
2. Then start the Verlet rewrite, keeping `PointAtDepth()` identical

---

# THE FIRST THING TO DO

Commit. Right now, before anything else on this page. Six thousand lines of work
are sitting untracked, and every other item here assumes they still exist
tomorrow.
