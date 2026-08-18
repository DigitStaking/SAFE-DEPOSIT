# SAFE DEPOSIT — Production Plan

Written 14 Aug 2026, after a full read of the code and `GAME_DESIGN.md`.
Design truth stays in `GAME_DESIGN.md`. This file is *how it gets finished*.

---

# PART 0 — WHAT I NOW UNDERSTAND

Correcting my earlier plan, which treated this as a loot game with a rope.

**The game is a weight argument.** The rope has one load limit and three things
compete for it:

| | Pays | Costs |
|---|---|---|
| **Treasure** | the mafia — keeps you alive | nothing |
| **Survivors** | nothing | as much as your best piece of loot |
| **Evidence** | nothing | backpack slots, deep floors only |

Every run is the same conversation held out loud on a rope: *this person, or the
gold?* And it isn't a menu choice — it's a winch that's already groaning.

**The collapse is an antagonist, not a timer.** The government is demolishing
the tower from the roof down because the war was planned in it and the paperwork
is still inside. That means the floors you know disappear, the deepest floors
are the most valuable *and* most dangerous, and somebody is actively beating you.

**The endgame is evidence + one friend's family on the bottom floor.** Greed
keeps you alive and keeps you owned. That's the whole arc.

**Progression is rope length.** The ratchet is that surfacing costs you floors.

### Code status, verified

| Built | Not built |
|---|---|
| Graybox shaft generator | Health / damage / downed / rescue |
| FP camera, motor, animation, hand IK | Puzzles (0 files) |
| Main rope (analytic), tether, hook | Traps (0 files) |
| Carry weight classes, backpack | Survivors, evidence (0 files) |
| Run loop — quota, extraction, collapse | Loot Collector (0 files) |
| Campaign persistence, shop *logic* | Shop **UI** |
| Atmosphere, smoke, light shafts | Doors, keys, rooms as content |
| | Netcode — **the entire co-op game** |

~6,250 lines of runtime C#, ~2,300 of editor tooling. It is a strong single-player
vertical slice of *feel*. It is not yet a co-op game, and co-op is the product.

---

# PART 1 — THE ONE PROBLEM WITH THE RESCUE DESIGN

You chose: left-behind players **spectate**, the rescue team is **NPCs resolved
automatically**, and if 2 runs pass **the friend is out of the game while the
other three continue**.

The mechanic is excellent. The consequence is not.

**A real human could be spectating for the rest of the campaign.** Get lost on
run 3 of a 10-run campaign and that's potentially three or four hours of a
person watching their friends play. No shipped co-op game does this, and the
one time it gets tried in a playtest, that player quits and doesn't come back.

The instinct behind it is right — losing someone must be *permanent* and it must
*hurt*. Here is how to keep all of that and still not bench a person.

### Separate the character from the player

- **The character is gone forever.** Their money, their gear, their story
  thread. If it was the friend whose family is on the bottom floor, that plot
  line is genuinely lost. The crew carries it.
- **The player returns next run as a new recruit** — fresh, broke, no gear, and
  no share of the crew's money.

The punishment lands on the campaign, not on the human holding the controller.

### Make the two rescue runs the best part

While Lost, the player is still *down there*. Give the spectator agency:

1. **They watch through their own eyes**, not a floating camera. They're
   trapped on a floor in the dark.
2. **Proximity voice still works.** When the crew passes their floor, they can
   hear them. Faint, then clear, then gone.
3. **One radio call per run.** A single message the crew hears wherever they are.

That turns 20 minutes of spectating into the most tense thing in the game — and
it's the clip. *"We could hear him the whole time."*

None of this costs much: it's a camera that stays put, a voice channel, and one
button.

### The auto-resolved rescue — make it a scene, not a number

You picked automatic resolution, which is the cheap and correct build. But don't
let the emotional peak happen in a results screen. Play it out over ~40 seconds
between runs: radio chatter, a descent counter, dust. Outcomes should not be
binary:

| Spend | Outcome band |
|---|---|
| Under-spend | Failed. They're still down there. One run left. |
| Adequate | Rescued, but injured — starts next run at reduced health |
| Adequate | Rescued, gear lost — they come back with nothing |
| All-in | Clean rescue |

Partial outcomes are what make the "go all in?" argument real. Binary odds just
make it a coin flip with a price tag.

**This is a proposal, not a decision — tell me if you'd rather the player stay
benched.** But I'd be doing you a disservice not to flag it.

---

# PART 2 — HEALTH, DAMAGE, DOWNED, LOST

You chose **no regeneration — healing only from items or the shop.** That's the
right call and it makes the med supplies compete with rope for money, which is
exactly the pressure this game runs on.

### The state machine

```
  HEALTHY ──damage──► HURT ──damage──► DOWNED ──timer or crew leaves──► LOST
     ▲                  │                 │                              │
     │                  │                 │                      2 runs to buy
     └──── med item ────┴──── revive ─────┘                       a rescue
                                                                        │
                                                              rescued ◄─┴─► gone
```

### Rules

**Health does not regenerate.** It carries between floors and only resets when
you buy treatment at the shop. A bad trap on floor 1 shadows the whole run.

**Injury is visible on the character, not just on a bar.** A limp, a hand
clutching the side, slower climb. Your crew should be able to see you're hurt
from across a room without asking — because in a game with proximity voice,
information you can *see* is information nobody has to waste words on.

**Downed is not dead.** You're on the floor. You cannot climb, you cannot carry.
A bleed-out timer (~90 s) runs.

**Two ways to save a downed player:**
1. **Med spray** — expensive, instant, used where they lie
2. **Carry them out** — free, and they are a `Carryable` with real mass

That second one matters enormously and it's nearly free to build, because
`Carryable`, the weight classes and the rope-clipping already exist. A downed
friend competes with the gold for the same load limit — which is *exactly* the
argument your design doc is built on, applied to a person your crew actually
knows.

**Extraction while someone is downed** should require an explicit confirmation.
Leaving must be a decision somebody makes and everyone hears.

### Injury sources
- Traps (Part 4)
- Falls over ~4 m — scales with height and with what you're carrying
- Threats
- The collapse catching you on a floor being demolished

---

# PART 3 — PUZZLES: HOW TO GET A LOT OF THEM

You asked for many puzzles. The amateur approach is to write thirty puzzle
scripts. The professional approach is to write **eighteen components and author
three hundred combinations**.

## The framework

Every puzzle in this game is **a LOCK, a KEY, and a MODIFIER.**

### LOCKS — what stops you

| # | Lock | Notes |
|---|---|---|
| 1 | Keypad | needs a code |
| 2 | Card reader | needs a physical card |
| 3 | Keyed door | needs a physical key |
| 4 | Powered door | needs power routed to it |
| 5 | Pressure plate | needs mass held on it |
| 6 | Multi-lever | N levers within N seconds |
| 7 | Timed shutter | open for N seconds, then shuts |
| 8 | One-sided door | only opens from the far side |
| 9 | Counterweight | load a platform to raise the door |
| 10 | Silence lock | closes if noise exceeds a threshold |
| 11 | Balance scale | two plates must match within a tolerance |
| 12 | Water level | rise or drain to reach it |

### KEYS — what solves it

| # | Key |
|---|---|
| 1 | A code found written somewhere else |
| 2 | A carried item (card, key, fuse, battery) |
| 3 | Power routed from a generator with limited output |
| 4 | Mass — and the only mass nearby is your loot |
| 5 | Another player, elsewhere, at the same moment |
| 6 | A shop tool (crowbar, multitool) — bought, not solved |
| 7 | An environmental change (flood, drain, restore lights) |
| 8 | Information one player can see and another cannot |

### MODIFIERS — what turns one puzzle into twenty

| # | Modifier | Why it matters |
|---|---|---|
| 1 | The key is **valuable** | spend it or sell it |
| 2 | The key is **heavy** | it costs rope load to carry |
| 3 | The key is **in a trapped room** | solving costs health |
| 4 | The lock is **on the clock** | the collapse is coming |
| 5 | The hint is **on another floor** | you must remember and carry it |
| 6 | The lock is **visible from the rope**, reached from a room | you see the prize long before you can take it |
| 7 | The key is **consumed** | one shot, choose which door |
| 8 | Solving it **makes noise** | attracts a threat |

**12 locks × 8 keys × 8 modifiers.** You author puzzles as data — a ScriptableObject
naming a lock, a key, a modifier and two room sockets. Adding your hundredth
puzzle costs five minutes and no code.

## The test every puzzle must pass

> **Does solving it cost time, weight, rope, light, health, or a body?**

If not, it's a locked door with a riddle taped to it. And per your design doc:
if one player can solve it alone while the others watch, cut it.

## Never gate extraction on a puzzle

Gate loot, gate shortcuts, gate the vault. The way home stays open, or a failed
puzzle becomes a wipe and players stop experimenting.

## The ones from your design doc that are Tier 1

Already specified and worth building first: the **manager's keycard** (floor 2 →
floor 4, occupies a backpack slot the entire way), the **ledger** (one player
reads numbers aloud), **three fuses three rooms**, the **counterweight vault**
(spend your own loot to open it, not knowing what's inside), **power routing**
(one door of two, never learn what was behind the other), the **light plate**,
and **matched dials**.

---

# PART 4 — TRAPS

Traps now do damage. They should also take something *else* — damage alone is a
number, and this game's currencies are richer than that.

| Trap | Damage | Also takes |
|---|---|---|
| **Floor collapse** | fall damage | your position, and whatever you were carrying |
| **Gas leak** | over time | the room becomes a countdown |
| **Rope fray / cutter** | none | **the shared line** — threatens everyone |
| **Blast door lockdown** | none | a teammate, sealed in |
| **Electrified floor** | high | forces a route, holds you still |
| **Falling debris** | high | can down you outright, and blocks a doorway |
| **Alarm** | none | quiet — summons a threat later |
| **EMP / darkness** | none | your headlamp |
| **Weakened floor** | fall damage | holds one player, not two, and never one carrying a safe |
| **Deadfall / crusher** | very high | punishes running |

**The rope fray is the best trap in the game.** It's the only one that attacks
the object every player is holding. Make it visible (strands popping), audible
(a creak that worsens), and repairable at a cost — the patch kit from your shop
list, applied by someone who has to hold still for six seconds while the clock
runs.

**Trap scanner** is already on your shop list. Good — it makes traps a
*resource-management* problem rather than a memorisation problem, which is what
keeps them fun on replay.

---

# PART 5 — THE ROPE REWRITE

Still the biggest technical item, and it now has to carry survivors, downed
players and the Collector as well as loot.

**Build it as a Verlet / position-based chain.** 24–32 nodes, gravity, distance
constraints solved 6–8 times per fixed step. Never Unity joints — a joint chain
under a 400 kg load stretches, jitters and eventually explodes.

Everything falls out of the solver rather than being coded:

- the rope sags under its own weight, and more under load
- pulling it moves it, and pulls from four players sum naturally
- opposite pulls cancel with no special case
- overload sags **visibly** before it frays

### The migration insight that makes this affordable

`PlayerTether`, `RopeHook` and `Carryable` all reach the rope through
`PointAtDepth(depth)`, `Length` and `AnchorPosition`. **Keep those signatures
byte-identical** and reimplement the inside as "find the node at this arc
length, interpolate to its neighbour."

Done right, ~2,000 lines of tether and hook code do not change. That turns a
rewrite into a swap.

### Node count is a network decision

24–32 nodes over 20 m simulates convincingly and stays cheap to replicate. 100
nodes looks marginally better and costs four times the bandwidth. Choose now,
while changing it is free.

### Cargo on the rope

Clipped items should make the rope **deviate around them** — pin two nodes to
two points on the object and let the solver form the hitch. The bow deepens with
mass, so players read weight from the silhouette with no UI at all.

And cargo **occupies a band of rope**. A climber who reaches it must
**Traverse** — a ~1.2 s swing around the load during which they can hold
nothing. With a carabiner, 0.5 s.

This makes the rope a shared queue with a traffic problem. Someone clips a
vending machine at 6 m and everyone below has to get around it. Someone stacks
three crates and walls in the person at the bottom. The clock is running.
Players will invent "clip high, climb low" on their own and then break it under
pressure — and *emergent etiquette can't be spoiled by a wiki*, which is worth
more than any scripted set piece.

It also makes the **Loot Collector** coherent: your design doc already says it
occupies the rope and shoves people aside. With a real rope, that's not a
special case — it's the same band rule with a bigger object.

---

# PART 6 — HOW GAMES ACTUALLY GET FINISHED

This is the part you asked me to research. It's less about code than about the
four or five habits that separate shipped games from six-year abandoned repos.

### 1. Vertical slice before breadth

Finish **one floor completely** — final art, audio, three puzzles, two traps,
one survivor, working extraction — before building ten floors of graybox.

The slice tells you the truth about (a) whether it's fun, (b) how long a floor
actually takes to build, and (c) whether your art pipeline survives contact with
reality. Building ten graybox floors first tells you none of those things and
feels like progress.

### 2. Milestones with a written definition of done

Never "work on rooms." Always **"three room modules, playable end to end, no
placeholder art, 8-minute floor clear time."** A milestone you can argue about
is a milestone you'll slip.

### 3. Measure your own velocity and multiply

Track estimate vs actual for a month. You will find a personal factor — usually
between 1.5× and 3×. Multiply every future estimate by it. This single habit is
the difference between a plan and a wish.

### 4. Feature freeze, then content lock

Pick a date after which **no new features** are added — only content. Then a
later date after which **no new content** — only bugs and polish. Write both
dates down now. Studios that skip this ship late or not at all; the failure mode
is always "one more system."

### 5. Playtest with strangers, weekly, from as early as possible

Your friends will be kind and they already know how the rope works. You need
people who don't. Watch them silently — where they hesitate is your design
document. Once a week, four players, from the moment two-player works.

### 6. A build every week from month one

Not a build when you need one. A Windows build, every week, played outside the
editor. Editor-only assumptions (`Camera.main`, editor scripts, missing shader
variants) fail silently until they don't, and finding out in month fourteen is
how projects die.

### 7. The demo is a marketing asset, not a slice of the game

It should be the most polished 20 minutes you can make, tuned for streamers:
fast to get into, funny within 3 minutes, and it should end on a cliffhanger,
not a fade-out. Your design doc's demo cut list is already right — rope, four
players, three floors, five room types, two puzzle types, load limit, collapse,
one survivor you can choose to leave behind. **No shop, no story text, no shrink
gun.**

### 8. The last 10% is another 90%

Save/load, settings menus, key rebinding, controller support, five aspect
ratios, the pause menu, crash handling, Steam integration, achievements,
localisation. None of it is fun and all of it is required. Budget three months
you haven't planned for, because you will need them.

---

# PART 7 — THE SCHEDULE, WORKING BACKWARDS FROM A REAL DEADLINE

**A game can only ever appear in ONE Steam Next Fest.** You do not get a second
try, so it must be spent on a demo that's genuinely ready.

| Next Fest | Runs | Submission deadline |
|---|---|---|
| October 2026 | Oct 19–26, 2026 | **too soon — 2 months away** |
| February 2027 | Feb 22 – Mar 1, 2027 | 8 Feb 2027 |
| **June 2027** | **June 14–21, 2027** | **31 May 2027** |

February is roughly six months out. With the rope rewrite and netcode both
unbuilt, that is not a demo — it's a rushed one, spending your single Next Fest
on it.

## → Target: **June 2027 Next Fest. Demo submitted by 31 May 2027.**

That's **9.5 months**. Everything below is scheduled against that date.

---

# PART 8 — THE PLAN

### PHASE 0 — Lock down · *this week*

- [ ] **Commit everything.** ~6,000 lines are untracked right now. One crash and
      it's gone. Nothing else on this list matters if this doesn't happen.
- [ ] Delete dead code — `AnimationSetupTool` (legacy, can still overwrite your
      controller), `PlayerProceduralAnim`, `PlayerArms`, `Arm_L`/`Arm_R`
- [ ] First Windows build; play it outside the editor
- [ ] **Steam page up.** Wishlists compound from the day it exists, and the page
      is required before Next Fest registration anyway

---

### PHASE 1 — The rope rewrite · *Sept–mid Oct 2026 (5 weeks)*

- [ ] Verlet node chain, PBD solve in `FixedUpdate`
- [ ] `PointAtDepth()` / `Length` / `AnchorPosition` preserved exactly
- [ ] Player attachment pins a node; pulls propagate and sum
- [ ] Cargo pins two nodes, adds mass, forms the hitch
- [ ] Load sag and visible fray replace the load number
- [ ] Rope tug signals — yank the rope, everyone attached feels it. Two tugs
      = coming up, three = help. Nearly free once the rope is real, and players
      will invent their own code.

**Done when:** three heavy weights hang on it, four simulated pullers swing it,
and it never explodes or jitters over a ten-minute run.

---

### PHASE 2 — Health, downed, cargo traffic · *mid Oct–Nov 2026 (5 weeks)*

- [ ] Health, no regen, visible injury states
- [ ] Downed with bleed-out; med spray revive
- [ ] **Downed player is a `Carryable`** — real mass, competes with loot
- [ ] Lost state, and the shop's rescue contract with banded outcomes
- [ ] Cargo bands + the Traverse move
- [ ] Winch that raises rope-cargo and drags everyone attached
- [ ] Rope fray trap + patch kit

**Done when:** a solo run where clipping loot badly makes your own climb worse.
If you can't yet screw yourself over, the system isn't finished.

---

### PHASE 3 — De-single-player the code · *Dec 2026 (2 weeks)*

Not netcode. Removing assumptions while the game still runs solo and you can
verify nothing broke.

- [ ] `Camera.main` — **9 files** — each player owns its camera reference
- [ ] `FindObjectsByType` — **9 files** — replace with a player registry
- [ ] `Campaign` stops being `static`; run state gets an explicit owner
- [ ] Input, HUD and audio become per-player

Debugging a refactor and a network layer at the same time is how solo projects
stall for six months. Keep them apart.

---

### PHASE 4 — Two players · *Jan–Feb 2027 (8 weeks)* ★ biggest unknown

- [ ] Netcode for GameObjects + Steam transport, host-authoritative
- [ ] Host simulates the rope; clients get node positions at ~20 Hz and interpolate
- [ ] Local prediction for your own body only
- [ ] Downed / revive / Lost replicated
- [ ] **Two** players, not four. Two proves every hard problem; four is a number
      change afterwards.

**Done when:** two players on one rope, opposite pulls cancel, one can carry the
other out.

---

### PHASE 5 — The vertical slice floor · *Mar 2027 (4 weeks)*

**One floor, finished to shippable quality.** Not ten graybox floors.

- [ ] Landing + 3 rooms + vault
- [ ] 3 puzzles from the framework, 2 traps
- [ ] 1 survivor you can choose to leave
- [ ] Final art pass on that floor only
- [ ] Weekly playtests with strangers start here

This is the milestone that tells you the truth. If this floor isn't fun with
two players, no amount of content fixes it — and you'll find out with seven
months still on the clock instead of one.

---

### PHASE 6 — Content · *Apr–mid May 2027 (6 weeks)*

- [ ] Room kit: 6–8 modules with tagged sockets, procedurally arranged
- [ ] Puzzle framework as ScriptableObjects; author 20–30 combinations
- [ ] 5–6 traps
- [ ] 4 players
- [ ] Shop UI with the tool list from `GAME_DESIGN.md`
- [ ] **FEATURE FREEZE: 1 May 2027.** No new systems after this date.

---

### PHASE 7 — Demo polish · *mid–end May 2027 (3 weeks)*

- [ ] **CONTENT LOCK: 15 May 2027.** Bugs and polish only.
- [ ] Audio pass — you asked to defer sound, and this is where it lands. Rope
      creak under load, breathing that worsens with weight, the collapse
      approaching. Cheapest tension in games.
- [ ] End-of-run ledger — who carried what, who got left behind. Blame is content.
- [ ] Menus, settings, rebinding, aspect ratios, crash handling
- [ ] **Submit by 31 May 2027**

---

# PART 9 — WHAT GETS CUT WHEN YOU FALL BEHIND

You will fall behind. Decide now, not in May.

**Cut first, in this order:**
1. **Four players → three.** The rope traffic jam is funnier with three anyway,
   and every player is a network cost
2. **Evidence.** It's the endgame layer; the demo doesn't need it
3. **The Loot Collector.** Massive items can simply not be extractable in the demo
4. **Threats.** Traps and the collapse carry the demo alone
5. **Procedural room arrangement.** Ship three hand-built floors

**Never cut:**
1. **The rope rewrite** — it's the identity of the game
2. **Downed players as cargo** — it's the moment people make videos about
3. **The weight argument** — treasure vs survivor. It is the entire product,
   and it must be in the demo or the demo is just a physics toy

---

# PART 10 — OPEN QUESTIONS

1. **How long is a campaign?** Everything about the Lost mechanic depends on it.
   If a campaign is 8–10 runs, losing someone on run 3 is brutal in the right
   way. If it's 30, it's just a person who stopped playing.
2. **Rescue contract price** — flat, or scaled by the floor they're lost on?
   Scaling makes deep runs genuinely frightening.
3. **Does the crew know exactly where they are?** Knowing the floor makes it a
   plan. Not knowing makes it a horror story.
4. **Is the friend with family on the bottom floor a fixed character?** If so,
   losing *that* one should hit differently from losing anyone else — and that's
   a story beat worth protecting.

---

**Sources for the Next Fest dates and the one-time participation rule:**
[Steam Next Fest: February 2027](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/feb_2027) ·
[Steam Next Fest: June 2027](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/june_2027) ·
[Steam Next Fest: October 2026](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/2026october) ·
[Steam Next Fest overview](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest)
