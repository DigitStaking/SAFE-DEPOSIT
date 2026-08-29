# SAFE DEPOSIT — Phase 2: Mass, Health, Downed

The elevator carries the weight argument. This phase gives it teeth: a
**person** becomes something you can carry, and something you can lose.

Corresponds to **Block 2** in `DEMO_PLAN.md` — 4 weeks, 21 Sep – 18 Oct 2026.
Written to be handed to Claude Code **one step at a time**, exactly like
`ELEVATOR_SPEC.md`.

Last updated: 19 Aug 2026, after Phase 1 shipped.

---

# PART 1 — WHAT THIS PHASE IS FOR

Phase 1 built a lift with a load gauge. Right now that gauge is an
inconvenience: go over 550 kg and a button refuses you. Annoying, not moral.

This phase turns it into the argument the whole game is about, by adding the
one cargo that can object to being left behind.

## The single most important thing in this phase

> **A downed player is a `Carryable`.**

`DEMO_PLAN.md` lists it under "never cut" and calls it "the moment people make
videos about". It is also the cheapest big idea in the project, because
`Carryable` already exists and already works: weight classes, two-handed
carrying, the load gauge, the price scanner. A downed crewmate becomes 70 kg
of cargo that talks — and every system built in Phase 1 handles them without
knowing they are a person.

That is the whole design. Everything else in this phase exists to produce that
moment or to make it cost something.

## What we are NOT building here

- **Threats.** Nothing hunts you in the demo (`DEMO_PLAN.md`). Damage comes
  from falling, from the collapse, and from traps.
- **Netcode.** Block 4. Everything here stays single-player and must not
  assume otherwise — no new `Camera.main` or `FindFirstObjectByType` in
  gameplay paths beyond the ones Phase C already has to clean up.
- **Puzzles or survivors.** Blocks 5 and 6. Survivors reuse the downed-player
  carrying code, which is another reason to build it properly now.

---

# PART 2 — THE SPEC

## Health

**100 HP. No regeneration. Ever.**

`DEMO_PLAN.md` is explicit and it is the point: damage is permanent within a
run, so a bad fall on floor 3 is still with you on floor 12. The only way back
is a bandage you had to buy with money you wanted for cable.

| State | HP | Effect |
|---|---|---|
| Fine | 100–51 | nothing |
| Hurt | 50–26 | **the limp** — slower, audible |
| Critical | 25–1 | limp plus heavy breathing; screen edges darken |
| **Downed** | 0 | on the floor, bleeding out, `Carryable` |
| **Lost** | — | bleed-out completed. Gone until rescued |

## Damage sources in the demo

1. **Falling.** Below a safe drop, damage scales with impact speed. This is
   the one that makes the 4.9 m shaft gap lethal rather than embarrassing.
2. **The collapse.** Already kills outright (`RunManager.SealRoomIndex`).
3. **Traps.** Block 6. Cable fray is the exception and lands in this phase,
   because it belongs to the elevator.

## Downed

At 0 HP you do not die. You drop where you stood and start a **90-second
bleed-out**. While downed you:

- cannot move, look freely, or interact
- **can still talk** — this is the entire reason the state exists
- are a `Carryable` at **70 kg**, weight class Massive
- can be revived in place with a **med spray** (35), or carried out

The clock does not stop because someone picked you up. Carrying you to the lift
is a race, not a rescue.

## Lost

Bleed-out completes and you are **Lost** — not dead. You are gone for the rest
of the run and the next one, and the shop offers a **rescue contract**:

```
Rescue(R, f) = Mafia(R) × (1 + f/10)      f = the floor they were lost on
```

`ECONOMY_AND_CAMPAIGN.md` Part 5: shallow losses are recoverable, deep losses
are a crisis costing both of your next two runs. **Partial payment carries
over**, so the crew spends two rounds deciding, every single time they open the
shop, whether the cable matters more than their friend.

**Three deaths ends the campaign** (ECONOMY Part 7). Lost is not death — dying
is failing to pay for the rescue.

## Cable fray

The one trap that belongs to the elevator, and the reason `ELEVATOR_SPEC.md`
insisted on keeping a visible cable: *"The cable can fray under overload — your
best trap survives."*

- Riding **overloaded** frays the cable a little each trip
- Fray is **visible on the cable itself**, above your heads
- At 100% the cable snaps: everyone aboard is Lost, the run is over
- A **patch kit** (15) repairs it mid-run

This is the only place in the demo where greed kills you directly rather than
by running out of time.

---

# PART 3 — WHAT CHANGES

## New files

`PlayerHealth.cs` · `DownedPlayer.cs` · `CableWear.cs` · `RescueContract.cs`

## Changed

| File | What changes |
|---|---|
| `Carryable.cs` | must tolerate a carryable that is also a player — no layer/renderer stomping |
| `PlayerMotor.cs` | speed multiplier from injury; movement lock while downed |
| `PlayerCarry.cs` | picking up a downed player; cannot stow one |
| `ElevatorDeck.cs` | a carried downed player must not be double-counted (70 kg once, not twice) |
| `Campaign.cs` | capacity upgrades, Lost roster, rescue debt |
| `RunManager.cs` | Lost players in the results screen; three-deaths campaign end |
| `ElevatorCable.cs` | fray visual |

## Untouched

Everything the elevator does. If a step in this phase requires editing
`Elevator.cs`, `ElevatorBridge.cs` or `ElevatorDashboard.cs`, stop and ask
whether the change belongs somewhere else — Phase 1 is finished and its
behaviour is the baseline this phase is tested against.

---

# PART 4 — BUILD ORDER: TEN STEPS

**One step per session.** Each ends with a game that runs and a commit.

Steps 7 and 9 have moved to Phase 4 — see their entries below. The remaining
order is 1-6, then **8**, then **10**. Both are testable with one player;
neither of the deferred ones is.

### Step 1 · Capacity upgrades
`Campaign` gains `Capacity(n) = 550 + 50n` and `CapacityCost(n) = 50 × 1.25ⁿ`,
a shop button, and `ElevatorDeck.capacity` reads it instead of a constant.
**Done when:** buying an upgrade visibly raises the gauge's ceiling, and the
cost of the next one has gone up.
*Closes the economy loop you are testing right now — do it first for that
reason alone.*

### Step 2 · Health
`PlayerHealth.cs` — 100 HP, no regeneration, `TakeDamage`, a HUD readout.
Nothing damages you yet; a debug key does.
**Done when:** a keypress takes you to 0 and the number never climbs back.

### Step 3 · Fall damage
Damage above a safe landing speed, scaled by impact. Reuses `PlayerMotor`'s
existing grounded/velocity state — no new physics.
**Done when:** stepping off a crate is free, falling down the shaft is not.

### Step 4 · Injury states
The limp below 50%, heavy breathing below 25%, a vignette. Speed comes from
`PlayerMotor.speedMultiplier`, which already exists and is already respected.
**Done when:** you can tell someone is hurt without looking at a number.

### Step 5 · Downed and bleed-out
0 HP puts you on the floor with a 90-second clock instead of ending anything.
Movement and interaction locked, voice unaffected.
**Done when:** the clock reaching zero does something distinct from dying.

### Step 6 · A downed player is a `Carryable` ★
**The step this phase exists for.** 70 kg, Massive, two hands, no jumping,
counts against the load exactly like cargo.
**Done when:** you can pick up a downed crewmate, feel the speed penalty, and
watch the load gauge go amber as you do.

### Step 7 · Revive — ⏭️ MOVED TO PHASE 4 (21 Aug 2026)

Med spray revives in place at partial health.
**Done when:** somebody sprays somebody else back onto their feet.

**CARRYING CUT, 26 Aug 2026.** It did not survive the netcode. Step 4 made
every body owner-authoritative, so a carrier's hands filled correctly and the
body never moved — the downed player's own machine went on reporting the floor
it was lying on. Making it work means handing the carrier temporary ownership
of another player's body, which would stop "a body I own" meaning "my player"
for every input gate, camera binding and health write in the project.

**The cost, honestly:** there is one way to save someone now and it costs
money. With an empty kit, a downed crewmate is lost. That makes the 35 closer
to mandatory than optional — a change to the economy, not just the controls.

**Deferred because it cannot be tested solo.** Both ways of saving someone
need a second player — you cannot spray yourself, and carrying yourself out
is not a thing. Building it now would mean shipping a rescue verified only by
reading the code, in the one phase whose entire point is the moment somebody
gets saved.

**Already done, and staying:** `DownedPlayer.Revive()` restores you at 20 HP
(Critical — still limping, one mistake from going down again), drops you out
of your carrier's arms via `PlayerCarry.ForceDrop`, and destroys your
`Carryable` so you stop being luggage. What Phase 4 owes is the med spray as
a shop item, the use interaction, and a real second player to prove it.

### Step 8 · Lost
Bleed-out completes → Lost. Removed from the run, named on the results screen,
absent from the next one.
**Done when:** a run can end with someone missing and the game says who.

### Step 9 · Rescue contract — ⏭️ MOVED TO PHASE 4 (21 Aug 2026)

`Rescue(R, f) = Mafia(R) × (1 + f/10)` in the shop, partial payment carried
over, three deaths ends the campaign.
**Done when:** paying for a friend visibly costs you depth.

**Deferred because solo it is not a decision.** This step exists to create
one moment — *"the crew spends two rounds deciding, every single time they
open the shop, whether the rope matters more than their friend"* — and that
needs the run to be able to continue **without** paying. With one player,
being lost means pay or the campaign ends: mandatory, therefore not a choice.
Partial payment carrying over is untestable for the same reason, since you
cannot earn the rest of the money while you are the one who is missing.

**Already done, and staying:** `Campaign.LostCrew` records who and on which
floor, which is what the formula reads. Each entry carries a `paid` field
that nothing touches yet, so the debt is a running total from the moment it
is created rather than a price computed at the till.

**Known before it starts:** ECONOMY's table says a round-5 loss on room 4
costs 372; the formula gives 370. The round-10 row (823) matches exactly, so
it is a rounding slip in one row, not a different formula. Resolve it against
the formula when this is built.

### Step 10 · Cable fray
Overload wears the cable, fray is visible above the car, a snap ends the run,
the patch kit repairs it.
**Done when:** you look up at the cable before pressing GO.

---

# PART 5 — THE FOUR RULES (unchanged)

1. **One step per session.**
2. **Explanation before code.**
3. **Commit after every step.**
4. **Read before write.**

And the two things it still cannot do: **drag things in the Unity editor** (so
prefabs get built by editor scripts — that has worked every time this project
has needed it), and **see your game** (so keep sending screenshots; every
Phase 1 layout bug was found in one).
