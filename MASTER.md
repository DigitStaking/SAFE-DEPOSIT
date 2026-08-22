# SAFE DEPOSIT — Master Index

**Start here.** This is the single entry point. It says which documents are
true, which are dead, and where every system is specified — so nothing gets
lost when the design changes.

Last updated: 14 Aug 2026, after the elevator decision.

---

# 1. DOCUMENT STATUS

## Authoritative — these are true

| Doc | Covers |
|---|---|
| **`MASTER.md`** | this file — index, system inventory, handover prompt |
| **`GAME_DESIGN.md`** | world, story, the crew, the three cargoes, threats, art direction |
| **`ECONOMY_AND_CAMPAIGN.md`** | the numbers. Rooms, rounds, loot, mass, shop, survivors, rescue |
| **`PUZZLES.md`** | all 25 puzzles, the lock/key/modifier kit, placement rules |
| **`ELEVATOR_SPEC.md`** | the elevator, and the 12-step build order — **all 12 done, 19 Aug 2026** |
| **`PHASE3_SPEC.md`** | de-single-player — the 7-step build order for Block 3, with the survey it was planned from |
| **`PHASE2_SPEC.md`** | mass, health, downed, Lost, rescue, cable fray — the 10-step build order for Block 2 |
| **`ROADMAP.md`** | **start here for "where are we going" — all ten phases, and where voice and audio land** |
| **`DEMO_PLAN.md`** | schedule to Next Fest, consistency check, cut list — **rewritten 18 Aug 2026 for the elevator** |
| **`ANIMATIONS.md`** | animation system, clip list, two-layer Animator |

## Superseded — do not follow these

| Doc | Replaced by | Note |
|---|---|---|
| `ROPE_AND_PLATFORM.md` | `ELEVATOR_SPEC.md` | the platform became the elevator |
| `BUILD_PLAN.md` | `DEMO_PLAN.md` | older schedule |

**`ROADMAP.md` was rewritten on 19 Aug 2026** and is authoritative again — it
is now the phase-by-phase map from here to submission. The rope-era version it
replaced is in git history at `0df8199` if it is ever wanted.

## Sections of `GAME_DESIGN.md` that are now obsolete

- **§6 The rope** — tether, spool, Q pin, pumping, leaping. All replaced.
- **§7 The Loot Collector** — merged into the elevator.
- **§11 Shop** — superseded by the full price list in `ECONOMY_AND_CAMPAIGN.md`.

Everything else in that document — the world, the mafia, the three cargoes, the
threats, the art direction, the demo cut list — **still stands.**

---

# 2. WHAT THE ELEVATOR CHANGE DID NOT TOUCH

This is the part worth being explicit about.

| System | Status | Spec |
|---|---|---|
| **All 25 puzzles** | ✅ **completely unaffected** | `PUZZLES.md` |
| **The lock/key/modifier kit** | ✅ unaffected | `PUZZLES.md` |
| **Economy — 3 constants, all formulas** | ✅ unaffected | `ECONOMY_AND_CAMPAIGN.md` |
| **Loot budget spawner (1.4× what you can carry)** | ✅ unaffected | ECONOMY §4b |
| **Mass system — 550 kg, players count** | ✅ unaffected | ECONOMY §4 |
| **Shop — ~35 items with prices** | ✅ unaffected | ECONOMY §5 |
| **Survivors — 9 of 11, 3 deaths = loss** | ✅ unaffected | ECONOMY §7 |
| **Documents — 5 of 7** | ✅ unaffected | ECONOMY §7 |
| **The father, deepest room, exempt** | ✅ unaffected | ECONOMY §7 |
| **Rescue contract, 2 rounds, depth-scaled** | ✅ unaffected | ECONOMY §5 |
| **Health, no regen, downed, Lost** | ✅ unaffected | `DEMO_PLAN.md` |
| **Room clock, demolition, 100→50 rounds** | ✅ unaffected | ECONOMY §1 |
| **Shared money, leader, change-leader vote** | ✅ unaffected | ECONOMY §6 |
| **Traps** | ✅ unaffected except rope fray → **cable fray** | ECONOMY §4 |
| **Story, mafia, evidence, endings** | ✅ unaffected | `GAME_DESIGN.md` |
| **Animation, hand IK, emotes** | ✅ unaffected | `ANIMATIONS.md` |
| Rope tether / swing / hook | ❌ **deleted** | — |
| Cargo bands / Traverse | ❌ deleted — cargo goes on the deck | — |
| Rope tug signals | ❌ deleted | — |

**Only the traversal layer changed.** Everything about *why you're down there,
what you're deciding, and what it costs* is untouched.

---

# 3. THE FLOOR SHAPE, RECONCILED

Three different versions exist across the docs. **This is the current one:**

```
   ELEVATOR ──bridge──► DOOR ──► ROOM COMPLEX (2–3 connected spaces)
```

- **One door per floor.** The elevator's bridge extends to it.
- **Which of the four sides** the door is on varies by floor, so arriving means
  orienting yourself.
- Behind the door: a **room complex of 2–3 connected spaces** — enough to split
  a crew of four into pairs, few enough that nobody gets lost.
- **One floor in four** has a **sealed sub-room** behind a puzzle.

## The rule that governs all content placement

> **Everything that matters is behind a puzzle.** Every rare item, every
> survivor, every document. There is no other way to get them.

So a puzzle is never a detour — it's always *the* objective. Normal floors are
pure looting and pure speed. Three fast floors, then a floor where everyone
stops and thinks.

**Traps: one per floor, two on deep floors.** Punctuation, not prose.

---

# 4. SYSTEM INVENTORY — WHAT EXISTS, WHAT DOESN'T

## Built and working

| System | File(s) | Lines |
|---|---|---|
| First-person camera | `FirstPersonCamera.cs` | 209 |
| Player motor | `Playermotor.cs` | 312 |
| Carry + weight classes | `Carryable.cs`, `PlayerCarry.cs` | 519 |
| Backpack | `PlayerBackpack.cs` | 282 |
| Run loop — quota, extraction, collapse | `RunManager.cs` | 692 |
| Campaign persistence | `Campaign.cs` | 132 |
| Animation + hand IK | `PlayerAnimatorDriver`, `FirstPersonHands`, `AnimatorBuilder` | 1,395 |
| Head cull, skin, headlamp | 3 files | 507 |
| Atmosphere, smoke, light shafts | 3 files | 490 |
| Graybox generator | `Editor/Grayboxbuilder.cs` | 328 |

## To be deleted (Step 2)

`Playertether.cs` 987 · `Mainrope.cs` 624 · `RopeHook.cs` 308 ·
`PlayerProceduralAnim.cs` 94 · `Playerarms.cs` 147 ·
`Editor/AnimationSetupTool.cs` 312 — **2,472 lines**

## Not built at all

Elevator (all 6 files) · **puzzles — 0 files** · **traps — 0 files** ·
health / damage / downed / Lost / rescue · survivors · documents · shop UI ·
doors and keys · room content · leader and voting · **netcode**

---

# 5. THE BUILD ORDER

## Phase A — Elevator (`ELEVATOR_SPEC.md` §4, steps 1–12)

Delete the rope. Build the car, movement, dashboard, bridge, deck, scanner,
return-to-surface, graybox rebuild, economy retune.
**~4 weeks.**

## Phase B — Health, downed, Lost

Health with no regeneration · downed + bleed-out · **a downed player is a
`Carryable`** · Lost state · rescue contract with banded outcomes.
**~4 weeks.**

## Phase C — De-single-player

`Camera.main` in 9 files · `FindObjectsByType` in 9 files · `Campaign` stops
being `static` · per-player input, HUD, audio. Still runs solo throughout.
**~2 weeks.**

## Phase D — Netcode

NGO + Steam transport, host-authoritative. Elevator state replicated. Shared
money, leader, change-leader vote, departure vote. **Two players first.**
**~7 weeks.**

## Phase E — Rooms and puzzles

Room kit with tagged sockets · the 12 locks + 8 keys + 8 modifiers · the
**5 Tier-1 puzzles** for the demo · 4 traps · every survivor behind a puzzle.
**~4 weeks.**

## Phase F — Economy and shop

All formulas live · shop UI with leader spending · ~20 shop items · survivor
markers, screams, personal timers · mafia demand and results screen.
**~3 weeks.**

## Phase G — Vertical slice polish, then content, then ship

One floor to shippable quality · **audio pass** · art pass ·
**FEATURE FREEZE 3 May 2027** · 20 rooms populated ·
**CONTENT LOCK 17 May 2027** · **SUBMIT 31 May 2027.**

---

# 6. THE HANDOVER PROMPT

Paste this at the start of any new session:

> Read `MASTER.md` first, then the documents it marks as authoritative.
> The project is at `C:\Users\Digitstak\SAFE DEPOSIT`.
>
> SAFE DEPOSIT is a 4-player co-op first-person game in Unity 6.3 / URP. A crew
> descends an elevator into a demolished bank-shelter to loot food and medicine
> for the mafia, rescue survivors, and recover evidence — against a load limit
> and a demolition clock.
>
> We are on **Step N** of `ELEVATOR_SPEC.md`. **Do only Step N.**
>
> Before writing any code: read the files you intend to change and explain what
> you'll change and why. Then wait for me.
>
> Do not add systems that aren't in Step N. Do not refactor code you weren't
> asked about.

## The four rules that make this work

1. **One step per session.** Otherwise it builds five steps and you can't tell
   which one broke.
2. **Explanation before code.** This is why you can now debug your own project.
3. **Commit after every step**, not every session.
4. **Read before write.** An agent that hasn't read your code will confidently
   invent a version of it.

## What it can't do

- **Drag things in the Unity editor.** Steps 3 and 11 need you — or an editor
  script that builds the prefab in code, which has worked well here already.
- **See your game.** Screenshots have done the heavy lifting all along. Keep
  sending them.
- **Stop on its own.** It will always offer one more improvement. The step list
  says no for you.

---

# 7. STILL OPEN

1. **Does the mafia quota survive alongside the 30% cut?** Two money pressures
   may be one too many.
2. **Sub-spaces per room complex** — 2 or 3? Decide when building the room kit.
3. **The elevator at 250 in round 5** costs a whole round's surplus. Allow
   saving across two rounds, or drop to 200?
4. **`PlayerTether.maxTether`** is gone, but the 10 m reach it gave into rooms
   informed room sizing. Re-check room dimensions against the bridge instead.

---

# 8. THE NEXT THING TO DO

**Phase 1 is complete.** 19 Aug 2026 — all twelve steps of `ELEVATOR_SPEC.md`
are done. A full round plays start to finish: descend, loot, load, RETURN,
results, shop, repeat. The economy speaks `ECONOMY_AND_CAMPAIGN.md`'s numbers
rather than the pre-elevator prototype's, and loot is the five real tiers on a
per-round value budget.

**Next: play three rounds and answer one question — *does the money feel
tight?*** `DEMO_PLAN.md` is right that you will know within twenty minutes, and
that if it does not, the thing to change is `g`, not the systems.

**Then: `PHASE2_SPEC.md` Step 1 — capacity upgrades.**

## What Phase 1 turned out to be

Worth recording, because none of it was in the plan:

- **Step 1 was not "commit everything".** The code was already committed; a
  942 MB unreferenced `.glb` sat above GitHub's 100 MB per-file limit and had
  been silently blocking every push, so the entire project existed on one disk.
- **Four real bugs came out of Step 2's cleanup**, none of them rope-related:
  a world-space eye offset, stale-frame IK, world-space hand smoothing, and a
  skydiving pose on a 1.1 m hop.
- **The shaft was too narrow to be frightening.** 2 m of clearance against a
  3.7 m maximum jump — measured from `PlayerMotor`'s own constants, not
  guessed. Now 4.9 m.
- **Floor 0 was inside the shaft's ceiling slab**, which is why players and
  loot were left behind on extraction. Geometry, not code.
- **The quota was linear.** That one was in the plan, and it was still the
  most important single line changed in the whole phase.

## Scope decision, 18 Aug 2026

**The demo is built first and alone: 20 floors, 5 Tier-1 puzzles, 10 rounds.**
Nothing from the full game gets built until the demo ships. See `DEMO_PLAN.md`.
