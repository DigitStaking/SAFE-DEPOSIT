# SAFE DEPOSIT — WHO ELSE IS DOWN THERE

*Started 30 Aug 2026, from a design conversation.*

---

# PART 1 — THE RULE EVERY INHABITANT HAS TO PASS

This building already runs on five pressures, and each of them is a resource
the crew is spending:

| pressure | what it costs |
|---|---|
| **mass** | 550 kg. A survivor is loot you did not take |
| **cable** | how deep you can go, bought in rope |
| **time** | a room seals every 10 minutes, forever |
| **light** | a headlamp is how you see and how you are seen |
| **the crew** | four people who cannot be in four places safely |

**An inhabitant earns its place by pressing on one of those, not by adding a
sixth.** A monster with its own health bar and its own resistances is a
different game; a monster that makes you turn your lamp off is *this* game,
because the lamp is already a thing you own and now it is a thing you have to
decide about.

The test, applied to everything below: **name the resource it taxes.** If the
answer is "the player's reflexes", it does not belong here.

---

# PART 2 — WHAT YOU ASKED FOR

## The fat man — ALREADY DESIGNED, needs building

ECONOMY Part 6 has him at **140 kg, value 0**. That is four crates and
**$100 — half a mafia payment** — and it is the single cleanest decision in
the game already written down:

> *A survivor doesn't cost you a trip. It costs you loot.*

**Taxes: mass.** Nothing to redesign. Phase 5 builds him, Phase 6 puts him
behind a puzzle like every other survivor.

## The thief — takes what you already earned

Steals from a pack or from the deck and runs. Not a fight; a **chase you
choose not to have**, because chasing costs the one thing you cannot buy back.

**Taxes: time, and the sunk cost of loot already carried.** It is the only
threat in the game that makes you angry rather than frightened, and that is a
useful second note.

Design notes:
- Steals **stowed** items, never held — losing what is in your hands reads as
  a bug, losing what is on your back reads as a theft
- Visible for a moment before it takes anything. A thief you never saw is
  indistinguishable from the game losing your items
- Drops what it stole when killed, so the gun has a use that is not defence

## The cannibal — 20 damage, charges on sight or noise

**Taxes: light and the radio.** It is the reason to go dark and the reason to
shut up, which makes two systems this project has already built into
decisions.

Design notes:
- 20 damage is exactly right: **five hits from full**, and Phase 2 gives no
  regeneration ever, so two hits in round 1 is a wound you carry for the rest
  of the campaign
- Hearing must include the walkie-talkie and proximity voice. A crew that has
  to stop talking is a crew that has lost its coordination, which is worse
  than losing health
- It should lose you in the dark. If it tracks perfectly, the lamp toggle
  stops being a decision

## The seller — rare, 30% off

**Taxes: cable and time.** He is deep, he is not on your route, and going to
him is a round you did not spend looting.

Design notes:
- 30% off is only meaningful against ECONOMY's scaled prices, so it stays
  worthwhile at round 40 exactly as at round 1 — the scaling already handles it
- He should also **buy**, at a loss. A crew 40 short of the quota with a
  vending machine they cannot lift now has a bad option instead of no option
- Rare, and never twice in a campaign on the same floor. He is a rumour

## The gun — and the thing to be careful about

A gun that removes threat turns this into a shooter, and the whole design is
built on not being able to fight. So it converts, like every other tool in
ECONOMY:

> *Every good tool converts one resource into another.*

**The gun converts money and NOISE into safety.** Each shot is heard on the
whole floor and brings whatever else is listening. Few rounds, expensive
rounds, and reloading is slow enough to be a decision rather than a reflex.

Fired in a corridor with a cannibal already coming, it saves you. Fired
because you were startled, it costs you the floor.

---

# PART 3 — THE DEMO SEVEN

*Decided 30 Aug 2026.* Seven inhabitants for the ten-floor demo. Chosen so
that **no two tax the same resource** — a crew that meets seven things which
all punish carelessness has met one thing seven times.

| # | who | taxes | the sentence it makes a crew say |
|---|---|---|---|
| 1 | the fat man | **mass** | "he's four crates. Do we want him?" |
| 2 | the seller | **cable + time** | "he's two floors past our rope" |
| 3 | the cannibal | **light + voice** | "lamps off. Nobody talk." |
| 4 | the thief | **loot already earned** | "he's got my bag — push him!" |
| 5 | the eyeless | **your lamp itself** | "I'll go lit. You three stay dark." |
| 6 | the tenant | **the radio** | "…is that you on the radio?" |
| 7 | the weight | **the way out** | "why won't it move?" |

Two of those are not threats — the fat man and the seller — and that is
deliberate. Seven monsters is a bestiary; five monsters, a burden and a
merchant is a **place**.

**And no two take the same thing from you:**

| | it takes |
|---|---|
| cannibal | **your health**, and your ability to coordinate |
| thief | **loot you already earned** |
| eyeless | **your sight** — the lamp, not the life |
| tenant | **your trust in your own crew** |
| weight | **your way out** |

Health is the *only* one of those a normal game would think of. That is the
point: four of the five threats never touch your HP bar, and are worse for
it.

---

## 3 · The cannibal — revised 30 Aug

**Fast.** Not shambling. When it commits, the decision is already made and
you are choosing which door, not whether to run.

**It hunts by SOUND, and that includes the walkie-talkie.** Proximity voice,
the radio, footsteps, a dropped crate, a gunshot. The radio one is the cruel
part and the reason it is the best threat in the list: the crew's only
long-range coordination becomes the thing that kills them, so the correct play
against a cannibal is to **go dark, go quiet, and lose contact with each
other**.

That is a monster that separates a crew without ever touching them.

- **20 damage** — five hits from full, and Phase 2 gives no regeneration
  ever, so two hits in round 1 is a wound carried for the campaign
- **Loses you in the dark and in silence.** If it tracks perfectly the lamp
  and the radio stop being decisions, and the whole design collapses to a
  chase
- A **push** staggers it. One second, not a solution — enough to get through a
  door, not enough to win

## 4 · The thief — revised 30 Aug

Takes a **stowed** item and runs for an edge, then jumps. Chasing does not
work; that is the point of him.

Two answers, and both cost:

- **Shoot him** — he drops everything, and every ear on the floor now knows
  where you are
- **Push him** — he drops one item. Keep pushing, keep collecting, and it
  becomes a scramble that is loud and slow and exactly as undignified as it
  sounds

He steals from the **pack, never the hands**. Losing what you are holding
reads as a bug; losing what is on your back reads as a theft. And he is
visible for a beat before he takes anything — a thief nobody saw is
indistinguishable from the game losing your items.

## 5 · THE EYELESS — it takes your sight

**Its power: it breaks lights.** Not you. Lights.

It has no eyes. It finds a lamp by the heat and the hum of it, walks to it,
and puts it out — the cage light in a room, a dropped torch, and if it reaches
you, **your headlamp**.

A broken headlamp is not a scratch. It is dark for the rest of the round, it
follows you into the next one, and **it costs money at the shop to replace**.
So the eyeless does something no other threat does: it takes a *tool* rather
than health, and the crew pays for it two rounds later.

**How it hunts:** it goes toward the brightest thing it can sense and ignores
everything else. Sound means nothing to it. You can stand beside it and talk.

**How you survive it:** turn your lamp off and walk past. That is the whole
answer, and it is the reason this creature exists — Phase 4 replicated the
headlamp switch so a crewmate going dark is visible to everyone, and right now
that switch means nothing. Against the eyeless it becomes a formation: **one
lit scout drawing it down a corridor while three dark crewmates loot behind
it.**

**Why it is evil:** it does not kill you. It makes the building unusable and
sends you home poorer, and a crew whose lamps are gone has to decide whether
to keep going blind or take the loss.

---

## 6 · THE TENANT — it takes your trust

**Its power: it speaks in your crew's voices.**

It listens. When somebody talks near it — proximity or radio — it keeps a few
seconds of them. Later, somewhere else, it plays them back.

Not gibberish. **Your friend's actual voice**, saying something your friend
actually said, arriving from the wrong room at the wrong time. And because
Phase 4 already carries real voice frames between machines, this is genuinely
that person's voice rather than an impression of one.

**What it does with it:**

- Calls you toward a room nobody is in
- Says *"I'm down here"* in the voice of somebody who is fine, or who is
  standing next to you
- Takes the walkie-talkie channel and holds it, so the one person who needs
  the radio cannot get on it

**How you survive it:** verify. Ask a question it cannot answer with a
recording, or go and look. Both cost time, which is the point.

**Why it is evil:** every other threat here is answered by a rule — go dark,
go quiet, run. This one is answered by **not believing your friends**, and a
crew that has been caught once hesitates the next time somebody genuinely
shouts for help.

It is the only inhabitant that makes the game worse *after* you have killed
it, because the doubt stays.

---

## 7 · THE WEIGHT — it takes your way out

**Its power: it rides the elevator, and the elevator cannot move.**

It does not chase anybody. It does not enter rooms. It climbs onto the car —
the roof, the underside, the frame — and **it counts as mass**.

The load gauge is already in this game and already refuses to move over
550 kg. So the crew arrives at the lift, presses RETURN, and the dashboard
says the car is too heavy. **Nothing is visibly wrong.** The deck looks the
same as it did.

**What it costs:** the crew has to find it and get it off. That means
somebody leaves the car, goes around, and **pushes** — which is loud, and slow,
and happening while the room timer runs and everything else on the floor is
coming toward the noise.

**How you survive it:** get it off, or drop enough loot to move anyway. That
second option is the good one: **it turns your haul into the price of
leaving.**

**Why it is evil:** it arrives at the exact moment a run is over and safe. The
crew has already won, is already counting the money, and the doors will not
close. It turns the load gauge — a number the crew has been reading all game —
into the thing telling them something is on the roof.

---

---

# PART 4 — TWO NEW VERBS

## PUSH — the game has no way to affect somebody without killing them

Requested 30 Aug, and it is the most useful thing in this document, because it
is a verb that works on **everything**: friends, thieves, cannibals, the fat
man, a crate on a ledge.

**What it costs:** a short cooldown and your hands being empty-ish. Nothing
else. It is not a weapon, and it must never become one.

| pushed | what happens |
|---|---|
| a crewmate | shoved. Out of a doorway, off a bridge, into a lift. **Yes, you can push a friend into the shaft** — and that has to stay possible, because a game where you cannot betray somebody is a game where trusting them means nothing |
| the thief | drops **one** item. The scramble above |
| the cannibal | staggered one second. Enough for a door |
| the fat man | barely moves. He is 140 kg and the shove tells you so |
| loot | slides. A crate you cannot lift can still be moved toward the lift |

**Mass decides the outcome**, using the weight classes Phase 2 already built.
Nothing new to tune: a push is an impulse, and 140 kg absorbs one.

## Q — put it down, or hold to throw

**E is for taking.** Q is for giving up, and separating them fixes something
that has always been slightly wrong: the same key doing both means a
mis-timed press picks up what you just dropped.

- **Tap Q** — place it down, gently, where you stand
- **Hold Q** — wind up. Release to throw

**The heavier it is, the longer the wind-up and the shorter the throw.** A can
goes across a room; a crate goes two metres and lands hard; a vending machine
cannot be thrown at all and the wind-up simply never completes.

What throwing is *for*, in order of how much it matters:

1. **Loot into the lift** from the doorway, without walking it in — which
   saves seconds, and seconds are the resource
2. **Noise, deliberately** — a can thrown down a corridor is a cannibal sent
   somewhere else. The first real counterplay in the game that is not hiding
3. **At the thief** — slow, unreliable, satisfying

Throwing loot **damages nothing** and loses no value. This game already
punishes greed with weight; it does not need to punish it with breakage too.

---

# PART 5 — WHERE THESE LAND

| | phase | why |
|---|---|---|
| push, Q drop, Q throw | **5** | verbs first. Every inhabitant below assumes push exists |
| fat man + survivors | **5** | already costed in ECONOMY; the room kit places them |
| survivors behind puzzles | **6** | ROADMAP already says *no exceptions* |
| eyeless | **6** | hazard slot; it is a movement rule plus one interaction |
| the weight | **6** | it is a number on the load gauge and a push |
| cannibal | **6** | needs sound to be a system first |
| thief, seller, gun | **7** | economy pieces before they are creatures |
| the tenant | **7** | needs the radio to be something a crew relies on |

**Push comes first, before any of them.** Four of the seven have an answer
that is "push it", and building the creatures before the verb means building
them twice.
