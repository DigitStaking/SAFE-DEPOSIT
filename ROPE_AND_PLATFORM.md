# SAFE DEPOSIT — The Rope, the Cargo, and the Platform

Answering three things: how loot attaches to the rope, how you climb past it,
and whether the elevator idea is right.

---

# PART 1 — THE PROBLEM YOU SPOTTED IS REAL

> *"if the rope very long people need to click shift a lot to go to floor 20,
> it's gonna be boring"*

**You're right, and it's the single biggest threat to the late game.** By round
30 a full ascent is 100+ metres of holding one key. That's not tension, it's a
loading screen you have to participate in.

Level design has a standard answer for this, and it isn't "add fast travel." It's:

> **New abilities speed up traversal in areas you have already earned.**

That's the Metroidvania rule. The first climb up a shaft should be work. The
fiftieth should not — but the thing that makes it fast must be something you
*bought*, so the speed feels like progress rather than an apology for bad design.

Your elevator instinct is exactly this. But the version matters enormously.

---

# PART 2 — THE ELEVATOR: YES, BUT IT MUST RIDE THE ROPE

## Why a free-standing elevator would hurt the game

An elevator with its own shaft and a floor dashboard is a **menu**. Click 12,
watch a fade, arrive. And the moment that exists:

- The rope stops being the shared object — you're in a box, not on a line
- The weight limit stops being felt — a lift either moves or it doesn't
- Pulls stop summing, swings stop happening, tugs stop meaning anything
- The traffic jam disappears, and with it the arguing

You'd have solved the tedium by deleting the identity of the game.

## The version that solves it and keeps everything

> **A cargo platform that hangs FROM the main rope.**

Not a lift in a separate shaft. A steel platform clamped to your rope, with a
control box on the railing. You stand on it. The loot goes on it. It climbs
**the rope you already own**, at the depth your rope already reaches.

| | Free-standing lift | **Platform on the rope** |
|---|---|---|
| Fixes the boring climb | ✅ | ✅ |
| Dashboard, pick a floor | ✅ | ✅ |
| Weight limit still felt | ❌ | ✅ — same 550 kg, shown on a gauge |
| Rope still the shared object | ❌ | ✅ |
| Swinging, pulls, tugs survive | ❌ | ✅ |
| Rope length still = progression | ❌ | ✅ — the platform can only reach where your rope does |
| Costs you an art/tech budget | new shaft, new system | one prop + a UI panel |

**Everything you liked about the elevator, none of what it would have cost you.**

## How it works

**The control panel** is on the platform railing: a column of floor buttons, lit
for floors your rope reaches, dark for floors that don't exist yet, **red for
floors already demolished.** That panel is a map, a progress bar and a graveyard
in one object, and players will look at it every single round.

**The big button** at the bottom: `RETURN TO SURFACE`. That's your "go pay the
mafia" — it's the ascent vote, on a physical object, that everyone can see
someone reaching for.

**It is slow.** Roughly 2 m/s. Fast enough to not be boring, slow enough that
the trip is a conversation. Twenty floors is about 40 seconds — which is exactly
the length of a good argument about whether to go back for the man.

**It is loud.** A motor whine that carries up and down the shaft. Anything down
there knows where you are.

**It moves the whole rope.** Everyone clipped on gets dragged with it. Somebody
mid-climb when it starts is *going for a ride*.

**The weight gauge is on the panel.** Green, amber, red. Load it past 550 and it
won't move — and now four people are standing on a platform doing arithmetic out
loud while a timer runs.

## When you get it — round 5, as you said

Rounds 1–4 you climb by hand. That's the tutorial: you learn what the rope is,
what it weighs, what it feels like to haul a crate up 15 metres with your arms.

**Then you buy the thing that means you never have to do it again.** That's the
best possible feeling for a purchase, and it only works because the first four
rounds were genuinely tiring.

**Price: 250.** More than three ropes. It should hurt, and it should be obvious
that it's worth it.

## Keep the manual climb

The platform doesn't delete climbing — it's slow, it's loud, and it's *one
place at a time*. You'll still climb by hand to:

- Nip up one floor while the platform is somewhere else
- Escape when it's at the bottom and something is coming
- Move quietly, because the motor is a dinner bell
- Get past it when someone parked it above you

---

# PART 3 — CARGO ON THE ROPE: HOW IT ATTACHES

You asked for it to look like the rope is really carrying the load, not a crate
glued to a line. Here's how riggers actually do it, and what's cheap to build.

## The three real methods, and which to use

**1. The barrel hitch** — the rope wraps *around* the object and cinches. Used
for crates and barrels. Looks the most "rope-like."

**2. The sling** — a loop of rope goes under the object, both ends clip to the
main line, forming a V above it. Used for anything awkward.

**3. The lanyard** — a short cord from the main rope to a hook on the object.
Used for small things.

### Use all three, chosen by weight class. It's free readability.

| Class | Attachment | On the rope | Blocks climbing? |
|---|---|---|---|
| **Small** | short lanyard, hangs off to one side | dangles beside the rope | **No** — lean past it |
| **Heavy** | sling, V above the object | rope forks around it | **Yes** — Traverse |
| **Bulk-heavy** | barrel hitch, rope wrapped twice around | rope visibly *deforms* around the mass | **Yes** — Traverse, slowly |

A player looking up the shaft can tell what's hanging there and whether it's in
their way, **from the silhouette alone.** No icons, no UI.

## How to build it with a Verlet rope

The trick: **you don't wrap a simulated rope around a mesh.** You fake it in two
layers, and nobody has ever noticed.

**Layer 1 — the deviation.** Pin two rope nodes to two attachment points on the
object. The solver pulls the chain into a V or a bow around it. This is real
physics and it's what makes the mass *readable* — a heavy item bows the rope
visibly, and the bow deepens with weight.

**Layer 2 — the hitch mesh.** A small pre-made loop-of-rope model, scaled to the
object's bounding box, parented to it. Same material as the rope. It sits exactly
where the two pinned nodes are, so it reads as one continuous rope wrapping the
crate.

Two nodes and one prop. That's the whole effect.

**Add the sag.** The rope should bow outward at every load, deeper with mass.
Players will learn to read the weight of a haul by looking at the shape of the
line — which is the kind of thing people write comments about.

---

# PART 4 — CLIMBING PAST LOOT

## Small items: no move needed

They hang on a lanyard **off to the side of the rope**. You lean and pass. It
costs nothing and it means the rope doesn't become a wall of obstacles every
time somebody clips a can of beans to it.

## Heavy and bulk: the Traverse

Hold a key while adjacent. Over ~1.2 seconds your character swings around the
load and re-grips above it.

- **You cannot hold anything in your hands** while traversing — it auto-stows or
  you drop it
- You are slow, and both hands are busy
- **The carabiner (45)** cuts it to 0.5 s

## Why this stays in even though you now ascend together

You go up as a crew, once — but during the ten minutes on a floor, people are
constantly moving up and down between the landing and the rooms, and cargo gets
clipped as it's found. So the rope fills up *during* the round, and the traffic
problem is a **mid-round** problem, not an ascent problem.

Someone clips a water tank at 6 m and now everyone coming back from the side
room has to get around it. The clock is running. That's the comedy engine, and
it survives the change completely.

**And when the platform exists, it changes the calculus again** — the platform
sweeps past everything, but only if the total is under 550. So a rope hung with
junk is a rope the platform can't lift.

---

# PART 5 — GOING DOWN SHOULD ALWAYS BE FAST

The tedium is one-directional. **Falling is already fun; climbing is the chore.**
So make the asymmetry explicit:

- **Down: rappel.** Hold a key and drop fast, brake with the other. A 20-floor
  descent in a few seconds, slightly dangerous, entirely under your control.
- **Up: the platform.** Slow, loud, shared, weight-limited.

This is the one-way-shortcut principle from level design, and it does something
else useful: **going deeper is easy and coming back is hard.** Which is exactly
the feeling a game about a collapsing building should have.

---

# PART 6 — THE FLOOR SHAPE, WITH THE BACK ROOM REMOVED

As you asked. Every floor is now:

```
   shaft ──► LANDING ──► MAIN ROOM ──► SIDE ROOM
```

- **Landing** — the only safe unclip point. Regroup, stage loot, argue
- **Main room** — always open. 1–2 loot items
- **Side room** — always open. 1 loot item

And **one floor in four has a SEALED ROOM** off the landing, behind a puzzle.

> **Everything that matters is behind a puzzle. Every rare item, every survivor,
> every document.** There is no other way to get them.

That's a strong rule and I'd keep it exactly as you stated it. It means:

- A puzzle is never a detour — it is always *the* objective
- The crew never argues about whether a puzzle is worth doing
- Finding a sealed room is instantly exciting, because it can only contain
  something that matters
- **1 puzzle per 4 rooms** falls out naturally, with no placement algorithm

The trade: normal floors are pure looting and pure speed. That's fine — it gives
the game a rhythm. Three fast floors, then a floor where everyone stops and
thinks.
