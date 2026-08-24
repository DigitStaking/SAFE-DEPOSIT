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
  PHASE 2  ████████████  mass, health, downed .............. DONE  8/8
  PHASE 3  ████████████  de-single-player .................. DONE  7/7
  PHASE 4  ██░░░░░░░░░░  netcode + PROXIMITY VOICE ...... in progress  2/11
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

`PHASE2_SPEC.md` · **8 steps** (7 and 9 moved to Phase 4) · 4 weeks · *21 Sep – 18 Oct 2026*

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
| 8 | ✅ Lost — a named roster, not a game over |
| 9 | ⏭️ Rescue contract — **moved to Phase 4** (needs a crew) |
| 10 | ✅ Cable fray — greed, billed in rope |

**Steps 7 and 9 moved to Phase 4 on 21 Aug 2026.** Step 9 for a subtler
reason than Step 7: solo, the rescue is not a DECISION. The step exists to
create one moment — *"whether the rope matters more than their friend"* — and
that requires the run to be able to continue **without** paying. With one
player, being lost means pay or the campaign is over, which is a paywall
wearing the formula rather than the choice it is supposed to be. Partial
payment carrying over is untestable for the same reason: you cannot earn the
rest while you are the one who is missing.

**Step 7 moved for a blunter one.** Both ways of saving someone —
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

`PHASE3_SPEC.md` · **7 steps** · 2 weeks · *19 Oct – 1 Nov 2026*

**Not netcode.** Removing the assumption that there is exactly one of
everything, while the game still runs solo the whole time.

**The one-sentence test:** drop a second player prefab into the scene, press
Play, and both bodies work — two cameras that do not fight, one HUD, two
health values, and a load gauge reading 140 kg.

| # | Step |
|---|---|
| 1 | ✅ The player registry — replaces 6 player lookups, caches 9 singletons |
| 2 | ✅ A player knows if it is local |
| 3 | ✅ Every player owns its camera — all 14 `Camera.main` calls dead |
| 4 | ✅ Per-player state — 4 fields moved to `Crew` |
| 5 | ✅ The crew is a list, not a player |
| 6 | ✅ Input per player — devices are owned, not global |
| 7 | ✅ The two-body test — an editor rig and a runtime audit |

Surveyed 21 Aug 2026, and it is smaller than this file used to claim. Two
findings worth carrying:

**`Campaign` should NOT stop being static.** Of everything it holds, only
`Health`, `BleedOutLeft`, `PlayerLost` and `BackpackSlots` are per-player.
The rest is shared *by design* — `ECONOMY` Part 6: "All loot goes into one
pot." Four fields, not a rewrite.

**Every one of the 14 `Camera.main` calls means "mine".** None wants a
global, so that half is mechanical rather than architectural.

Three things will break loudly and are already written down in the spec: the
body cull will hide a **teammate's** head, the dashboard will freeze the
**other** player when somebody presses F, and there is exactly one headlamp
bound to whichever animator Unity returned first.

**Done when:** the game plays identically and nothing references a global
player.

---

# PHASE 4 — NETCODE, AND PROXIMITY VOICE 🎙️

`PHASE4_SPEC.md` · **11 steps** · 7 weeks · *2 Nov – 20 Dec 2026* · **★ biggest unknown**

Seven weeks, down from ten — deleting the rope is what bought that. A moving
platform is a position and a state; a 32-node simulated rope replicated at
20 Hz was the hardest thing in the old plan.

**The survey found one number that is the whole phase: 59 public statics.**
A static is one copy per PROCESS, so `Campaign.Money` on a client today is not
a stale copy of the host's — it is an unrelated number. `Campaign` and `Crew`
are static because they must survive `ReloadScene`, which was correct and
still is; surviving a scene reload and surviving a network boundary are just
different problems, and only one of them is solved.

**All three decisions are made** — see `PHASE4_SPEC.md` Part 5, decided from
what the comparable games actually shipped rather than from preference.

- **Unity Netcode for GameObjects + Facepunch Steam transport**,
  host-authoritative. *(Decided 21 Aug 2026 — `PHASE4_SPEC.md` Part 5 records
  why this moved twice.)* **No subscription, no player ceiling**: Steam
  Datagram Relay is free to Steam developers with no CCU limit. This is
  **Lethal Company's** stack, and Lethal Company is a closer comparable to
  this game than PEAK or We Were Here — one developer, four-player co-op,
  first person, scavenge against a quota, proximity voice, Steam.

  Photon Fusion is the better SDK on physics and genuinely easier. It also
  bills monthly forever, and PEAK and We Were Here pay it because they are
  companies with millions of sales. Trade taken: rider sync is ours to solve
  rather than the SDK's, against a cost with no ceiling.
- **Steam now; Epic is a planned fork, not a surprise.** *(Decided 21 Aug
  2026.)* Steam relay only serves Steam players. If the game does well and
  Epic becomes worth it — Epic takes 12% where Steam takes 30% — the answer is
  **Epic Online Services**: free, no CCU limit, and crossplay across Steam,
  Epic, itch and standalone. It would then be mandatory for *everyone*, since
  a four-player co-op game split across stores without crossplay is worse than
  not shipping there. Swapping is one component behind NGO, which is the whole
  reason the transport is not the SDK. **Revisit at Step 11.**
- **Voice: Dissonance** — one-time Asset Store purchase, no subscription,
  official NGO integration, and what Lethal Company's proximity chat uses.
- Elevator state replicated: floor, moving, doors, bridge, load
- Local prediction for your own body only
- Players riding a moving platform stay in sync
- Downed / revive / Lost replicated
- Shared money, leader, **Change Leader** vote
- Departure vote — everyone aboard, name whoever is not
- **🎙️ PROXIMITY VOICE — occluded by concrete, with reverb by space.**
  Two floors down is silent; one floor down is a muffled thump you can almost
  identify; the shaft has a tail on it. Specified in `PHASE4_SPEC.md` Step 10.
  Note that the concrete winning is a FEATURE — the walkie-talkie (30) and the
  radio relay (75) are only worth buying because voice does not carry.
- **Rescue contract** — deferred here from Phase 2 Step 9.
  `Rescue(R, f) = Mafia(R) × (1 + f/10)`, partial payment carried over, three
  deaths ends the campaign. `Campaign.LostCrew` already records who and on
  which floor, and carries an untouched `paid` field for exactly this.
  Needs a crew that can keep running while somebody is still down there —
  otherwise paying is mandatory and the decision does not exist.
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
- **★ THE MARKET — a shop you walk through, not a menu.** *(Proposed 21 Aug
  2026.)* Four players in a physical space, picking things off shelves,
  arguing about money in front of each other. It is the same argument the
  dashboard already won: that file says a menu "would be less work and it
  would be wrong", because a panel is somewhere you **stand**, with your back
  to the door, while three people watch. The current between-runs shop is one
  person clicking a GUI while three others look at nothing — the worst
  multiplayer moment in the game, and the market fixes exactly that.

  **Scope honestly:** an environment, an interaction pass and a checkout, on
  top of a 3-week block, against a 5-week buffer netcode can eat. So the GUI
  shop stays as the shipping fallback and the market is built only if Phase 4
  lands on time. Nothing depends on it — backpack slots are already owned
  per-person (`PHASE3_SPEC.md` Part 2), so the market changes the *interface*
  and not the data.
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

### Solo, bleeding out ends the campaign · by design until Phase 4

With one player there is nobody left above ground, so a bleed-out ends the
run AND the campaign — "there is nobody left above ground to come back for
you". That is not a bug and it is not the finished behaviour either. The
rescue contract (Phase 4) is what turns it into a bill instead of a wall, and
it needs a crew that can keep running while somebody is still down there.

Until then: **Shift+H before the 90 seconds expire**, or start over.


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
