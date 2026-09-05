# SAFE DEPOSIT — Phase 5: The room kit

*Written 5 Sep 2026. 4 weeks · 21 Dec 2026 – 17 Jan 2027.*

**Done when:** ten generated floors a stranger can navigate without a map.

---

# PART 1 — WHAT THIS PHASE IS FOR

Phase 1 built one floor and repeated it twenty times. That was correct then —
it proved the lift, the doors and the descent. It is the reason the game
currently has no reason to look at the second floor, or the fifth.

This phase turns that one floor into a **generator**: a small set of modules,
a rule for arranging them, and enough variation that a player forms a memory
of *this* building rather than a memory of *a* corridor.

## What is different about this phase

Phase 4 was invisible work — nothing on screen changed and everything
underneath did. Phase 5 is the opposite. Every step is visible the moment it
lands, which makes it far easier to fool yourself: a floor that *looks*
generated and is actually the same three rooms shuffled reads fine for one
playthrough and dies on the second.

So the done-when is deliberately about a **stranger**, not about the
generator. "It produced ten different floors" is a property of the code. "A
stranger navigated them without a map" is a property of the game.

## Verbs come first, and that ordering is not negotiable

`INHABITANTS.md` Part 5 puts push, Q-drop and Q-throw in this phase and says
why: *"verbs first. Every inhabitant below assumes push exists."* Four of the
demo seven have an answer that is "push it". Build the creatures before the
verb and you build them twice.

Push is mostly built already. The throw is not built at all.

---

# PART 2 — WHAT ALREADY EXISTS

Worth being exact, because two of these are further along than they look and
one is further behind.

| | state |
|---|---|
| `Grayboxbuilder` | builds 20 identical floors: shaft 14m, floor height 5m, room depth 6m, door 2×2.5m |
| Door on a different side per floor | **already working** — Phase 1 built it |
| `RoomSeal` | seals a doorway with rubble, and knows if a player is inside |
| `LootSpawner` | tiered spawning, 60 items across 20 floors, host-published roster |
| Downed-player carrying | **done in Phase 2** — a person is 70kg of Massive cargo and every system already handles them |
| `PlayerPush` | built, networked, and **intermittently misses** — see Step 3 |
| Q drop / Q throw | **nothing** |
| Room modules, sockets, generator | **nothing** |

The survivor is the cheapest thing in this phase and people will assume it is
the most expensive. It is a `Carryable` on a body that cannot walk, and Phase 2
already made a person 70kg of cargo without any special case.

---

# PART 3 — BUILD ORDER: EIGHT STEPS

Ordered so that nothing is built twice. Verbs, then the contract, then the
things that fill it, then the thing that arranges them.

---

### Step 1 · Q — put it down ✅ DONE

**E is for taking. Q is for giving up.** Separating them fixes something that
has always been slightly wrong: one key doing both means a mistimed press
picks up what you just dropped.

Tap Q places the held item **gently, where you stand** — not thrown, not
dropped from chest height. It should land flat and stay put, because the
elevator load gauge counts what is physically in the car and a crate that
rolls back out of the doors is a bug that will be reported as "the lift lost
my loot".

`PlayerCarry` already owns hold state and `Carryable.Drop(velocity)` already
exists, so this is a second binding and not a system — but note what `Drop`
does today: it assigns the carrier's velocity, deliberately, *"so dropping
something while running throws it rather than parking it in mid-air."* Tap Q is
`Drop(Vector3.zero)` plus a placement in front of the feet. Do not change what
`Drop` does; call it differently.

**Done when:** you can set a crate down inside the lift and it is still there
when the doors close.

---

### Step 2 · Q — hold to throw ✅ DONE

Hold Q to wind up, release to throw. **The heavier it is, the longer the
wind-up and the shorter the throw.** A can crosses a room; a crate goes about
two metres and lands hard; a vending machine cannot be thrown at all and the
wind-up simply never completes.

The numbers for this already exist and should not be re-invented:

- `Carryable.Mass` is the rigidbody's real mass — use it for the continuous
  scaling of wind-up time and throw distance
- `Carryable.Weight` is the class derived from it, and the thresholds are
  already drawn where this feature needs them: **Small ≤ 8kg, Heavy ≤ 60kg,
  Massive above.** `Massive` is the "wind-up never completes" case, and it is
  the same line that already stops you jumping and drops you to 0.45 speed
- A downed person is 70kg, so **Massive**, so a person cannot be thrown — for
  free, from a threshold that was drawn for another reason entirely

That last one is worth pausing on: the weight classes were set in Phase 2 for
speed and jumping, and they land exactly right for throwing. If a fourth
system disagrees with them later, the classes are wrong, not the system.

Throwing **damages nothing and loses no value**. This game already punishes
greed with weight; it does not need to punish it with breakage.

What the throw is *for*, in the order that matters:

1. **Loot into the lift from the doorway**, without walking it in — saves
   seconds, and seconds are the resource
2. **Noise, deliberately** — a can thrown down a corridor is a cannibal sent
   somewhere else. The first real counterplay that is not hiding
3. **At the thief** — slow, unreliable, satisfying

Reuse `PlayerPush`'s trajectory solver from Phase 4: it already turns a
distance and a height into a velocity, and a throw is the same arithmetic
aimed by the camera instead of by two body positions.

**Done when:** you can stand in a doorway and land a crate in the lift, and a
vending machine cannot be thrown at all.

> **Tuned in play, 5 Sep 2026.** Throws land at **4m for a light thing and 2m
> for the heaviest throwable one**, at a fixed 20 degree launch. Settled by
> feel across three passes — nine metres read as flinging loot away, two was
> too short to clear anything, four is the placement throw.
>
> The 2m heavy figure is Part 4's own number restored: *"a crate goes two
> metres and lands hard."*
>
> One thing this still does not reach, recorded rather than absorbed: **use 2
> above — noise, deliberately.** Four metres is not "a can down a corridor",
> so sending a cannibal to the far end of one — *"the first real counterplay
> in the game that is not hiding"* — is not buildable at this range.
>
> That costs nothing yet and a number gives it back: `throwDistanceLight` is the
> dial, and the distraction only needs it raised for Small items, which is
> exactly the class a can is in. Revisit when the cannibal is built, not
> before.

---

### Step 3 · Make the shove reliable ✅ DONE

Carried from Phase 4's KNOWN ISSUES. Push works and then occasionally does
not, from what looks like the same position. Four faults have already been
found and fixed under it, each of which looked like the answer at the time —
so the next person to touch it should **instrument before changing anything**.

Three things have never been checked:

1. `contactAt` is 0.341, so the probe fires a third of a second AFTER the
   keypress. Either player moving in that window loses the target. Locking the
   target at swing start rather than re-probing at contact would settle it.
2. Whether `hit.rigidbody` comes back null against a wall, leaving the overlap
   to do all the work.
3. Whether the shove lands and is lost on the observer's side only — the
   victim's body is kinematic there and driven by `NetworkTransform`.

**Done when:** twenty shoves from twenty positions all connect.

> **Instrumented first, 5 Sep 2026.** One `[PUSH]` line per swing, naming what
> was in front of the eye at the keypress and at contact, and — when those
> disagree — the distance, the cone dot, how far the target moved in between,
> and whether `Pushable.Allows` refused it.
>
> Worth stating before reading any of it: contact firing 0.341s after the
> keypress is **deliberate**, and `PlayerPush` carries a comment saying so —
> *"somebody who steps out of the way during the wind-up actually gets away
> with it."* A moving target escaping is the feature. The question this log
> answers is whether the reported misses are that, or something else wearing
> the same clothes.
>
> Two bugs earlier today were each settled by one log line after several rounds
> of reading source had settled nothing. That is why this step starts here.
>
> **Answered in twelve swings.** Every one found a target; nine found the
> **ELEVATOR**. So it was never the timing, the reach or the network — the
> three leads carried since Phase 4 — it was target selection.
> `Pushable.Allows` ends with "pushable unless it is loot", and the lift has a
> rigidbody, no `Carryable` and no `Pushable`, so it fell through to `true`.
> Standing in the car, the spherecast reached a wall before it reached the
> person. Outside the lift it always worked; inside it never did, and the lift
> is where two players naturally stand.
>
> Fixed twice over: the lift is refused by what it *is*
> (`GetComponentInParent<Elevator>()`), and — the general fix — **a person now
> beats scenery even when scenery is nearer**, which also covers a doorframe, a
> railing, or a friend backed against a wall.
>
> Reach cut to **1.0m** afterwards, on request. That is the floor: both players
> carry a 0.42 capsule, so two people in contact are 0.84m apart and can never
> be nearer. Below ~0.9 the shove does not get tighter, it stops working.
>
> Known trade, left deliberately: **no line-of-sight check.** Every symptom in
> this bug's history was a false negative, and adding a filter that can produce
> more of them is the wrong move. If shoving through thin walls turns out to
> matter, that is a raycast added with evidence.

---

### Step 4 · The room module contract

The most important step, and the one with nothing to look at. Everything after
it is cheap or expensive depending entirely on getting this right.

A module is a prefab with **tagged empty transforms**:

| socket | what fills it | phase |
|---|---|---|
| `LootAnchor` | `LootSpawner` places an item | 5 |
| `LockAnchor` | a door, keypad or shutter | 5 |
| `HazardAnchor` | the eyeless, a trap | 6 |
| `SurvivorAnchor` | a downed body to carry out | 5 |
| `PuzzleAnchor` | a mechanism half | 6 |

Rules that keep the generator honest:

- A module declares its sockets; it never decides what goes in them
- Every module fits the same doorway footprint, so any module can follow any
  other. `Grayboxbuilder` already fixes that at 2×2.5m
- A module must be navigable with **all** its sockets empty, or a floor that
  spawns no loot becomes impassable

`PUZZLES.md` needs mechanisms split across **two rooms** — a button in A
opening a shutter in B, two dials in different rooms, a key board and a lock.
So the generator must be able to name rooms and place a paired anchor in each.
Design that in now; building it is Phase 6.

**Done when:** a module can be dropped into the scene, and a script can list
its sockets without knowing what module it is.

> **Built 5 Sep 2026, not yet compiled.** `RoomSocket` (kind + `pairId`) and
> `RoomModule` (role, socket queries, `Problems()`), plus
> `RoomModule.SocketsUnder(Transform, Kind)` — which takes a plain `Transform`
> on purpose, so the twenty `Level_NN` floors standing today answer "no
> sockets" rather than needing a null check at every call site.
>
> `LootSpawner` now asks the room first and falls back to its hardcoded
> `Slots` array when the room says nothing. **Both paths are permanent for
> now, deliberately** — a migration that requires every floor to be converted
> before anything runs is one that gets abandoned halfway. When Steps 5 and 6
> build the modules, the same call starts answering and `LootSpawner` does not
> change.
>
> `pairId` is spent early on purpose. Nothing reads it until Phase 6, but every
> puzzle in `PUZZLES.md` is split across two rooms, and retrofitting pairing
> into a socket system that already has rooms authored against it is the
> expensive version of this decision.
>
> **Rule 3 is not validated and will not be.** "Navigable with every socket
> empty" is a claim about pathing; a validator guessing at it would be wrong
> in both directions. `Tools → Rooms → Validate Modules In Scene` reports what
> it can and says plainly that this one is on the human.

---

### Step 5 · Six modules

Built to the contract, on the existing graybox proportions.

Landing → main → side → back is the fixed shape, so the six divide as:

- **2 landings** — where you step out of the lift. Must read as "the way back"
  from anywhere on the floor
- **2 mains** — the room the landing opens into, largest, most loot
- **1 side** — off the main, optional, where a lock or a survivor lives
- **1 back** — the dead end, best loot, worst place to be caught

Greyboxed only. Materials, props and lighting are Phase 8.

**Done when:** each of the six can be walked end to end with no socket filled.

> **Built 5 Sep 2026.** `Tools → Rooms → Build Six Modules` writes them to
> `Assets/_Project/Prefabs/Rooms/`. Greybox only — materials, props and
> lighting stay in Phase 8.
>
> **The frame is the load-bearing decision.** A module's origin is its own
> doorway, on the floor, with +X into the room. An exit is that same frame
> pointing outward — so attaching B to an exit of A is "put B's origin on the
> exit and match rotation", for any A and any B, with neither knowing about
> the other. Get this wrong and Step 6's generator becomes a table of special
> cases about which rooms fit which, which is the version that never finishes.
>
> `RoomExit` was added here, not in Step 4 — the contract said *"any module can
> follow any other"* and I built sockets without the thing that makes following
> possible.
>
> Caught before it shipped: the first shell built four solid walls and cut a
> doorway only at the front, so the main room's two exits pointed into
> concrete. **An exit is a promise you can walk through it**, and nothing in
> the code was keeping it. Walls are segmented around their openings now, so a
> module cannot declare an exit the shell does not know about without it being
> obvious the moment you walk in.
>
> Still on the human, and still not checkable: **walkable with every socket
> empty and every exit sealed.**

---

### Step 6 · The floor generator

Picks modules and arranges them per floor. Landing, main, then side and back
where the module's exits allow.

Two rules stop it producing nonsense:

- **Never the same main twice in a row.** A player descending should never see
  the same room shape on consecutive floors — that is the single strongest
  signal that a building is generated
- **The door side is already decided** by Phase 1's per-floor rotation. The
  generator must respect it, not re-decide it

Deterministic from a seed, and the seed is **published by the host** exactly
as `LootSpawner`'s roster is, so every machine builds the same building
without sending geometry.

**Done when:** ten floors in a row, and you can tell them apart from memory.

> **Built 5 Sep 2026.** `FloorLayout` (pure function) + `FloorGenerator`
> (walks exits) + `Tools → Rooms → Preview Ten Floors`.
>
> **The seed is `Campaign.RunNumber`.** It is already a host-owned
> `NetworkVariable<int>` every machine agrees on, so four machines build the
> same building with no geometry on the wire and no second thing that can
> disagree. A new seed variable would have been a second source of truth for
> "which run is this" — the mistake Phase 4 spent seven weeks undoing across
> 59 statics.
>
> `System.Random`, never `UnityEngine.Random`: the latter is global mutable
> state, so its next value depends on how many times anything else in the
> process has rolled. Determinism that depends on call order is not
> determinism.
>
> **Placement is one line** — `SetPositionAndRotation(exit.Position,
> exit.Rotation)`. No offsets, no table of which room fits which. That is what
> Step 5's frame bought.
>
> Caught by checking rather than by reading: the no-repeat rule **did not
> work**. It compared each floor's main against the previous floor's *raw*
> draw, but that draw may itself have been nudged — so it compared against a
> room that was never built, and repeats survived at about the rate of having
> no rule at all. Replaced with a **stepping walk**: each floor moves 1..n−1
> places around the set, and a step of at least one cannot land where it
> started. Verified zero repeats over ten floors at both 2 and 3 mains.
>
> That check then exposed a design problem: **with two mains the rule forces
> A,B,A,B**, which reads as generated just as loudly as a repeat. Two of
> anything alternates. So there is a **third main** now — a columned hall —
> making seven modules, not six. The spec said six; six was wrong.
>
> **Revised after looking at it.** Ten previews side by side were ten
> identical silhouettes: the generator always built landing → main → side →
> back, so the only variety was *which* main — a partition wall you have to
> walk inside to see. **A player does not remember which main room they were
> in. They remember that the fourth floor went on forever and the fifth was a
> cupboard.**
>
> So the spine is 1–4 mains, side rooms hang off any of them, the dead end is
> optional, and **a floor may use the same main more than once** — two halls
> end to end read as a long floor, not a bug. Two to nine rooms per floor
> instead of a fixed four. Only *adjacent* repeats are avoided, and only the
> first main carries the cross-floor rule, since that is the room you just
> left.
>
> And: **unused exits are now sealed.** Modules cut an opening for every exit
> they declare, but the generator fills only some — so a spine that ended at a
> main left a doorway onto the skybox. A door that leads nowhere is worse than
> no door: a player walks to it, finds nothing, and stops trusting doors. This
> also makes "walkable with every exit sealed" automatic rather than a rule
> the human has to remember.
>
> **Rooms made long, 5 Sep 2026.** The first set were 10×12, 7×7, 6×8 — near
> square, and a near-square box has no direction: you walk in, see the whole
> thing, and leave. Depth is roughly twice width now (main 20×10, back 14×8,
> side 10×5, landing 11×7), so a floor runs **31m to 105m** along its spine.
>
> A long room has a far end — somewhere the light does not reach, somewhere
> loot is worth the walk, and somewhere a thing can get between you and the
> door. None of that exists in a square, and every inhabitant in Phase 6
> assumes it.
>
> **Made to branch, 5 Sep 2026.** A main now offers **left, right and straight
> on**, and the generator spends its room budget breadth-first across those —
> so a floor is a **tree**, not a corridor.
>
> That is not decoration. Branching is the reason a crew splits up, which is
> the reason they have to talk to each other, which is what the Phase 4 voice
> work was for. **A corridor needs no radio.**
>
> Breadth-first on purpose: depth-first spends the whole budget down one arm
> and produces a long thin floor with a stub, which is a corridor again with
> extra steps.
>
> Branching brought a hazard a spine could not have: **two arms turning toward
> each other and overlapping.** Rooms are placed, measured, and removed again
> if their footprint hits one already there — the exit is then left unused and
> sealed, so a rejected branch becomes a wall rather than a hole. Placing
> first is not laziness: a module's footprint depends on its rotation, and the
> honest way to know where it lands is to put it there.

---

### Step 7 · Doors, keys, locked states

A locked door is the first thing that makes a floor a place rather than a
corridor. It needs to be **visibly** locked, because a door you cannot open
and cannot tell is locked is a bug report.

- Locked, unlocked, and jammed-open states
- A key is a `Carryable` — so it can be carried, dropped, thrown to somebody
  across a gap, and lost. That falls out of Steps 1 and 2 for free
- `LockAnchor` decides which doors on a floor are locked

**Done when:** a door is locked, a key exists somewhere on the floor, and the
crew can open it — including by throwing the key.

---

### Step 8 · Survivors

A survivor is a downed person who did not come with you. Phase 2 already made
a downed player 70kg of Massive cargo that every system handles without
knowing it is a person.

- Placed at `SurvivorAnchor`
- Carried out exactly like a downed crewmate
- Worth something at the top, which `ECONOMY_AND_CAMPAIGN.md` costs

The design question this raises, and it is worth answering now: **a survivor
competes with loot for the same arms.** That is the point. Two hands, a quota,
and a person who will not walk is the same argument as cable versus your
friend, one floor lower.

**Done when:** a survivor can be found, carried up, and banked.

---

# PART 4 — WHAT IS EXPLICITLY NOT HERE

- **Puzzles** — Phase 6. This phase builds the anchors they hang on
- **The eyeless, the cannibal, the passenger** — Phase 6, and all three assume
  push and the room kit exist
- **The thief, the seller, the gun** — Phase 7, economy pieces first
- **Materials, props, lighting, audio** — Phase 8
- **Floors beyond ten** — Phase 9

---

# PART 5 — THE RULES THIS PHASE INHERITS

Carried from Phase 4 and earned the hard way:

1. **A symptom that is asymmetric** — works on the host, fails on the client,
   or only slot 0 is wrong — **is a replication or identity problem.**
2. **A symptom that works once and then stops is a cached answer going stale.**
3. **When two fixes have failed, stop reasoning about the code and log the
   actual numbers.** Every hard bug this project has had was found by
   measurement and none by argument.
4. **A serialized value and a code default are different things.** Changing one
   says nothing about the other. Check the prefab.
