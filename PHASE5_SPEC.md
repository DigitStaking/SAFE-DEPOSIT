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

### Step 1 · Q — put it down

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

### Step 2 · Q — hold to throw

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

### Step 3 · Make the shove reliable

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
