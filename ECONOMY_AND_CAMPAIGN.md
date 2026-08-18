# SAFE DEPOSIT — Campaign Structure & Economy v2

Built from your round-1 numbers. Every value below is derived from a formula, so
you can retune the whole game by changing three constants.

---

# PART 1 — THE NUMBERS LINE UP, AND THAT'S NOT LUCK

**Room destruction is driven by real time spent in the building, not by the
round counter:**

```
RoomsDestroyed(runMinutes) = floor(runMinutes / 10) + 1
```

Every full 10 minutes you're still inside, one room is destroyed and you **see
it happen** — a live demolition event. On extraction, one more room always
goes, but since you're already on your way up, you only learn about it
**afterward, on the results screen.**

| Run length | Rooms lost | How you learn about it |
|---|---|---|
| < 10 min | 1 | reported at extraction |
| 15 min | 2 | 1 witnessed (10-min tick) + 1 reported |
| 25 min | 3 | 2 witnessed (10, 20-min ticks) + 1 reported |

**A ~15-minute round destroys 2 rooms on average** — that's where the "100
rooms / 50 rounds" planning target below comes from. It's a target, not a
guarantee: play fast and the building outlives the schedule; play slow, or get
into trouble, and it doesn't.

| | Rooms | Rounds | 2 × Rounds |
|---|---|---|---|
| **Full game** | 100 | 50 | **100** (planning target) |
| **Demo** | 20 | 10 | **20** (planning target) |

## When the building runs out before the campaign does

A fast, efficient crew out-paces the demolition — fewer than 2 rooms lost per
round, on average, across the campaign. If they clear the last room before
round 50, **that's a win-shaped ending, not a loss**: *the crew got everything
out before the building came down.* This sits alongside the other two endings
— the mafia killing you for a missed payment, and 3 survivor deaths — as the
third way a campaign ends, and the only good one. It should read as an
achievement on the results screen — *"THE BUILDING IS GONE. YOU GOT THEM ALL
OUT FIRST."* — not a generic game-over.

It also means speed is rewarded **twice**, structurally: the existing
`SpeedBonus` pays money for a fast round, and a fast round now *also*
preserves more of the building for future rounds. Slow, cautious play is safer
inside the room but starves the campaign of both money and time.

It also gives you a hard rule for tuning: **if you change the round count,
change the room count with it, 2:1** — as a planning target. The real number
will vary with how the crew plays.

---

# PART 2 — THE THREE CONSTANTS

Everything scales from these. Change one, the whole economy retunes.

```
BASE_INCOME  = 400     money in a full round-1 clear (9 items, 3 rooms)
BASE_MAFIA   = 200     what the mafia takes in round 1
g            = 1.07    growth per round (7%)
m            = 1.072   mafia growth per round (7.2%)
```

**The mafia grows 0.2 points faster than everything else.** That is the entire
difficulty curve. It's invisible per round and inescapable over fifty.

## The functions

```
Income(R)      = 400  × g^(R-1)          money available in round R
Mafia(R)       = 200  × m^(R-1)          the cut, non-negotiable
Surplus(R)     = Income(R) - Mafia(R)    what you actually get to spend

RopeCost(R)    = 80   × g^(R-1)          +5 m, +1 room. Max 2 buys per round
MassCost(n)    = 50   × 1.35^n           +50 kg. n = upgrades already owned
ShopPrice(R)   = base × g^(R-1)          every other item scales the same way

Rescue(R, f)   = Mafia(R) × (1 + f/10)   f = the room they were lost on
```

**Why every price scales with `g`:** if shop prices stayed flat, a walkie-talkie
would be a real decision in round 1 and pocket change by round 20. Scaling
everything together means the *relative* cost never changes — round 40 feels
exactly as tight as round 1. This is standard relative pricing and it's the
reason the curve stays honest for fifty rounds without hand-tuning.

## The table

| Round | Income | Mafia | Surplus | 2 ropes | **Left over** |
|---|---|---|---|---|---|
| 1 | 400 | 200 | 200 | 160 | **40** |
| 5 | 524 | 265 | 259 | 105×2 = 210 | **49** |
| **10 (demo end)** | **736** | **374** | **362** | **294** | **68** |
| 20 | 1,448 | 749 | 699 | 580 | **119** |
| 30 | 2,848 | 1,502 | 1,346 | 1,140 | **206** |
| 40 | 5,603 | 3,012 | 2,591 | 2,242 | **349** |
| 50 | 11,021 | 6,040 | 4,981 | 4,408 | **573** |

**Round 1 matches your example exactly:** 400 loot − 200 mafia − 160 for two
ropes = **40 left**, enough for the 30 walkie-talkie and 10 saved.

And look at the leftover as a *share of income*: **10% at round 1, 5% at round
50.** The pile of cash gets bigger and the freedom gets smaller. The game eats
everything, exactly as you asked — but it does it by squeezing, not by taking.

---

# PART 3 — LOOT: FOOD, NOT PIANOS

You're right and it fixes the story. People are starving on the surface. A grand
piano is worth nothing to a mafia cornering the food market.

**3 items per room. Round 1 = 3 rooms × 3 = 9 items = $400.** So the average
round-1 item is ~$44.

| Tier | Value (R1) | Mass | $/kg | Examples |
|---|---|---|---|---|
| Bulk | 15–30 | 20–35 kg | ~1 | canned goods, flour sacks, bottled water |
| Common | 35–60 | 10–20 kg | ~3 | dried stores, cooking fuel, salt, coffee |
| Good | 70–120 | 4–10 kg | ~12 | vitamins, sealed rations, water purifier tabs |
| Rare | 150–300 | 1–3 kg | ~100 | **antibiotics, insulin, seed-bank vials, baby formula** |
| Bulk-heavy | 250–400 | 120–250 kg | ~2 | ration pallet, water tank, sealed freezer unit |
| **Survivor** | **0** | **70 kg** | **0** | |
| **Document** | **0** | **1 kg** | **0** | backpack slot only |

**The skill curve is value ÷ kilo.** A crate of beans is heavy and nearly
worthless. A box of antibiotics is the size of a book and worth six crates.
Learning to read a room and take the *dense* things is the mastery.

And it makes the moral line sharper than gold ever could: **the medicine you're
selling to the mafia is medicine somebody in this building needs.**

---

# PART 4 — MASS, AND HOW IT CONNECTS TO MONEY

**Corrected.** You go up **once**, together, with everything. No trips, no
waiting, nobody left on a landing. That changes the whole calculation and it
makes the design cleaner, not messier.

## The rule

> **Everyone ascends together, with everything, in one movement.**
> **The rope's limit is the crew plus the cargo plus the survivors.**

```
BASE_CAPACITY  = 550 kg          total mass on the rope
PLAYER_MASS    = 70 kg           × 4 = 280 kg
Capacity(n)    = 550 + 50n
CapacityCost(n)= 50 × 1.25^n
```

## Round 1 sits exactly on the line

Your own loot table:

| Item | Value | Mass |
|---|---|---|
| 1 × Good | 100 | 10 kg |
| 1 × Common | 50 | 20 kg |
| 4 × Bulk | 4 × 25 = 100 | 4 × 35 = 140 kg |
| 1 × Bulk-heavy | 150 | 100 kg |
| **Loot total** | **$400** | **270 kg** |
| 4 players | — | 280 kg |
| **On the rope** | | **550 kg — exactly at capacity** |

**Round 1 lets you take every single thing, and not one kilo more.** That's the
right starting point: the crew learns that the rope is *full*, before the game
ever asks them to choose.

## Then the game asks

| Situation | On the rope | Over by |
|---|---|---|
| Crew + all loot | 550 | — |
| Crew + all loot + **1 survivor** | 620 | **70 kg** |
| Crew + all loot + **fat survivor (140)** | 690 | **140 kg** |
| Crew + all loot + **two survivors** | 690 | **140 kg** |

**A survivor doesn't cost you a trip. It costs you loot.** Take the person and
70 kg goes back down the shaft — two bulk crates, about **$50**, a quarter of
round 1's mafia payment.

Take the **fat man** and it's 140 kg. Four crates. **$100 — half the payment.**
And you still have to make that payment or the mafia kills you.

That is the entire game in one number, and it happens on every single ascent.
Nobody waits, nobody is separated, and nobody needs a cutscene to feel it.

## The function you asked for

```
LootValue(R)  = 400 × 1.07^(R-1)                money on the floor
LootMass(R)   = 270 × 1.019^(R-1)               kilos it comes in
Density(R)    = LootValue / LootMass            $ per kg
NeededCap(R)  = 280 + LootMass(R)               to take it all
SurvivorCost  = 70 × (cheapest $/kg in your haul)
```

**Value grows at 7% a round. Mass grows at 1.9%.** Loot gets *denser* as you go
deeper, because deep rooms hold rares and rares are small.

| Round | Value | Mass | $/kg | Capacity needed | Upgrades |
|---|---|---|---|---|---|
| 1 | 400 | 270 | 1.5 | 550 | 0 |
| 10 | 736 | 320 | 2.3 | 600 | 1 |
| 20 | 1,448 | 385 | 3.8 | 665 | 3 |
| 30 | 2,848 | 465 | 6.1 | 745 | 4 |
| 40 | 5,603 | 562 | 10.0 | 842 | 6 |
| 50 | 11,021 | 680 | 16.2 | 960 | 9 |

**This is the mass ↔ money relation.** Capacity is not optional and it is not a
power fantasy — it is a **tax you pay to keep taking everything.** Fall behind on
upgrades and you start leaving loot on the floor of a building that's being
demolished.

Nine upgrades across fifty rounds cost **1,290 total** — a small, constant drain
that competes with rope every single round.

| Upgrade | Cost (R1) | Capacity | Spare after full haul |
|---|---|---|---|
| — | — | 550 | 0 kg |
| 1st | 50 | 600 | 50 kg |
| 2nd | 63 | 650 | 100 kg — **one survivor** |
| 3rd | 78 | 700 | 150 kg — **the fat man** |
| 4th | 98 | 750 | 200 kg |

**Read that table again — it's the best thing in the economy.** Capacity
upgrades aren't measured in kilos, they're measured in **people**. The second
upgrade is "we can save someone without losing money." The third is "we can save
*him*."

---

# PART 4b — LOOT SPAWNING: ALWAYS MORE THAN YOU CAN CARRY

This is the piece that was missing, and it resolves something I had wrong.

> **`BASE_INCOME` is what a good crew EXTRACTS. It is not what is on the floor.**

The floor always holds more. You take the best of it and you leave the rest —
and then you spend the whole next round wondering whether the room you left it in
is still standing.

```
SpawnValue(R) = LootValue(R) × 1.4        what actually spawns
Extractable(R) = LootValue(R)              what fits on the rope
```

**Round 1: ~$560 of loot spawns across 3 floors. You can carry ~$400 of it.**
The other $160 stays behind.

## The budget spawner

This is a standard technique and it does exactly what you described. Each floor
gets a **value budget in points**. Items are drawn at random and their price is
deducted until the budget runs out.

```
FloorBudget(R) = SpawnValue(R) / openFloors × random(0.8, 1.2)
```

Round 1: 560 / 3 ≈ **187 points per floor**, ±20%.

| Draw | Cost | Running total |
|---|---|---|
| Bulk-heavy | 150 | 150 |
| Bulk | 25 | 175 |
| Bulk | 25 | 200 — budget spent |

Another floor might roll:

| Draw | Cost | Running total |
|---|---|---|
| Good | 100 | 100 |
| Common | 50 | 150 |
| Bulk | 25 | 175 |
| Bulk | 25 | 200 |

**Same money. Completely different problem.** The first floor is 200 kg for $200.
The second is 100 kg for $200. One of them you can afford to take entirely; the
other you cannot.

## Why the budget is in VALUE and never in mass

Because mass is the thing you want to vary. Budget the money, let the kilos fall
where they may, and the variance you asked for appears on its own:

- A floor that rolls **two bulk-heavies** — $300 in 200 kg. Take one, come back
  for the other. *That's your exact scenario.*
- A floor that rolls **a rare** — $300 in 3 kg. Take everything, laugh.
- A floor of **all bulk** — heavy, cheap, and genuinely not worth the rope space
  if a survivor needs it

No special cases. One random draw against a money budget produces all of it.

## The trick worth stealing

Set a tier's cost slightly **above** half the budget so a floor can rarely afford
two. Bulk-heavy at **150** against a 187 budget means two never fit on one floor —
but across a floor's main *and* side room, or across two floors in the same run,
they absolutely do. That's how you tune "sometimes" without writing a rule for it.

## What this does to the game

**You can never clear a round.** There is always something left, and the thing
left is always the heavy, awkward, low-density item nobody wanted to carry.

So round 2 begins with a real question: *go deeper into the new room the rope
just bought, or go back for the water tank on floor 1?*

And the demolition answers it for you, badly, about a third of the time.

**This also fixes the thing you were worried about** — the crew stripping all
three floors in round one and having nothing to return for. Now they physically
cannot, because the rope will not lift it.

---

# PART 5 — THE SHOP

Base prices are round-1. Multiply by `g^(R-1)` for later rounds.

**Benchmark from Lethal Company:** their first quota is 130 and shop tools run
30–140 — so **a tool costs 25–100% of the first payment**. Ours: first payment
200, tools 8–160. Same shape, which is a good sign.

### Rope & load
| Item | R1 | What it buys |
|---|---|---|
| **Rope +5 m (+1 room)** | **80** | depth. Max 2 per round |
| **Capacity +50 kg** | **50 ×1.25ⁿ** | trips, which is time, which is rooms |
| Rope patch kit | 15 | repairs fraying mid-run |
| Carabiner | 45 | traverse past cargo in 0.5 s not 1.2 s |
| Pulley descender | 60 | fast controlled drop; no help going up |
| Cargo net | 55 | two small items occupy one rope band |
| Portable anchor | 130 | a second rope point. Opens routes |
| **Loot Collector** | **600** | the cage. After room 20 |

### Light — darkness is a resource
| Item | R1 | |
|---|---|---|
| Spare headlamp cell | 8 | consumable |
| Chem lights ×5 | 10 | mark cleared rooms, no battery |
| Flares ×3 | 12 | bright, temporary, visible to threats |
| Floodlight | 70 | lights a whole room. Weighs 15 kg |
| Night vision | 140 | permanent, one owner |

### Information
| Item | R1 | |
|---|---|---|
| **Hint tracker — 1 use** | **25** | glows the solution. One owner only |
| Room map | 35 | reveals one floor's sub-room layout |
| Value appraiser | 65 | shows true $/kg. Counters junk that looks rich |
| Trap scanner | 90 | reveals traps at short range |
| **Demolition schedule** | **110** | **shows which room dies next.** The most
strategically valuable item in the shop |

### Communication
| Item | R1 | |
|---|---|---|
| Signal beacon | 20 | marks a spot, visible through walls, one use |
| **Walkie-talkie (pair)** | **30** | leader picks the two who can talk |
| Radio relay | 75 | extends proximity voice by one floor |

### Medical
| Item | R1 | |
|---|---|---|
| Bandage | 10 | heals 40 |
| Splint | 22 | removes the limp |
| Adrenaline | 28 | full speed while injured, 60 s |
| **Med spray** | **35** | revives a downed player where they lie |
| Stretcher | 160 | carry a survivor at full walking speed |

### Access — buy your way past a puzzle
| Item | R1 | |
|---|---|---|
| Bolt cutters | 40 | one chain or grate |
| Crowbar | 55 | forces one locked door. **Loud** |
| Multitool | 70 | bypasses one keypad |
| Master key | 95 | opens any keyed door, once |

### Survivors
| Item | R1 | |
|---|---|---|
| Rations | 18 | calms a survivor; feeds the starving one |
| Oxygen mask | 45 | a survivor survives 60 s longer in gas |
| Harness | 85 | survivor climbs twice as fast |

### Emergency
| Item | R1 | |
|---|---|---|
| Emergency winch pull | 120 | yanks everyone up instantly. Once per run |
| **Rescue contract** | formula | see below |

**Two of these deserve a note.**

**The demolition schedule (110)** is the strongest item in the shop and it should
be expensive enough to hurt. Knowing which room dies next converts the whole game
from panic into planning — which is exactly why it must cost more than a rope.

**The crowbar (55)** lets a crew decide they don't have time for the puzzle
tonight, and pays for it with noise. Every good tool converts one resource into
another; this one converts money into time and time into risk.

### Rescue
```
Rescue(R, f) = Mafia(R) × (1 + f/10)
```
| Lost in round | On room | Cost | vs surplus |
|---|---|---|---|
| 5 | 4 | 372 | 1.4 rounds |
| 10 | 12 | 823 | 2.3 rounds |
| 25 | 28 | 3,050 | 3.2 rounds |
| 40 | 45 | 16,566 | 6.4 rounds |

Shallow losses are recoverable. Deep losses are a crisis that takes both of your
two runs and every purchase in between. Partial payment carries over — so the
crew spends two rounds deciding, every single time they open the shop, whether
the rope matters more than their friend.

---

# PART 6 — CREW SYSTEMS

## Shared money, one leader

All loot goes into one pot. **The leader spends it** and assigns permanent items
to specific players.

This is a better idea than it looks. It creates:
- **A real role.** Someone is the quartermaster, and it isn't a menu — it's a
  negotiation with three people talking over each other.
- **Blame with a name.** "Who bought the night vision instead of rope?"
- **A reason for the walkie-talkie to be a *pair*** — the leader chooses which
  two people can talk. That's a tactical decision *and* a social one.

**One rule to add:** show every player what was bought and who got it, on one
screen, before the run starts. Secret spending breeds resentment; visible
spending breeds arguments, and arguments are content.

## The ascent vote

Everyone must be **on the main rope** to vote. If someone isn't, the vote screen
names them:

> *"Karim is not on the rope."*

That single line does a lot of work — it turns "let's go" into "where is Karim",
which is the correct question. **Name them, don't just say "someone".** With
proximity voice, three people shouting one name is the moment.

## Survivors climb themselves

Get a survivor within **2 m of the rope** and they climb on their own.

That's the right call — escorting is more interesting than carrying, and it
means a survivor is a *navigation* problem through traps and puzzles rather than
a slow walk. But keep the weight: **while they climb, their 70 kg counts against
rope capacity.** So the moment they start up, your loot budget shrinks.

The scene that produces: a survivor starts climbing, the rope groans, and
somebody has to unclip a crate of antibiotics and drop it down the shaft.

## The hint tracker

Press **H**: the solution glows — the key's location, the fuse, the correct
alignment, or a short clip showing the mechanism.

**One use. One owner.** Only one player may hold it, so the crew has to decide
*who* gets to be the one who can see. And at 25 it's cheap enough to buy every
round, which means the real cost is that you spent it on the wrong puzzle.

---

# PART 7 — SURVIVORS

- **9 required. 11 placed. 3 deaths = campaign over.**
- **Every survivor is behind a puzzle.** No exceptions — you never just walk up
  to a person and press E.
- **A survivor cannot die in a room your rope cannot reach.** Their clock starts
  when the room becomes reachable.

That third rule is yours and it's the one that makes the whole system fair. It
completely solves the problem I raised last time — you can never lose someone
you had no way to save. The pressure lands only where the player had a real
choice, which is the only place pressure ever belongs.

**The father** is exempt from random demolition and is the last room to go.
Losing him ends the campaign.

**Documents: 5 required, 7 placed.**

---

# PART 8 — DEMO CONFIGURATION

| | Demo | Full |
|---|---|---|
| Rooms | 20 | 100 |
| Rounds | 10 | 50 |
| Puzzles | **5** | 25 |
| Survivors | 2 required, 3 placed | 9 required, 11 placed |
| Documents | 1 required, 2 placed | 5 required, 7 placed |
| Father | not in demo | deepest room |
| Income range | 400 → 736 | 400 → 11,021 |
| Loot Collector | not in demo | after room 20 |

**The demo is 10 rounds ≈ 2.5 hours.** That's a long demo, but this genre
supports it — Lethal Company's appeal is the loop, and a loop needs several
rounds to show its teeth. Cut to 8 rounds if playtests say otherwise.

Use the demo's five puzzles from Tier 1 only. Save every new concept for the
full game — a demo that shows all your best ideas has nothing left to sell.

---

# PART 9 — WHAT OTHER GAMES DO, AND WHAT TO STEAL

## Lethal Company's quota

Their formula is **quadratic**: `100 × (1 + fulfilled²/16) × randomizer`, first
quota 130. Because the *increase* scales quadratically, the quota itself scales
cubically and the total money needed scales quartically. Pressure rises slowly,
then accelerates hard.

**Yours is exponential (7.2%/round). Keep it.** Exponential is smoother and — more
importantly — *predictable*. A player can look at round 12 and reason about round
20. Quadratic curves feel fine until they suddenly don't, and Lethal Company gets
away with it because a run is 20 minutes and a campaign is short. Yours is 50
rounds; players need to be able to plan.

### Two things worth taking

**1. The randomizer.** Lethal Company multiplies the quota by a random factor.
Without it, players compute the optimal purchase order once and follow it forever.

```
MafiaDemand(R) = 200 × m^(R-1) × random(0.9, 1.1)
```

±10% is enough to break perfect optimisation without ever feeling unfair. Show
the number *before* the run so it's a plan, not a surprise.

**2. The overtime bonus.** Theirs is
`(ScrapSold − Quota)/5 + 15 × DaysUntilDeadline` — it pays you for
over-delivering **and** for finishing early.

You already reward speed structurally (fast run = +1 room). Money on top would
be double-dipping, so keep it small — a visible thank-you, not a strategy:

```
SpeedBonus = Income(R) × 0.10 × max(0, (10 - runMinutes) / 10)
```

Out in 5 minutes → +5%. Out in 2 → +8%. It's small, but it appears on the
results screen as its own line, and a number with a name on it changes
behaviour far more than its size suggests.

## The one to avoid

Lethal Company's failure state is instant: miss quota, get jettisoned. It works
for a 3-hour loop. **At 50 rounds, an instant wipe on a missed payment would be
brutal** — which is why your design doc's mafia-lends-rope-at-a-brutal-rate
escape valve matters. Keep it. Debt is a better antagonist than a game-over
screen, because debt keeps playing.

---

# PART 10 — RESOLVED

### Sub-rooms per floor — **3, with a fixed shape**

```
   shaft ──► LANDING ──► MAIN ROOM ──► SIDE ROOM
                            └────────► BACK ROOM (locked or hazard-gated)
```

- **Landing** — the only place you can safely unclip. Regroup point, staging
  area for loot, and where the arguing happens. Every co-op game needs a campfire.
- **Main room** — always open, 1–2 loot, teaches the floor
- **Side room** — open, 1 loot, usually holds a puzzle key
- **Back room** — locked or hazard-gated, holds the best item, the survivor, or
  a document

**Traps: one per floor, two on deep floors.** You asked for "not a lot" and
you're right — traps are punctuation, not prose. A floor where everything is
trapped stops being frightening within two rooms.

Three sub-rooms is the sweet spot: enough to split a crew of four into two pairs,
few enough that nobody gets lost, and small enough that proximity voice still
*almost* reaches — which is what sells the walkie-talkie.

### Failing to pay the mafia — **they kill you, campaign over**

Harsher than Lethal Company, which just jettisons you at the end of a 3-hour
loop. At 50 rounds this is severe, so the numbers have to be honest:

**The mafia demand must always be beatable by a competent run.** With Mafia(R)
at 50% of Income(R) and never rising above ~55%, a crew that clears its open
rooms always makes payment. Death should only come from **greed or disaster** —
buying too much rope, or losing a run to a trap — never from arithmetic.

Show the next demand **on the results screen of the previous round**, so it's
always a plan and never an ambush.

### Leader — **re-voted each round, only if someone asks**

The shop has a **Change Leader** button. If nobody clicks it, the leader stays
and the round starts immediately. If anyone clicks it, everyone votes.

This is a good design instinct — it makes leadership *stable by default and
challengeable on demand*. There's no ritual vote every round to sit through, but
a bad spend is always answerable. And the act of clicking that button in front of
your friends is itself a social moment.

### Loot on the rope — **comes up on ascent**

Voting to ascend brings everything clipped to the rope with you. It's collected.

**And a floor you strip stays stripped.** Clear floor 1 completely in round 1 and
floor 1 is empty forever — so there's no going back, only down. Combined with
demolition, the world only ever shrinks.

That means every round has exactly one question: *how deep can we afford to go,
and how much can we carry when we get there?*
