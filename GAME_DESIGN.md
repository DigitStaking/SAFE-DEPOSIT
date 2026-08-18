# SAFE DEPOSIT — design document

Co-op first person. 4 players (up to 6). Unity, PEAK-style flat shading.
One shared rope down a collapsing shelter.

---

## 1. The world

The war ended and the surface lost. Nothing grows, nothing is manufactured,
nothing is imported. People survive by trading things that still have value —
metal, medicine, art, and above all **food**.

The mafia understood this before anyone else. They are not looting for
treasure, they are racing to corner a market. Whoever controls the last
functioning supply controls what is left of the world.

## 2. The building

A bank tower converted into a civilian shelter during the war. Vaults and
deposit boxes on the upper floors; families housed on the lower ones. It took
a hit and the government wrote it off.

The government is now demolishing it **floor by floor, from the roof down.**

Officially: it is unsafe.

Actually: the war was planned in that shelter, and the paperwork is still in
it. Every floor they blow is evidence of a war crime turning to dust.

That single fact does more work than anything else in this design:

- It explains why the government refused your offer to help
- It makes the collapse an **antagonist with a motive** rather than a timer
- It gives the deepest floors a reason to be the most dangerous
- It means somebody is actively racing you, and they are winning

## 3. The crew

Four friends. They offered to help the rescue effort. They were refused.

So they signed with the mafia — not because they wanted to, but because the
mafia has rope, tools, and a reason to go down there. The crew is using the
mafia as much as the mafia is using them.

Three things they actually want:

1. **Resources** — to survive, and to keep the mafia paid so they stay alive
2. **The people still in the shelters** — the ones nobody is coming for
3. **One friend's family**, on the bottom floor

*Can you get deeper. Can you solve the puzzles. Can you stay alive. Can you
stop being greedy.*

## 4. The three things you can carry, and why that is the game

The rope has one load limit. Everything competes for it.

| What | Pays | Costs you |
|---|---|---|
| **Treasure** | The mafia. Keeps you alive. | Nothing — this is the safe choice |
| **Survivors** | Nothing at all | A person weighs as much as your best piece of loot |
| **Evidence** | Nothing at all | Backpack slots, and it is only on the deep floors |

The mafia does not care about people and does not care about paperwork.

So every run is the same argument, held out loud, on a rope, with your friends
listening: **this person, or the gold? this file, or the gold?**

And it is not abstract — it is a weight limit. You are not choosing with a
menu, you are choosing with a winch that is already groaning.

**Endgame.** Enough evidence plus your friend's family is what lets the crew
break from the mafia. Pure greed keeps you alive and keeps you owned.

---

## 5. The loop

```
descend → loot → load the rope → climb out → sell → pay the mafia
   → buy rope → the government blows another floor → descend deeper
```

**Rope length is progression.** More rope, more floors, new rooms, harder
puzzles, better loot.

**The ratchet.** Every time you surface, another floor is destroyed from the
top. The rooms you already know stop existing. Farming a familiar floor is not
a slow strategy, it is a slow death.

**The failure state.** Under-buy rope and you end up with a line that only
reaches floors that are no longer there. Nothing left to loot, nothing left to
pay with, and the mafia does not take excuses.

**The escape valve.** The mafia will front you rope on credit at a brutal rate.
The run is never unwinnable, only worse. That way nobody rage-quits — they get
deeper in debt instead, and the mafia gets scarier for free.

---

## 6. The rope

### Structure
One rope from a winch at the top. Not a physics chain — an anchor, a length,
and a bend. Everything reads its position from one function, which is why one
player's action moves everyone.

### Your tether
A self-retracting spool on your back.

- **Feet on something** — it feeds out, up to 10m. That is your reach into a room.
- **In the air** — it winds back to 2.5m and catches you. That is the pendulum.

Cut it with **F** and you are free, slower, and **invisible to the whole crew**.

### Moving the rope
Three ways, in ascending order of coolness:

1. **Q — pin it.** Hooks the rope to a doorway. It kinks there and everyone
   below is suddenly hanging off your door. One person at a time, so the crew
   waits and talks.
2. **Pump it.** Push off walls while taut and the rope bends. Pulls are
   **summed**, so four people in rhythm can throw it across the shaft and two
   people out of sync achieve nothing at all. Coordination is physics, not a
   scripted puzzle.
3. **Space — leap off it** toward wherever you are looking.

### Weight
| Class | Effect |
|---|---|
| Small | Goes in your backpack. Hands free, move normally. |
| Heavy | Two hands. No climbing, no jumping, no leaping. |
| Massive | Cannot be carried out at all until you own the Collector. |

**The backpack changes convenience, never weight.** Every gram still counts
against the winch. If the pack made things lighter it would delete the load
limit, and the load limit is the game.

You cannot see your own pack — your friends can. "How heavy are you?" is a
question the crew has to ask out loud at the moment nobody has time to talk.

**G ditches the entire pack instantly.** The panic move.

---

## 7. The Loot Collector

A cage that rides the main rope. Bought after floor 5.

- Load it, then send it up or down remotely
- Far more capacity than clipping items one at a time
- **The only way to extract Massive items.** Before you own it, the piano is
  physically not leaving the building.

**It must not be a pure upgrade,** or it deletes the tension it was meant to
relieve. So:

- It is **slow**, and it is **loud**. Noise attracts things.
- While it moves it **occupies the rope** — anyone clipped on is shoved aside
  or has to wait
- Its contents still count against the anchor load
- It can only be at one place on the rope at a time, so the crew has to agree
  where it goes

The correct feeling: owning it changes *what* you can steal, not *whether* you
have to think.

---

## 8. Puzzles

Design rule: **a good puzzle here is not a riddle, it is a reason to be in two
places at once.** If one player can solve it alone while the others watch, cut
it.

### Cross-room dependencies
- **The manager's keycard.** Found in an office on floor 2, opens the vault on
  floor 4. You have to remember it exists and carry it down — and it takes a
  backpack slot the whole way.
- **The ledger.** Deposit boxes are identical and mostly worthless. The book
  saying which ones matter is in another room. One player reads numbers aloud
  while the others open boxes. Pure voice-chat gameplay.
- **Three fuses, three rooms.** A floor has no power until all three are found
  and fitted. Forces the crew to split up, which is the point.
- **The intercom.** A panel in one room shows instructions for a machine in
  another that the reader cannot see.

### Simultaneous action
- **Matched dials.** Two vault dials in separate rooms, same number, same
  moment. Neither player can see the other's dial.
- **Three-handle blast door.** Three levers, three rooms, all pulled within two
  seconds. Hard-locks the room to teams of three or more.
- **The light plate.** A pressure plate keeps the room's only light on. One
  player stands there doing nothing while everyone else works. Somebody will
  step off as a joke.

### Puzzles that cost you
- **Counterweight vault.** The door is held by a counterweight. You load *your
  own loot* onto a platform to open it — spending treasure to reach treasure,
  without knowing if what is inside is worth more.
- **Power routing.** The generator has enough for one door. Two are marked. You
  pick, and you never find out what was behind the other one.
- **The scale.** A vault opens only when two plates balance. Two players plus
  cargo, doing arithmetic under a clock.

### Puzzles that use the rope
- **Swing gaps.** Someone above has to hold your line at exactly the right
  length while you cross.
- **Flood the room.** Raise the water to float up to a high ledge. Your loot
  floats too, and floats away.

---

## 9. Threats

Rule: **an enemy that just kills you is boring. Every threat should make you
drop something, cut something, or hold still.**

- **The rival crew.** Another salvage team, mafia-connected. They go for **the
  rope, not for you** — nobody else in the game does that. They talk, they
  threaten, and they can be paid off by dropping loot. So the answer to "how do
  we survive this" is "how much money will we lose."
- **The starving one.** One of the people you were meant to rescue, down there
  too long. Tracks sound and food. **You can feed him** — buy rations, drop
  one, and he stops. Feed him across several runs and he starts following you
  and helping. An enemy you convert with money you needed.
- **Something that hunts vibration.** Blind, wakes when the winch runs. Everyone
  freezes while cargo moves.
- **The unlit thing.** Only moves when no light is on it. Costs almost nothing
  to build and turns your torch into a weapon you have to aim by choice.
- **Bank security, still armed.** Gas and shutters triggered by carrying flagged
  high-value items. Pure logic, no AI to write.

**Do not give players real weapons.** A stun tool at most. Running away while
dropping a piano is always funnier than winning a fight, and the moment players
can kill things they stop panicking. Panic is the product.

---

## 10. Loot

Food is treasure now. Canned goods, a stocked vending machine, and sealed
rations are among the most valuable things in the building — which is exactly
why the mafia is racing for the shelters.

**Common** — cash drawers, office equipment, filing cabinets, fire extinguishers
**Rare** — gold bars, deposit box contents, art, chandeliers, the aquarium
**Massive** — the grand piano, the vending machine, marble busts, wall safes cut
out whole

**Comedy props** — a music box that plays when jostled and attracts attention.
An aquarium with a live fish that floods the room when it breaks. A vending
machine that dispenses if you shake it hard enough.

**Worth nothing to the mafia** — a child's drawing, a family photo, a sealed
letter that was never sent, a welded-shut door with scratch marks on the inside.
And the evidence: ledgers, tapes, orders with signatures on them.

---

## 11. Shop

| Item | Does |
|---|---|
| Rope | Depth. The main progression. |
| Backpack slots | More small items, hands free |
| Trap scanner | Reveals traps at short range |
| Night vision | See in unlit rooms |
| Radio | Voice beyond proximity range — turns one player into a dispatcher |
| Rope patch kit | Repairs fraying mid-run |
| Portable anchor | A second rope point. Opens routes. Limited. |
| Rations | Feed the starving one |
| Stretcher | Carry a survivor at normal speed. Most expensive item, pays nothing. |
| Shrink gun | 30 seconds of reduced size **and weight** — and the weight comes back when it wears off, mid-haul, and snaps your line. Works on friends. |
| **Loot Collector** | The cage. Unlocked after floor 5. |

---

## 12. Art direction

PEAK / Lethal Company. Flat colours, no textures, no PBR, chunky exaggerated
proportions, strong readable silhouettes. Active ragdolls rather than authored
animation.

**Dim, not black.** Darkness is a *mechanic* — light is a resource — but not a
*palette*. Props and suits stay flat and saturated so they pop. Test: shrink a
screenshot to Steam capsule size. If you cannot tell what is happening, it is
too dark.

Identity comes from three cheap things: silhouette, one good colour grade, and
coloured fog. Not from detail.

---

## 13. Build order

**Done** — graybox generator, first person controller, shared rope with bend and
kink, self-retracting tether, hook/pin, carryables with weight classes,
backpack, anchor load, run loop with quota, extraction and floor-by-floor
collapse.

**Next** — the shop between runs (closes the loop), then a second player.

**After** — puzzles, the Collector, threats, survivors, evidence.

**Demo cut list** — the rope, four players, three floors, five room types, two
puzzle types, the load limit, the collapse, and one survivor you can choose to
leave behind. No shop, no story text, no shrink gun. If that slice is not fun
with grey boxes, nothing else saves it.
