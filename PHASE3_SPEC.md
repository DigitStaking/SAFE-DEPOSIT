# SAFE DEPOSIT — Phase 3: De-single-player

Removing the assumption that there is exactly one of everything, **while the
game still runs solo the entire time**.

Corresponds to **Block 3** in `DEMO_PLAN.md` — 2 weeks, 19 Oct – 1 Nov 2026.
Written to be handed to Claude Code **one step at a time**, exactly like
`ELEVATOR_SPEC.md` and `PHASE2_SPEC.md`.

Written 21 Aug 2026, after Phase 2 shipped, from a survey of the code as it
actually stands rather than from the estimates in `ROADMAP.md`.

---

# PART 1 — WHAT THIS PHASE IS FOR

**This is not netcode.** Nothing connects to anything. At the end of it the
game still launches into a single-player scene and plays exactly as it does
today.

What changes is that every place currently saying *"the player"*, *"the
camera"*, *"the HUD"* says *"which one"* instead. Phase 4 is hard enough
without also discovering, mid-netcode, that `Camera.main` returns whichever
camera Unity felt like and that shrinking "the" head bone hid a teammate's
skull.

## The one-sentence test

> Drop a **second player prefab** into `Prototype.unity`, press Play, and
> both bodies work.

Not "both are controllable" — one of them can stand there doing nothing.
Both *work*: two cameras that do not fight, one HUD, two health values, two
headlamps, and a load gauge that charges 140 kg.

That test is runnable by one person at a desk, which is the whole reason this
phase exists as its own block instead of being absorbed into Phase 4.

---

# PART 2 — THE SURVEY

Counted 21 Aug 2026. `ROADMAP.md` said "9 files and 9 files"; the real
picture is more lopsided than that, and lopsided in a useful direction.

## `Camera.main` — 14 calls in 8 files

| File | Calls | Means |
|---|---|---|
| `ElevatorDashboard.cs` | 4 | the camera of the player *using the panel* |
| `PlayerHeadlamp.cs` | 3 | my own eye, for aim |
| `FirstPersonHands.cs` | 2 | my own eye, for IK targets |
| `PlayerMotor.cs` | 1 | my own, for move direction |
| `PlayerCarry.cs` | 1 | my own, for the pickup ray |
| `DownedPlayer.cs` | 1 | my own, to clamp the look arc |
| `LocalFirstPersonBodyCull.cs` | 1 | my own, to hide my own head |
| `AtmosphereBootstrap.cs` | 1 | the local viewer |

**Every single one means "mine".** None of them wants a global. That makes
this mostly mechanical: each becomes a reference resolved from the player's
own hierarchy.

## `FindFirstObjectByType` / `FindObjectsByType` — 18 calls in 12 files

Three different kinds, and only one is a problem:

| Kind | Count | Verdict |
|---|---|---|
| **World singletons** — `RunManager` ×4, `Elevator` ×3, `SceneAtmosphere`, `RealisticSmokeVolume` | 9 | Correct. There *is* one lift. Cache them; do not re-architect them. |
| **A player** — `PlayerMotor` ×2, `PlayerCarry`, `DownedPlayer`, `Animator` | 5 | **The actual work.** Each has to answer "which player". |
| **Collection sweeps** — all `Carryable` ×2, all `PlayerMotor` ×2, all `LootItem`, all `Light`, all `Camera` | 6 | Correct by nature. Some are per-frame and want caching. |

## `Campaign` — and the finding that shrinks this phase

`ROADMAP.md` says *"`Campaign` stops being static."* **It should not.** Of
the state it holds, almost all of it is genuinely, designedly shared —
`ECONOMY_AND_CAMPAIGN.md` Part 6: *"All loot goes into one pot."*

| Shared — stays exactly as it is | Per-player — has to move |
|---|---|
| `Money`, `CableLength`, `RunNumber`, `CapacityUpgrades` | **`Health`** |
| `CableBoughtThisRound`, `CapacityBoughtThisRound` | **`BleedOutLeft`** |
| `CampaignOver`, `EpitaphReason`, `DestroyedRooms` | **`PlayerLost`** |
| `LootRoster`, `LootSeeded`, `LostCrew`, `CableStrain` | **`BackpackSlots`** ⚠️ |

**Four fields.** That is the entire per-player migration, and three of them
are the ones Phase 2 deliberately parked in `Campaign` so they would survive
a scene reload — which they still must, per player.

⚠️ `BackpackSlots` is a design question, not just a refactor: is a pack
upgrade bought for the *crew* or for *a person*? `ECONOMY` Part 6 says the
leader "assigns permanent items to specific players", which implies
per-player. **Decide this before Step 4, not during it.**

## HUD — 9 `OnGUI` drawers

| Player-owned — must draw for the local player only | World-owned — correct for everyone already |
|---|---|
| `PlayerHealth`, `PlayerCarry`, `PlayerBackpack`, `DownedPlayer` | `Elevator`, `ElevatorBridge`, `ElevatorDashboard`, `CableWear`, `RunManager` |

---

# PART 3 — THREE THINGS THAT WILL BREAK LOUDLY

Not speculation. These are specific and they are already written.

**1. `LocalFirstPersonBodyCull` shrinks the Head bone to 0.0001.** It runs on
the player it is attached to. Put a second player in the scene and *their*
head vanishes too — you would be looking at a headless teammate. This is the
first thing the two-body test will show, and it is the clearest possible
demonstration of why the phase is needed.

**2. `ElevatorDashboard` disables `FirstPersonCamera` and zeroes
`externalSpeedLock`** when somebody presses F. Those are *the* camera and
*the* motor. With two players, one person using the panel freezes the other.

**3. `PlayerHeadlamp` is unparented and repositioned every `LateUpdate`** from
a head bone it found with `FindFirstObjectByType<Animator>()`. Two players,
one lamp, attached to whichever animator Unity returned first.

---

# PART 4 — BUILD ORDER: SEVEN STEPS

**One step per session.** Each ends with a game that runs solo and a commit.

### Step 1 · The player registry
A static `PlayerRegistry` players add themselves to in `OnEnable` and remove
in `OnDisable`, exposing `All` and `Local`. Replaces the 5 player-hunting
`FindFirstObjectByType` calls. Also caches the 9 world-singleton lookups —
including `RunHudGate`, which currently does a scene-wide search **on every
`OnGUI` call from all 9 drawers, twice a frame**.
**Done when:** nothing in a gameplay path searches the scene for a player.

### Step 2 · A player knows if it is local
One `bool IsLocal` on the player, true for everyone in solo. Every
"local-only" behaviour reads it: the body cull, the headlamp, the hands, the
HUD drawers, input.
**Done when:** setting it false on a player leaves a fully working body that
draws no HUD and hides no heads.

### Step 3 · Every player owns its camera
Delete all 14 `Camera.main` calls. Each player resolves its own camera from
its hierarchy; `FirstPersonCamera` registers itself with its owner.
**Done when:** two player prefabs in the scene, neither steals the other's
camera, and step 2's non-local body renders its own head.

### Step 4 · Per-player state
`Health`, `BleedOutLeft`, `PlayerLost` and (pending the decision above)
`BackpackSlots` move out of `Campaign` into per-player state that still
survives a scene reload. `Campaign` keeps the shared pot and stays static.
**Done when:** two players can be on different HP across a reload.

### Step 5 · The crew is a list, not a player
`RunManager`'s `crew`, `CrewStanding`, extraction, the collapse's
occupant check and `CountRecoveredValue` all work for N. `Campaign.LostCrew`
already stores names — Step 5 makes them real.
**Done when:** a run ends with one of two players missing and the results
screen names the right one.

### Step 6 · Input per player
`PlayerInput` device pairing, so two bodies can be driven by two devices.
Keyboard for one, gamepad for the other, on one machine.
**Done when:** two players move independently at the same desk.

### Step 7 · The two-body test
Second player prefab in `Prototype.unity`. Everything above, proven at once.
**Done when:** two bodies ride the lift, the gauge reads 140 kg, one carries
the other after a fall, and only one HUD is on screen.

---

# PART 5 — WHAT IS EXPLICITLY *NOT* HERE

- **Netcode.** Phase 4. Nothing serialises, nothing connects.
- **Audio per player.** `ROADMAP` lists it; there is no audio yet. Phase 8.
- **Split-screen as a shipped feature.** Step 7 is a *test rig*, not a mode.
- **Revive and the rescue contract.** Moved into Phase 4 from Phase 2 — they
  need real players, not two prefabs on one machine.

---

# PART 6 — THE FOUR RULES (unchanged)

1. **One step per session.**
2. **Explanation before code.**
3. **Commit after every step.**
4. **Read before write.**

And the two things it still cannot do: **drag things in the Unity editor** —
so the second player prefab in Step 7 gets placed by an editor script, like
everything else — and **see your game**, so keep sending screenshots.
