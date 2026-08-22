# SAFE DEPOSIT — The Elevator

Replaces the rope-sliding and hook systems. This document is the complete spec
plus the step-by-step build order, written so it can be handed to Claude Code
one step at a time.

---

# PART 1 — WHAT THIS CHANGE COSTS AND WHAT IT BUYS

You should go in with your eyes open, so here it is straight.

## What dies

- **Swinging and the pendulum.** The tether, the 2.5 m arc, the leap.
- **Summed pulls.** Four players moving one rope together — the emergent co-op
  physics we designed the whole thing around.
- **The rope traffic jam.** Cargo blocking the climb, the Traverse move, "who
  put the vending machine at 6 metres."
- **The hook / Q pin.** Kinking the rope into a doorway.
- **Rope tug signals.**

That was the most *distinctive* thing in the design. Nobody else has it.

## What survives untouched

- **The weight limit.** Crew + cargo + survivors on one gauge. This is the
  actual core argument — *this person or the gold* — and the elevator carries it
  perfectly.
- **Shared fate.** You're all in one box together.
- **The 10-minute room clock and the demolition.**
- **All 25 puzzles.** Completely unaffected.
- **The economy, the mafia, the survivors, the documents.**

## What you gain — and this is the serious part

- **~6 weeks of schedule.** The Verlet rope was Block 1, five weeks, and the
  highest-risk technical work in the project.
- **Netcode gets dramatically easier.** A moving platform is a position and a
  state. A 32-node simulated rope over the network is the single hardest thing
  we had planned. This might be the difference between shipping in June 2027
  and not shipping.
- **No climb tedium**, which was a real late-game problem you spotted yourself.
- **A social space.** Four people in a lit box with a weight gauge, a price
  list, and a floor to argue about is a *scene*. The rope never gave you that.

## My honest verdict

**On production grounds this is probably the right call.** You were stacking the
two riskiest systems in the project — simulated rope and netcode — on top of
each other, with one solo developer and a fixed Next Fest deadline.

But be clear about the trade: **the rope was your differentiator.** Without it
the game moves closer to Lethal Company's shape — a hub you return to, rooms you
loot. So the elevator has to carry the identity now, which means it cannot be a
grey box with buttons. The load gauge, the price scanner, the four doors, the
bridge that retracts while someone is still in the room — **those are the game
now.** Build them like they matter.

## One thing to keep: the cable

The elevator hangs from the winch on a **visible steel wire rope** — braided
strand over a grooved sheave, the real thing lifts do. Keep it, because it's
nearly free and it keeps four things alive:

- The load limit has a physical object attached to it
- The cable can **fray** under overload — your best trap survives
- The shaft still reads as a rope-drop, which is your existing art
- **It is what the shop sells.** Decided 18 Aug 2026.

### The hoist rope IS the progression

More wire rope on the drum = the car reaches a deeper floor. This is the same
role the old climbing rope had, so the economy is unchanged — `RopeCost(R) =
80 × g^(R-1)`, max 2 buys a round, one buy = one floor.

What changes is what you're looking at when you spend the money. You are not
buying abstract "depth", you are buying **more steel on the winch drum**, and
the drum is visible from inside the car. A player who has bought nothing this
round can see how little is left.

`Campaign.RopeLength` / `RopeChunk` / `BuyRope()` already model exactly this and
keep working untouched. **Rename them to Cable* in Step 12**, with the rest of
the constants — not before, or every step in between has to be re-tested for a
change that alters no behaviour.

---

# PART 2 — THE ELEVATOR SPEC

## The car

A square steel cage, roughly **4 × 4 m** inside, hanging from the cable.

**Four sides, each with a shutter door.** Only one opens per floor — but *which*
one depends on the floor, so arriving somewhere means orienting yourself.

### Inside the car

| Feature | Where | What it does |
|---|---|---|
| **Dashboard** | one wall | floor selection, press **F** to use |
| **Cargo deck** | the floor, marked out | where loot goes; counts toward load |
| **Price scanner** | a small station | hold an item to it → value and mass |
| **Load gauge** | above the dashboard | green / amber / red |
| **Cage light** | ceiling | the only reliable light in the game |

## The dashboard

Press **F** near it. The camera moves in, the crosshair becomes a cursor, and
movement locks. Press **F** or **Esc** to step back.

```
┌──────────────────────────────┐
│   FLOOR   [  1 2  ]   GO     │   type a number, press GO
│                              │
│         ▲   UP               │   one floor at a time
│         ▼   DOWN             │
│                              │
│  01 ■  02 ■  03 ■  04 □      │   ■ reachable
│  05 ✕  06 □  07 □  08 □      │   □ beyond your cable
│                              │   ✕ demolished
│   LOAD  ▓▓▓▓▓▓▓░░░  412/550  │
│                              │
│      [ RETURN TO SURFACE ]   │   the big red one
└──────────────────────────────┘
```

**The floor list is a map, a progress bar and a graveyard in one object.** Players
will look at it every round, and watching floors turn red is the demolition made
visible.

## Movement

| Input | Speed | Feel |
|---|---|---|
| Type a floor + GO | **fast** — ~8 m/s | a whoosh, a few seconds, lights strobing past |
| UP / DOWN buttons | **slow** — ~2 m/s | one floor, deliberate |

- **Doors lock while moving.**
- **It will not move above twice its rated load.** "WINCH STALLED" — the drum
  physically cannot lift it, so no amount of arguing changes the answer.
- **Between capacity and that ceiling it DOES move, and the cable frays.**
  *(Resolved 21 Aug 2026, Phase 2 Step 10.)* This line used to read "it will
  not move while overloaded", which contradicted the fray trap ten lines
  above: a car that never moves overloaded can never fray under overload.
  Overload is now a cost you defer and pay in rope, which is what makes it
  "the only place in the demo where greed kills you directly".
- It is **loud**. Anything down there hears it arrive.

## The bridge

The car stops. The shutter facing the room rolls up. **A steel bridge extends
from that side to the doorway** over about two seconds — mechanical, noisy,
unmistakable.

**Selecting another floor retracts it, with a 5-second countdown and an alarm.**

That countdown is now the most tense object in the game. Somebody is still in the
room. Somebody at the dashboard has their finger on GO. The clock is running.
It replaces the ascent vote's *"Karim is not on the rope"* with something
physical, visible, and cruel.

**Rule: the bridge cannot retract while a player is standing on it.** Being *in
the room* is fair game. Being on the bridge is not — that's a bug, not a moment.

## Load

```
Load = (70 × players inside) + cargo on deck + survivors inside
```

Same 550 kg base, same +50 kg upgrades, same everything from
`ECONOMY_AND_CAMPAIGN.md`. Nothing in the economy changes.

## The price scanner

Hold an item to the station: **value, mass, and $/kg**. That last number is the
one that matters, and putting it on a physical machine inside the car means the
"is this worth the space" argument happens out loud, in one room, with everyone
watching.

*(This makes the Value Appraiser shop item redundant — delete it. Or keep the
scanner basic and let the upgrade reveal true value for fakes.)*

## Return to surface

The big red button starts the ascent vote. Everyone must be **inside the car**.
The dashboard names whoever isn't:

> **CANNOT DEPART — KARIM IS NOT ABOARD**

Doors close, the car rises, the shop opens.

---

# PART 3 — WHAT TO DELETE, CHANGE, AND KEEP

## Delete entirely

| File | Lines | Why |
|---|---|---|
| `Playertether.cs` | 987 | tether, swing, leap, reclip — all gone |
| `RopeHook.cs` | 308 | the Q pin has no meaning now |
| `Mainrope.cs` | 624 | replaced by `ElevatorCable.cs` (~150 lines) |
| `PlayerProceduralAnim.cs` | 94 | superseded by real animation |
| `Playerarms.cs` | 147 | superseded by `FirstPersonHands` |
| `Editor/AnimationSetupTool.cs` | 312 | legacy, can overwrite the good controller |

**~2,470 lines removed.** That is the single healthiest thing you can do to this
codebase.

## Change

| File | What changes |
|---|---|
| `Playermotor.cs` | remove tether checks in jump/air control |
| `PlayerCarry.cs` | remove `ClipToRope` / `NearRope`; add "place on deck" |
| `Carryable.cs` | remove `OnRope`, `ClipToRope`, `UnclipFromRope`, `ropeDepth`; add `OnDeck` |
| `RunManager.cs` | extraction = elevator reached surface, not depth < 0.6 |
| `PlayerAnimatorDriver.cs` | remove `Climbing`, `ClimbSpeed`, `ClimbDir` |
| `Editor/AnimatorBuilder.cs` | remove the Climb state and the ClimbUp / Hang slots |
| `FirstPersonHands.cs` | remove the `Climbing` free-arms check |
| `Editor/Grayboxbuilder.cs` | rebuild: shaft + **one room per floor** + elevator |

## Keep as is

`Campaign.cs` (retune constants only) · `PlayerBackpack.cs` ·
`FirstPersonCamera.cs` · `LocalFirstPersonBodyCull.cs` · `PlayerSkin.cs` ·
`PlayerHeadlamp.cs` · `LightShaft.cs` · `RealisticSmokeVolume.cs` ·
`SceneAtmosphere.cs` · `RoomSeal.cs` · `RunHudGate.cs`

## New files

`Elevator.cs` · `ElevatorDashboard.cs` · `ElevatorBridge.cs` ·
`ElevatorDeck.cs` · `PriceScanner.cs` · `ElevatorCable.cs`

---

# PART 4 — BUILD ORDER: TWELVE STEPS

**One step per session.** Do not let anyone — including me — do more than one at
a time. Each step ends with a game that runs and a commit.

### Step 1 · Safety
Commit everything. Tag `v0.1-rope-era`. This is the last save point where the
rope exists, and you may want to look at it again.

### Step 2 · Demolition
Delete the six files in the "delete entirely" list. Fix every compile error by
*removing* the calling code, not by patching around it.
**Done when:** the project compiles and a player can walk, jump and fall.

### Step 3 · The car, static
Build the elevator prefab: 4 × 4 m cage, four shutter doors, deck markings,
dashboard panel, ceiling light. **No movement, no code.** Place it in the shaft.
**Done when:** you can walk into it and it looks like somewhere you'd argue.

### Step 4 · Movement
`Elevator.cs` — moves between fixed floor heights. Hard-code a key to go up and
down for now. Doors lock while moving.
**Done when:** it moves smoothly and the player rides it without falling through.

### Step 5 · Dashboard, part one
`ElevatorDashboard.cs` — press **F** to zoom in, cursor appears, UP and DOWN
buttons work, **F**/Esc exits.
**Done when:** you can drive the elevator entirely from the panel.

### Step 6 · Dashboard, part two
Numeric entry + GO, fast travel at ~8 m/s, and the floor list with reachable /
locked / demolished states.
**Done when:** typing `07` and pressing GO takes you there fast.

### Step 7 · The bridge
`ElevatorBridge.cs` — extends on arrival, retracts on departure with a 5-second
countdown and an alarm. Cannot retract while a player stands on it.
**Done when:** the countdown makes you nervous.

### Step 8 · Cargo and load
`ElevatorDeck.cs` — items placed on the deck register their mass. Load =
players + cargo + survivors. Gauge on the dashboard. Overload blocks movement.
**Done when:** four players plus a full haul cannot move, and you can see why.

### Step 9 · Price scanner
`PriceScanner.cs` — hold an item, see value / mass / $ per kg.
**Done when:** you can decide what to leave behind without leaving the car.

### Step 10 · Return to surface
The red button, the vote, "X IS NOT ABOARD", and `RunManager` extraction
rewritten to trigger on the car reaching the top.
**Done when:** a full round can be played start to finish.

### Step 11 · The graybox rebuild
`Grayboxbuilder.cs` — shaft with **one room per floor**, doors positioned on
whichever of the four sides that floor uses, elevator placed and wired.
**Done when:** ten generated floors are navigable.

### Step 12 · Economy retune
The eleven constants from `DEMO_PLAN.md`, plus the cable load limit at 550.
**Done when:** three rounds feel tight.

---

# PART 5 — HOW TO WORK WITH CLAUDE CODE ON THIS

**Yes, it can do this**, and this document is deliberately shaped for it. But
the way you ask matters more than what you ask.

### The rules that make it work

**One step per session.** Paste this document, then say: *"We are on Step 4.
Only Step 4. Do not touch anything else."* Without that it will helpfully build
Steps 4 through 8 and you will have no idea which part broke.

**Ask for the explanation before the code.** *"Explain what you're going to
change and why, then wait."* You have been doing this all along and it's why you
can now debug your own project. Keep doing it.

**Commit after every step.** Not every session — every step. When something goes
wrong you want to lose twenty minutes, not two days.

**Make it read before it writes.** *"Read Elevator.cs and RunManager.cs first,
then tell me what depends on what."* An agent that hasn't read your code will
confidently invent a version of it.

**Test in play mode after each step, and say what you saw.** "The bridge extends
but the player falls through it" is worth more than any amount of code review.

### What to paste at the start of a new session

> Read `ELEVATOR_SPEC.md`, `ECONOMY_AND_CAMPAIGN.md` and `DEMO_PLAN.md`.
> The project is at `C:\Users\Digitstak\SAFE DEPOSIT`.
> We are on **Step N**. Do only Step N.
> Explain what you'll change and why before writing anything.

### Where it will struggle

- **Unity scene and prefab work.** It cannot drag things in the editor. Steps 3
  and 11 need you, or an editor script that builds the prefab in code — which is
  the approach that has worked well on this project already.
- **Anything visual.** It cannot see your game. Screenshots have been doing the
  heavy lifting all session — keep sending them.
- **Knowing when to stop.** It will always offer one more improvement. The step
  list exists to say no for you.

---

# PART 6 — WHAT THIS DOES TO THE SCHEDULE

| Block | Was | Now |
|---|---|---|
| Rope rewrite | **5 weeks** | **deleted** |
| Mass, health, downed | 5 weeks | 4 weeks — no cargo bands or Traverse |
| Elevator (Steps 3–11) | — | **4 weeks** |
| Netcode | 10 weeks | **7 weeks** — a platform is far simpler than a rope |

**Net: roughly 5 weeks recovered.** That takes your buffer before the 31 May
2027 deadline from three weeks to about eight — which is the first point in this
whole plan where the schedule has looked survivable.

That, more than anything about the design, is the strongest argument for doing
this.
