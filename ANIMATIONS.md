# SAFE DEPOSIT — Animation System

Everything you need to download, and why the system is built the way it is.

---

## PART 1 — THE IDEA THAT MAKES THIS MANAGEABLE

You asked: *"sometimes he can walk while carrying stuff so some animation need to
work together, or should I bring a carry-walk animation?"*

Answer: **no, and this is the single most important decision in the whole system.**

Count what happens if you use one clip per combination:

| | Idle | Walk F | Walk B | Strafe L | Strafe R | Run |
|---|---|---|---|---|---|---|
| empty hands | 1 | 2 | 3 | 4 | 5 | 6 |
| small box | 7 | 8 | 9 | 10 | 11 | 12 |
| heavy crate | 13 | 14 | 15 | 16 | 17 | 18 |
| massive | 19 | 20 | 21 | 22 | 23 | 24 |

24 clips. Add "holding a torch" and it is 30. Add "wounded" and it is 36. This is a
**combinatorial explosion**, and it is how animation budgets die.

Now split the body in half:

- **Legs and hips** only ever do locomotion — 6 clips.
- **Arms and chest** only ever do the hand job — 4 clips.

**6 + 4 = 10 clips, and they cover all 24 combinations, plus every combination you
haven't invented yet.** Carrying a crate while strafing backwards works on day one
without anyone animating it.

That split is what an **Animator Layer with an Avatar Mask** is. The mask says
"this layer only writes to arms, chest and head". The base layer keeps walking
underneath, untouched.

This is why you do not need a carry-walk clip. If Mixamo happens to have one, we
use it to sweeten the blend — but nothing depends on it.

---

## PART 2 — WHAT TO DOWNLOAD FROM MIXAMO

### Download settings — get these right or nothing works

For **every** clip except the very first one:

| Setting | Value | Why |
|---|---|---|
| Format | **FBX Binary (.fbx)** | Unity reads it directly |
| Skin | **Without Skin** | You only need the mesh once. With Skin ships a full duplicate of your character in every file — that is what caused those "self-intersecting polygon" warnings and the 4 MB files |
| Frames per Second | **30** | 60 doubles file size for motion nobody sees |
| Keyframe Reduction | **none** | Reduction on top of Mixamo's own compression makes feet slide |

**In-place matters.** Any clip with an "In Place" checkbox — Walking, Running,
strafes — **tick it**. Your Rigidbody does the moving. If the clip also moves the
character, the two fight and you get sliding or drifting.

### Naming — this is not optional

Save every file as **`Player@<Mixamo name>.fbx`** into `Assets/_Project/Models/`.

The `@` is a real Unity convention, not a habit of mine. `Model@Clip.fbx` tells
Unity "this file's animations belong to the rig in `Model.fbx`". It is also how
the build tool finds your clips.

Example: `Player@Walking Backwards.fbx`

---

### Tier 1 — required. Nothing feels finished without these.

Search the term in the left column on mixamo.com.

| Search for | Save as | Used for |
|---|---|---|
| `Breathing Idle` | `Player@Breathing Idle.fbx` | standing still — calmer than Happy Idle for a heist |
| `Walking` ✔ have | `Player@Walking.fbx` | forward |
| `Walking Backwards` | `Player@Walking Backwards.fbx` | reversing out of a room |
| `Left Strafe Walking` | `Player@Left Strafe Walking.fbx` | sidestep left |
| `Right Strafe Walking` | `Player@Right Strafe Walking.fbx` | sidestep right |
| `Standard Run` | `Player@Standard Run.fbx` | sprinting for the extraction |
| `Jumping Up` | `Player@Jumping Up.fbx` | the launch |
| `Falling Idle` | `Player@Falling Idle.fbx` | airborne loop |
| `Hard Landing` | `Player@Hard Landing.fbx` | the landing |
| `Climbing A Rope` ✔ have | `Player@Climbing A Rope.fbx` | up and down the main rope |
| `Hanging Idle` | `Player@Hanging Idle.fbx` | dangling / swinging |
| `Box Idle` ✔ have | `Player@Box Idle.fbx` | carrying, arms layer |
| `Picking Up` | `Player@Picking Up.fbx` | grab loot |

That is **13 clips** and it covers the entire moment-to-moment game.

### Tier 2 — strongly recommended. This is where it starts feeling real.

| Search for | Save as | Used for |
|---|---|---|
| `Putting Down` / `Box Put Down` | `Player@Putting Down.fbx` | clipping loot to the rope, stowing in the pack |
| `Button Pushing` | `Player@Button Pushing.fbx` | keypads, puzzle switches, the winch |
| `Pulling` | `Player@Pulling.fbx` | dragging a massive item |
| `Falling Back Death` | `Player@Falling Back Death.fbx` | death |
| `Standing React` / `Stunned` | `Player@Stunned.fbx` | trap hits, hit by falling debris |
| `Waving` | `Player@Waving.fbx` | emote — **this is your marketing** |
| `Pointing` | `Player@Pointing.fbx` | emote — "loot over there", works without voice chat |

### Tier 3 — the viral clips. Do not skip these before Next Fest.

Content creators clip **emotes**, not walk cycles. PEAK, REPO and Content Warning
all spread on silly poses.

| Search for | Save as |
|---|---|
| `Hip Hop Dancing` | `Player@Hip Hop Dancing.fbx` |
| `Silly Dancing` | `Player@Silly Dancing.fbx` |
| `Clapping` | `Player@Clapping.fbx` |
| `Salute` | `Player@Salute.fbx` |
| `Shrugging` | `Player@Shrugging.fbx` |

### Two things I would add that you did not ask for

**`Player@Stunned.fbx`** — when a trap hits you, you need a half-second where the
player is *not in control* and can see they are not in control. Without a hit
reaction, damage feels like a number changing. With one, it feels like an event.
This is the cheapest game-feel win on the list.

**`Player@Hanging Idle.fbx`** — you already have a climb clip, but the state your
players will spend the most time in on the rope is *hanging still while arguing
about what to do next*. A frozen climb frame looks like the game crashed. Hanging
Idle is the clip your screenshots will be full of.

### Clips you asked about that you should NOT download

- **"walk backward while carrying"** — the mask gives you this free.
- **"backpack stow"** — Mixamo has no reach-behind-your-back clip that will look
  right. We reuse `Putting Down` on the arms layer. Nobody will notice, because
  the pack is behind your head in first person and half a second long in third.
- **"climb down"** — we play `Climbing A Rope` at **negative speed**. An Animator
  state can have its playback speed driven by a parameter; set it to `-1` and the
  clip runs backwards. One clip, both directions.

---

## PART 3 — HOW IT IS WIRED

### Parameters

| Name | Type | Set by | Meaning |
|---|---|---|---|
| `MoveX` | Float | driver | strafe, −1 left … +1 right, relative to where the model faces |
| `MoveZ` | Float | driver | −1 backward … +1 walk … +2 run |
| `Speed` | Float | driver | horizontal speed in m/s, for the carry blend |
| `Grounded` | Bool | driver | feet on floor |
| `Jump` | **Trigger** | driver | fires once at takeoff |
| `Climbing` | Bool | driver | on the rope and off the floor |
| `ClimbSpeed` | Float | driver | 0 = hanging still, 1 = climbing hard |
| `ClimbDir` | Float | driver | +1 up, −1 down — this is the state's playback speed |
| `Carry` | Int | driver | 0 none, 1 small, 2 heavy, 3 massive |
| `DoPickUp` | Trigger | driver | hands went from empty to full |
| `DoStow` | Trigger | driver | something entered the backpack |
| `DoUse` | Trigger | driver | keypad / winch |
| `Emote` | Int | driver | which emote |
| `DoEmote` | Trigger | driver | play it |
| `Dead` | Bool | driver | dead |

**Trigger vs Bool** is worth understanding. A Bool stays true until something sets
it false — if you used a Bool for Jump you would jump forever. A Trigger is
**consumed** by the first transition that reads it and resets itself. Use a Trigger
for anything that is an *event*; use a Bool for anything that is a *condition*.

### Layer 0 — Base (the whole body)

```
                    ┌─────────────┐
    Jump trigger    │             │  !Grounded
        ┌───────────┤ Locomotion  ├──────────┐
        │           │ (blend tree)│          │
        ▼           └──────▲──────┘          ▼
   ┌────────┐              │            ┌─────────┐
   │ JumpUp │              │ exit 80%   │ Falling │
   └───┬────┘              │            └────┬────┘
       │ exit 70%     ┌────┴────┐            │ Grounded
       └─────────────►│ Landing │◄───────────┘
                      └─────────┘

   AnyState ──Climbing──► Climb (blend tree)  ──!Climbing──► Locomotion
   AnyState ──Dead──────► Death
```

**Why the jump is three states, not one clip.** A jump is not a fixed-length event
— you do not know how long you will be in the air. Drop off a ledge and you might
fall for two seconds. One clip either ends early and freezes on the last frame, or
runs long and lands after your feet already touched. Splitting it into
launch → *loop* → land means the airborne part stretches to fit reality.

**Locomotion is a 2D Freeform Directional blend tree**, not five states:

```
                 (0, 2) Standard Run
                        │
                 (0, 1) Walking
                        │
  (-1,0) ─────────── (0,0) ─────────── (1,0)
  Left Strafe        Idle           Right Strafe
                        │
                 (0,-1) Walking Backwards
```

Five separate states would need 20 transitions between them and would *pop* every
time you changed direction mid-stride. A blend tree interpolates in **muscle
space**, so walking diagonally forward-left produces a genuine diagonal walk that
nobody animated, and turning is continuous.

**Climb is a 1D blend tree** on `ClimbSpeed`: `Hanging Idle` at 0, `Climbing A Rope`
at 1. Hang still on the rope and you get the hang; climb and it blends in. The
state's *speed* is driven by `ClimbDir`, so descending plays the same clip
backwards.

### Layer 1 — Arms (masked: chest, arms, hands, head)

```
   None (empty, default) ◄──── Carry == 0 ────┐
      │                                        │
      ├── Carry > 0 ──────────► CarryIdle / CarryWalk (blends on Speed)
      ├── DoPickUp ───────────► PickUp ──exit──► None
      ├── DoStow ─────────────► Stow   ──exit──► None
      ├── DoUse ──────────────► Use    ──exit──► None
      └── DoEmote + Emote==n ─► Wave / Point / Dance / Clap ──exit──► None
```

The default state is **empty on purpose**, with *Write Defaults off*. An empty
state with Write Defaults off writes nothing at all, so the base layer shows
through untouched. That is how the layer "turns itself off" without the script
having to manage layer weight every frame.

**One place the mask must be overridden:** while climbing, both hands are on the
rope. If the arms layer were still holding a carry pose, the crate would clip
through the rope. So the driver fades the arms layer weight to **0** whenever
`Climbing` is true, and back to 1 when it is not.

---

## PART 4 — RUN IT

1. Download the clips above into `Assets/_Project/Models/` with the `Player@` names.
2. Unity → **SAFE DEPOSIT → Animation → Build Full Animator**.
3. Read the Console. It lists every clip it found and every one it could not, so
   you always know exactly what is missing.
4. Press **Play**, then **V** for third person.

The build tool is safe to run as many times as you like. Download five more clips,
run it again, and they are wired.

### Controls added

| Key | Emote |
|---|---|
| `Z` | wave |
| `X` | point |
| `C` | dance |
| `B` | clap |

---

## PART 5 — WHAT COMES AFTER THIS

Not yet built, in the order I would do it:

1. **First-person arms.** Parent the camera to the head bone, cull only the head
   mesh, and add a third layer for FP-specific arm poses. You already have most
   of this — `LocalFirstPersonBodyCull` does the culling.
2. **Foot IK.** Feet plant on stairs and rubble instead of hovering. One
   `OnAnimatorIK` callback, roughly 40 lines, and it makes low-poly characters
   look far more expensive than they are.
3. **Look-at IK.** Your head turns toward where your camera points, so the other
   three players can see what you are looking at. In a co-op game about pointing
   at things, this is worth more than another twenty clips.
