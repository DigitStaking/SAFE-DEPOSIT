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
| **the foreman** | wears one of your crew's colours and dances like a friend, then attacks - and a trap on his floor paints one of YOU his colour |
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
| 6 | the foreman | **trust in what you see** | "which one of you is in room 6?" |
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

## 6 · THE FOREMAN — he is wearing your friend's colour

*Redesigned 30 Aug 2026, and this version is much better than mine.*

### What he is

**He spawns wearing the exact colour of one of your crew.** Not orange, not a
generic worker — if your crew is red, blue, green and yellow, he is one of
those four, picked when the floor is built.

So he is never "a stranger in the dark". He is always, specifically, **one of
you**.

### What he does, in order

1. **He walks.** Normally, like a person, going somewhere. At distance that is
   indistinguishable from a crewmate crossing a room, because it is the same
   silhouette in the same colour doing the same thing
2. **When he sees you, he dances**
3. **When you get close, he attacks**

### The dance is the whole design

Dancing is what players do to say *it's me*. It is the crew's own
identity-check — a wave across a dark room, the cheapest possible "friendly".

The foreman uses it. And the first time a crew is caught by that, **the emote
stops working for everyone, permanently.** A real crewmate dancing to say
"it's me, don't shoot" is now doing exactly what the thing that kills you
does.

That is a design that takes something away from the players and never gives it
back — using a feature the game already has.

### THE TRAP — and this is the part that turns it round

**Where there is a foreman, there is a colour trap on that floor.** Always.
One floor, one foreman, one trap.

A crewmate who walks into it is **repainted in the foreman's colour** for the
rest of the round. Guaranteed — **100% activation**, not a chance. It is a
rule, and a crew will learn it as one.

So the floor now holds **two figures in the same colour**, and only one of
them is your friend.

**Worked example, round 3:**

```
room 1   emptied last round
room 2   sealed
room 3   sealed
room 4   loot
room 5   loot
room 6   loot   <- the foreman, and the trap
```

Your crewmate is looting room 6 and steps in the trap. He is now the
foreman's colour. You come to help, you open the door, and there are two of
him.

### Both failure modes are terrible, and that is why it works

Every other threat in this game has one way to lose. This has two, and they
pull in opposite directions:

- **Trust the wrong one** — you walk up to it and it attacks
- **Distrust the right one** — you back off, or you shoot, and it was your
  friend. With the gun in the shop, that is a real thing a real crew will do
  to each other

### The marked player must know

The moment the trap fires, **his own hands change colour on his own screen.**
He knows. He can shout about it.

Without that the whole thing is unfair rather than frightening — he would be
mistrusted with no idea why, and the crew's confusion would read as a bug
rather than as a trap.

### How a crew survives it

**Voice.** Not the dance — the dance is exactly what has been taken away.

- Proximity voice at close range, which the foreman cannot produce
- The **radio**, if they bought one, which is the only way to answer "which of
  you is in room 6" from another floor

So this quietly makes both voice systems load-bearing, and makes the 20 for a
walkie-talkie the difference between a crew that can identify its own people
and a crew that cannot.

### It reverts at the end of the round

The marked crewmate is their own colour again at the surface. It is a **round
condition, not a wound** — and it should be, because it costs the crew a whole
round of doubt, which is expensive enough.

### What it needs building

| | where it already is |
|---|---|
| per-crew colours | `PlayerSkin` is on the player root, stubbed for exactly this |
| the dance | `PlayerAnimatorDriver` already has the emote trigger |
| walking | any inhabitant needs this anyway |
| attacking up close | shared with the cannibal |
| the trap | Phase 6's trap slot, plus one line: recolour the player |

The only genuinely new part is **the colour swap**, and it is a material
change on a model — the cheapest kind of new thing this game could have asked
for.

---

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
