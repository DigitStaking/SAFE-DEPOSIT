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

# PART 3 — WHAT I WOULD ADD

Four, each picked because it presses on something already built and needs
little new code.

## 1 · The one that hunts light ★

Blind. Comes toward any lamp it can see, and ignores you completely in the
dark.

**Taxes: light.** This is the one I would build first, and the reason is that
Phase 4 just spent a commit replicating the headlamp switch so a crewmate
going dark is visible to everyone. That switch currently means nothing. This
gives it teeth:

- You *can* cross its room. You just have to do it blind
- The crew's lamps become a formation problem — one lit scout, three dark
- **Somebody turning their lamp off is now a message**, and the game already
  transmits it

It needs no pathfinding cleverness and no combat. It walks at lights.

## 2 · The tenant — it uses your radio ★★

Occupies the walkie-talkie channel. Not with words — breathing, a wet click,
the sound of your own crew's voices half a second late.

**Taxes: the radio, which is the crew's only long-range coordination.** Phase
4 built the channel to hold exactly one voice at a time, and that mechanic is
sitting there waiting to be turned against the crew. Nothing else in this game
can take away *the ability to talk*.

Cheap to build — it is a NetworkVariable already written and an audio clip —
and it does something no monster in this genre usually does: it makes four
people go quiet and look at each other.

## 3 · The follower — only dangerous when you are alone

Keeps its distance while two crew are in sight of each other. Closes the
moment somebody is alone.

**Taxes: the crew.** Splitting up is how a crew loots efficiently, so this
prices the efficient play without forbidding it. It also makes the proximity
voice matter: hearing somebody through a wall is proof they are still there.

## 4 · The one that is not a survivor

Looks exactly like a downed crewmate at a distance. Kneeling, still, in the
dark.

**Taxes: the med spray, and trust.** A crew that has been burned once will
hesitate over a real crewmate — and hesitating over a real crewmate is the
most expensive thing this game can make you do.

Use sparingly. One per campaign is a story; one per floor is a tax on paying
attention.

---

# PART 4 — WHERE THESE LAND

| | phase | why |
|---|---|---|
| fat man + survivors | **5** | already costed; the room kit places them |
| survivors behind puzzles | **6** | ROADMAP already says *no exceptions* |
| light-hunter, follower | **6** | traps and hazards are the same slot |
| thief, seller, gun | **7** | all three are economy pieces first |
| the tenant | **7** | needs the radio to be something a crew relies on |
| the false survivor | **9** | it only works once people are comfortable |

**Not before Phase 5.** There is nowhere to put any of them: the room kit is
what gives a floor a place for a thing to be, and a cannibal in a graybox
corridor teaches nothing about whether a cannibal is fun.

---

# PART 5 — THE ONE I WOULD CUT IF THE DEMO IS TIGHT

All of them except **the fat man and one threat**.

The demo is ten floors and its job is to prove the loop — descend, weigh the
haul against the rope, decide who you leave behind. A crew that meets four
different monsters in ten floors learns none of them, and the loop gets less
airtime than the bestiary.

**One threat, met three times, is scarier than three threats met once.**
