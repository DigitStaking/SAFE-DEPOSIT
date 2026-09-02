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

## In one line each

No powers, no magic. Each of these is **one thing it does to you**, in the
same register as "the thief takes your items and runs":

| | what it does |
|---|---|
| **the fat man** | needs carrying, and weighs four crates |
| **the seller** | sells cheap, but he is deep and out of your way |
| **the cannibal** | hits you if you have a light on or make noise |
| **the thief** | takes your items and runs |
| **the eyeless** | smashes your lamp, so you are dark for the rest of the round |
| **the foreman** | wears hi-vis and carries a lamp, so at distance he looks like your crewmate |
| **the passenger** | runs to your lift when he sees you and sits in it, +70 kg |

That is the whole of what they are. Everything below is detail about how each
one behaves and how a crew beats it.


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
| 6 | the foreman | **trust in what you see** | "…is that you over there?" |
| 7 | the passenger | **mass, and the way out** | "get him OFF the lift" |

Two of those are not threats — the fat man and the seller — and that is
deliberate. Seven monsters is a bestiary; five monsters, a burden and a
merchant is a **place**.

**And no two take the same thing from you:**

| | it takes |
|---|---|
| cannibal | **your health**, and your ability to coordinate |
| thief | **loot you already earned** |
| eyeless | **your sight** — the lamp, not the life |
| foreman | **your trust in what you see** |
| passenger | **your way out, 70 kg at a time** |

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

## 6 · THE FOREMAN — the man you think is your friend

### The problem he exists to create

Right now, on a dark floor, **you identify a crewmate by exactly one thing: a
moving light in a red suit.** Not their name, not their face — you cannot see
either at fifteen metres. A light in a suit means friend, every time, without
you ever deciding it.

The foreman is a dead building worker in an orange hi-vis jacket with a
working lamp on his helmet. At fifteen metres in the dark, **he is the same
handful of pixels a crewmate is.**

### What actually happens, moment by moment

1. **You come into a room and there is a light at the far end.** Somebody is
   already looting it. You relax slightly and carry on with what you were
   doing
2. **The light does not move the way a player moves.** No strafing, no
   looking around, no bobbing. It stands there. But you are not watching it
   closely, because it is your mate
3. **You get within about six metres and it is obvious.** The jacket is
   orange, not red. The suit is filthy. He is much too still
4. **He turns and walks away from you** — slowly, staying at the edge of your
   lamp, going deeper into the floor
5. **If you follow, you end up somewhere you did not plan to be.** That is
   the whole of what he costs you

### He never touches you

No damage, no grab, no chase. **He is a liar, not a killer.**

That is deliberate, and it is why he is worth a slot. The cannibal already
supplies violence. What the foreman supplies is the thing violence cannot:
after the first time, **every distant light on every floor is a question**,
and the crew has to spend something to answer it.

### What it costs you

- **Position.** You walked away from your route and away from the lift,
  following something
- **Time**, which is the clock that never stops
- **Attention.** You spent ten seconds being sure, and ten seconds is a long
  time to be looking the wrong way

### How a crew beats him

**The radio.** *"Who's in the north room?"* — silence answers it instantly.

That is the quiet reason he belongs in this game specifically: the crew paid
20 each for walkie-talkies, and this is the first threat that makes those an
answer rather than a convenience. A crew with radios is never fooled twice. A
crew without them walks toward every light, every time.

### Why he is cheap to build

He is a model, a light, an idle animation and one rule: *stand still; when a
player gets close, walk away from them.* **No combat, no damage, no audio, no
language** — which is exactly why he can do the tenant's job without any of
the tenant's problems.

## 7 · THE PASSENGER — he wants to leave too

**What he does: the moment he sees you, he runs for your elevator and sits in
it. Seventy kilos, and he will not get out.**

He is not hostile. He is not a survivor either — he will not be led, he will
not follow you, he will not be rescued. He just wants out of this building,
and your lift is the only one working.

**What it costs you:** seventy kilos, which ECONOMY already prices as two
crates. The car is over 550 and will not move, and the crew is standing at the
panel doing arithmetic they did not plan on.

**Three answers, all bad:**

- **Push him off** — he gets up and walks back on. Somebody has to hold him
  off while the doors shut, and that person is now on the wrong side
- **Drop loot to make weight** — clean, and it costs exactly what he weighs
- **Take him** — he rides up with you, gets out at the surface, and is gone.
  You paid two crates for nothing at all

He is the fat man's opposite and that is the point. **The fat man is a person
you choose to save. The passenger is a person you did not choose**, and the
crew will argue about him far more bitterly than they ever argue about the
fat man.

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
| the passenger | **6** | he is a walk toward the lift and 70 kg |
| cannibal | **6** | needs sound to be a system first |
| thief, seller, gun | **7** | economy pieces before they are creatures |
| the foreman | **7** | he is beaten by the radio, so the radio has to matter first |

**Push comes first, before any of them.** Four of the seven have an answer
that is "push it", and building the creatures before the verb means building
them twice.
