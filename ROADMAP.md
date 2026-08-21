# SAFE DEPOSIT — The Whole Road

Every phase from here to submitting the demo, in order, with the step lists
that already exist and the ones that do not yet.

**Target: Steam Next Fest, June 2027. Submission 31 May 2027.**
A game appears in only one Next Fest, so this is the only shot.

Today: **19 Aug 2026.** Phase 1 is complete.

This file is the map. `DEMO_PLAN.md` is the schedule with dates and the cut
list; the per-phase specs hold the actual step-by-step. Where they disagree,
the per-phase spec wins for *what*, `DEMO_PLAN.md` wins for *when*.

---

# WHERE WE ARE

```
  PHASE 1  ████████████  the elevator ...................... DONE  12/12
  PHASE 2  ████████░░░░  mass, health, downed ........ in progress  6/9
  PHASE 3  ░░░░░░░░░░░░  de-single-player
  PHASE 4  ░░░░░░░░░░░░  netcode + PROXIMITY VOICE   ← biggest unknown
  PHASE 5  ░░░░░░░░░░░░  the room kit
  PHASE 6  ░░░░░░░░░░░░  puzzles and traps
  PHASE 7  ░░░░░░░░░░░░  economy and shop
  PHASE 8  ░░░░░░░░░░░░  polish + FULL AUDIO PASS
  PHASE 9  ░░░░░░░░░░░░  content to 20 floors
  PHASE 10 ░░░░░░░░░░░░  ship it
```

**Voice arrives in Phase 4. Everything else you hear arrives in Phase 8.**
Both are called out below where they land, because "when do I get to add
voice and everything" is the question this file was written to answer.

---

# PHASE 1 — THE ELEVATOR ✅ DONE

`ELEVATOR_SPEC.md` · 12 steps · finished 19 Aug 2026

Delete the rope, build the car, movement, dashboard, bridge, cargo and load,
price scanner, return to surface, graybox rebuild, economy retune.

**A full round plays start to finish**: descend, loot, load, RETURN, results,
shop, repeat. Loot is the five real economy tiers on a per-round value budget.

What it cost that was not planned: a 942 MB file silently blocking every push,
four bugs that fell out of deleting the rope, a shaft too narrow to be
frightening, and floor 0 sitting inside the ceiling slab.

---

# PHASE 2 — MASS, HEALTH, DOWNED

`PHASE2_SPEC.md` · **9 steps** (7 moved to Phase 4) · 4 weeks · *21 Sep – 18 Oct 2026*

Turns the load gauge from an inconvenience into the argument the game is about,
by adding the one cargo that can object to being left behind.

| # | Step |
|---|---|
| 1 | ✅ Capacity upgrades — `550 + 50n`, cost `50 × 1.25ⁿ` |
| 2 | ✅ Health — 100 HP, **no regeneration, ever** |
| 3 | ✅ Fall damage |
| 4 | ✅ Injury states — the limp *(done early; see Step 2)* |
| 5 | ✅ Downed and bleed-out — 90s, and it does not pause |
| 6 | ✅ ★ **A downed player is a `Carryable`** |
| 7 | ⏭️ Revive — **moved to Phase 4** (needs a second player) |
| 8 | Lost |
| 9 | Rescue contract — `Mafia(R) × (1 + f/10)` |
| 10 | Cable fray |

**Step 7 moved to Phase 4 on 21 Aug 2026.** Both ways of saving someone —
med spray *on them*, or carrying them out — need a second player to be
tested at all, and a rescue verified only by reading the code is not
verified. The seam is already built and working: `DownedPlayer.Revive()`
exists, restores you at 20 HP, drops you out of your carrier's arms and
destroys your `Carryable`. Phase 4 attaches a purchasable med spray to it.

**Step 6 is why this phase exists.** `Carryable` already handles weight
classes, two-handed carrying, the load gauge and the scanner — so a downed
crewmate becomes 70 kg that talks, and every Phase 1 system handles them
without knowing they are a person. Survivors in Phase 5 reuse it.

---

# PHASE 3 — DE-SINGLE-PLAYER

2 weeks · *19 Oct – 1 Nov 2026* · spec not written yet

**Not netcode.** Removing the assumption that there is exactly one of
everything, while the game still runs solo the whole time.

- `Camera.main` — **9 files** — each player owns its camera
- `FindFirstObjectByType` — **9 files** — replaced with a player registry
- `Campaign` stops being `static`
- Input, HUD and audio become per-player

Every script written in Phases 1 and 2 that says *"single-player lookup, Phase C
replaces this with a player registry"* is a line item here. They were written
that way on purpose, and they are already commented.

**Done when:** the game plays identically and nothing references a global player.

---

# PHASE 4 — NETCODE, AND PROXIMITY VOICE 🎙️

7 weeks · *2 Nov – 20 Dec 2026* · spec not written yet · **★ biggest unknown**

Seven weeks, down from ten — deleting the rope is what bought that. A moving
platform is a position and a state; a 32-node simulated rope replicated at
20 Hz was the hardest thing in the old plan.

- Netcode for GameObjects + Steam transport, host-authoritative
- Elevator state replicated: floor, moving, doors, bridge, load
- Local prediction for your own body only
- Players riding a moving platform stay in sync
- Downed / revive / Lost replicated
- Shared money, leader, **Change Leader** vote
- Departure vote — everyone aboard, name whoever is not
- **🎙️ PROXIMITY VOICE**
- **Revive** — deferred here from Phase 2 Step 7. Med spray (35) revives in
  place at 20 HP; carrying them to the lift is the free alternative that
  costs time instead of money. `DownedPlayer.Revive()` already does the work
  — this is the shop item, the use interaction, and the first honest test of
  it. **Do this early in the phase**, the moment two players can connect: it
  is the cheapest possible proof that downed/carry replicates correctly.

**This is where voice arrives, and it is not decoration.** Half the design
assumes it: the key-with-a-triangle puzzle exists to sell the walkie-talkie,
the ledger is "pure voice-chat gameplay", and naming the missing crewmate on
the departure screen only works because "three people shouting one name is the
moment." Every puzzle in Phase 6 is built on top of it.

**Done when:** three players ride together, one can carry another out, one can
spray another back onto their feet, and the departure vote correctly names the
person still in a room. **Two players first.**

---

# PHASE 5 — THE ROOM KIT

4 weeks · *21 Dec 2026 – 17 Jan 2027* · spec not written yet

The graybox becomes a real generator.

- Landing → main → side → back, the fixed 3-sub-room shape
- 6 room modules with tagged sockets (`LootAnchor`, `LockAnchor`,
  `HazardAnchor`, `SurvivorAnchor`)
- Floor generator arranging modules
- The door on a different side per floor — **already working**, Phase 1 built it
- Doors, keys, locked states
- **Survivors**, reusing Phase 2's downed-player carrying

**Done when:** ten generated floors a stranger can navigate without a map.

---

# PHASE 6 — PUZZLES AND TRAPS

4 weeks · *18 Jan – 14 Feb 2027* · `PUZZLES.md`

- The kit: **12 locks, 8 keys, 8 modifiers** as ScriptableObjects
- **The 5 Tier-1 puzzles** — three fuses, key-with-a-triangle, ledger, light
  plate, shutter relay
- 4 traps — floor collapse, gas, lockdown, cable fray *(fray already exists
  from Phase 2)*
- **Every survivor behind a puzzle. No exceptions.**

Build the twelve locks and eight keys **once**. A puzzle is then a
ScriptableObject naming a lock, a key, a modifier and two room sockets.

**Done when:** puzzle #6 can be authored in five minutes with no new code.

⚠️ **You asked for a full 25-puzzle redesign early on and we never did it** —
Phase 1 took over. The demo needs 5 Tier-1 puzzles only, so the redesign is not
blocking, but it is still owed before the full game.

---

# PHASE 7 — ECONOMY AND SHOP

3 weeks · *15 Feb – 7 Mar 2027* · `ECONOMY_AND_CAMPAIGN.md`

- Shop UI — leader spends, assigns items, **everyone sees what was bought**
- ~20 of the ~35 shop items
- The **±10% mafia randomiser** and the **speed bonus** — both deferred out of
  Phase 1 Step 12 on purpose, because they need state rather than a constant
- Survivor markers, screams, personal timers
- Results screen, rooms-lost report

**Done when:** ten rounds play end to end and the money is always tight.

---

# PHASE 8 — VERTICAL SLICE POLISH 🔊

3 weeks · *8 – 28 Mar 2027*

**One floor finished to shippable quality.** The milestone that tells you the
truth, landing with weeks in hand rather than days.

- **🔊 THE FULL AUDIO PASS** — cable creak under load, breathing that worsens
  with weight, the demolition approaching, survivors screaming through
  concrete, the bridge alarm
- **First-person arms** — the rebuild deferred from Phase 1, on its own decision
  note in `DEMO_PLAN.md`
- Art pass: food-tier loot props *(replacing the placeholder prefabs)*, PEAK
  flat shading, colour grade
- **FEATURE FREEZE: 3 May 2027.** No new systems after this date.

**This is where the game stops being silent.** Voice arrives in Phase 4;
everything else you hear arrives here.

---

# PHASE 9 — CONTENT TO 20 FLOORS

2 weeks · *29 Mar – 11 Apr 2027*

- All 20 floors populated
- 3 survivors, 2 documents placed
- Demolition tuned across 10 rounds
- **CONTENT LOCK: 17 May 2027**

---

# PHASE 10 — SHIP IT

2 weeks · *12 – 25 Apr 2027*

- Menus, settings, key rebinding, aspect ratios, crash handling
- Steam integration, lobby, invites
- End-of-run ledger — who carried what, who got left behind
- **SUBMIT: 31 May 2027**

---

# THE BUFFER IS ~5 WEEKS

Phases 2–10 total 31 weeks and finish around **26 April 2027**, against a
31 May deadline. `MASTER.md` once claimed eight weeks of buffer; the honest
number is five, and **netcode can still eat all of it.**

So the cuts are decided now, in advance, not in April:

**Cut in this order**
1. 3 players → 2
2. 6 room modules → 4
3. Documents
4. The Lost / rescue system *(Phase 2 steps 8–9)*
5. Demolition schedule item, appraiser, night vision

**Never cut**
1. The load gauge and the weight argument — *this person or the gold*
2. Downed players as cargo
3. One survivor you can choose to leave behind
4. The bridge retract countdown

---

# KNOWN ISSUES — carried, not forgotten

### ~~Loot ends up on the elevator roof~~ · FIXED 21 Aug 2026

Four attempts, three of them wrong, and the difference on the fourth was
that it **measured instead of reasoning**. Kept here as the worked example.

**The bug:** the loot prefabs carry a Rigidbody, so `Instantiate` registered
a physics body at the prefab's authored pose — the origin — before the
spawner touched anything. `go.transform.position = …` then moved the
*transform* and left the *body* at the origin. Enabling
`RigidbodyInterpolation.Interpolate` straight afterwards made it permanent:
interpolation has Unity write the transform every frame from the body's own
pose history, and that history said origin. Every item was stomped back to
0,0,0 and fell down the shaft, landing on the elevator roof — the only wide
flat thing on the way down.

**The fix:** write `rb.position` / `rb.rotation`, which move the physics
pose and reset the interpolation history, and enable interpolation
*afterwards* on a body already in the right place.

**Why three attempts missed it.** All three assumed a *placement* bug and
re-derived the slot arithmetic. The arithmetic was always correct. The
audit proved it in one run — all 60 spawn positions right — and then the
settled positions named the real cause: every item at x≈0, z≈0 within
centimetres, several having risen ninety metres to get there. Nothing
pushes 60 objects onto one axis; the origin was simply where they had
never really left.

**The lesson, which was already written here and ignored twice:** when two
fixes have failed, stop reasoning about the code and log the actual
numbers. The instrument cost one commit and less time than any single
wrong guess.

# THE FOUR RULES, WHICH HAVE NOT CHANGED

1. **One step per session.**
2. **Explanation before code.**
3. **Commit after every step.**
4. **Read before write.**

And the two things it cannot do: **drag things in the Unity editor** — so
prefabs get built by editor scripts, which has worked every time — and **see
your game**, so keep sending screenshots. Every layout bug in Phase 1 was found
in one, and several were found *only* in one.
