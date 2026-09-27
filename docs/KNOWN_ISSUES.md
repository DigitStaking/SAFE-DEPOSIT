# SAFE DEPOSIT — Known Issues

Open faults found by audit on 9 Sep 2026, plus what the design docs carry.
**None of these were fixed during the audit.**

Severity: **BLOCKER** stops work · **HIGH** wrong behaviour · **MEDIUM** costs
time or risks a mistake · **LOW** tidy-up.

---

## FIXED 9 Sep 2026 — Phase 5 floor generation now runs and validates

Executed in Unity batch mode (`-executeMethod`), not asserted:

```
Build Room Set        -> 7 prefabs, 0 errors
Validate Twenty Seeds -> 20 valid, 0 not
                         1-door x155  2-door x36  3-door x33  4-door x51
Preview Ten Floors    -> all ten generated, all ten passed validation
                         11-14 rooms, 3-5 junctions, 5-9 dead ends, depth 3-4
```

Two things were wrong and are fixed:

1. **The preview measured a different building.** It passed `Vector3.zero` as
   the lift doorway while the runtime passes `(7.5, 0, 0)`, so every start room
   was planted in the middle of the shaft and correctly rejected with *"the
   start room would sit inside the shaft"* — ten times out of ten. The
   generator was never wrong. Both now read `FloorDirector.ShaftDoorwayLocal`
   and `ShaftKeepOutFor`, which are the only definitions.

2. **Two floors in ten could not be placed at all.** The graph almost always
   asked for the full 14 rooms, and 14 rooms of 9-16m do not always pack around
   a shaft. Retries now lower the room *ceiling* toward `minRooms` as attempts
   go on — so a floor that cannot be fourteen becomes eleven rather than
   becoming nothing. **No constraint was relaxed**: every door still connects,
   nothing overlaps, nothing enters the shaft.

Still true: **this is validation, not play.** Nobody has walked these floors.

---

## HISTORICAL — Phase 5 floor generation had never run successfully

**Where:** `FloorGenerator.cs`, `FloorGraph.cs`, `FloorValidator.cs`,
`FloorDirector.cs`, `Editor/FloorPreview.cs`.

The generator was rewritten several times on 5 Sep 2026 and the final version
**has not been compiled or run.** The last recorded run failed on every floor
with *"room 0 ran out of doors"*; that specific bug was fixed (the entrance
door is a connection and the root was being asked for one child too many) and
the fix was verified **only as arithmetic reproduced in Python**, never in
Unity.

What is verified: graph topology — degree equals door count on every node
across 20 seeds, the room cap holds, ≥3 junctions and ≥6 dead ends.

What is **not** verified: that real prefabs at real rotations place without
overlapping, that the shaft keep-out works, and that the runtime lifecycle
functions at all.

**Next action:** compile, then `Tools → Rooms → Build Room Set`, then
`Tools → Rooms → Validate Twenty Seeds`.

---

## FIXED 9 Sep 2026 — `FloorDirector` needed a scene object and had none

It was a `MonoBehaviour` whose GUID appeared **zero times** in
`Prototype.unity`. `Elevator` called `FloorDirector.Instance?.ShowFloor(...)`,
the null guard swallowed it, and the entire floor system did nothing — with no
error and no warning. **A required manual step that fails silently is the worst
kind.**

It installs itself now, using the pattern `AtmosphereBootstrap` already uses
for `SceneAtmosphere`: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`,
create the object if absent, `DontDestroyOnLoad` so a between-round scene
reload does not lose it. A hand-placed instance still wins, so putting one in a
scene to tune its fields keeps working.

No new manager was added — `FloorDirector` *is* the floor lifecycle; it simply
stopped waiting to be placed.

---

## FIXED 9 Sep 2026 — floors were destroyed when the lift left them

The first lifecycle held **one** floor and destroyed it whenever the lift went
elsewhere. That is indefensible in a four-player game: player A stands on floor
1, player B rides to floor 2, and floor 1 is deleted underneath A.

*"Only the current floor exists"* was the wrong optimisation. The right one is
**"only floors that are actually unlocked exist"** — twenty floors of geometry
is the thing worth avoiding, and a crew with 15m of cable has three.

Geometry now follows **unlock**, not the lift:

| State | Geometry |
|---|---|
| beyond the cable | nothing built |
| unlocked | built, and it **stays** built |
| sealed | destroyed — **but only once nobody is standing on it** |

Leaving a floor does nothing at all. `AnyoneOn(floor)` checks every player's
height against the lift's own `surfaceY` / `floorHeight` before any destroy, so
a room sealing while the crew is still inside cannot drop them through the
world — the next reconcile tries again once the seal has resolved.

## FIXED 9 Sep 2026 — you could not jump in a generated room

`PlayerMotor.groundMask` on the player prefab is **`m_Bits: 512`** — 2^9,
layer 9, **Environment only**. `Grayboxbuilder` puts the whole shaft on that
layer; `RoomModuleBuilder` set no layer at all, so generated rooms were on
**Default**.

The colliders still worked, so nobody fell through. But the ground *check* is
masked, so it never hit a generated floor, `grounded` stayed false, and
`Jump()` returned early. **Standing on a floor the ground check cannot see is
indistinguishable from falling** — and the same flag drives fall damage, the
procedural legs and the animator, so all of them were wrong too.

Fixed by giving `RoomModuleBuilder.Finish` the same `SetLayerRecursive` call
`Grayboxbuilder` already used. Verified on disk: all seven prefabs now read
`m_Layer: 9`.

**The lesson is bigger than the line.** This slipped past every check in
`FloorValidator` — doors, overlaps, reachability, shaft clearance, branching.
All of it geometry, none of it layers. A room can be geometrically perfect and
completely unplayable, and nothing was watching for it.

## MEDIUM — nothing validates that rooms are on a playable layer

The bug above can recur. `RoomModuleValidator` checks the module contract;
neither it nor `FloorValidator` asserts that room geometry sits on a layer
`PlayerMotor.groundMask` actually includes.

## FIXED 10 Sep 2026 — loot was placed before the floors it belongs to exist

`LootSpawner.Start()` filled all twenty floors at once. Every `Start()` runs
before the first `Update()`, and `FloorDirector` generates in `Update`, so not
one floor had a room in it yet. `SpawnItem` asked each level for its loot
sockets, got none, and fell through to the hardcoded three-position `Slots`
grid — correct only for the single room Phase 1 built. The rooms were then
generated on top, putting crates inside walls.

The fallback was doing exactly what it was written for. It was being asked a
frame too early.

**Fixed** by the second of the two options named here originally — defer loot
per floor:

- `Campaign.PendingLootBudget` (floor -> money owed) holds a floor's share
  until it has somewhere to put it. It lives on `Campaign`, not on the
  component, because `ReloadScene` destroys the component between rounds and a
  floor first unlocked in round six must still be owed what round one set aside.
- `LootSpawner.StockFloorIfPending(level, floor)` spends that entry, and
  refuses while the floor reports zero loot sockets — staying owed rather than
  falling back to the grid.
- `FloorDirector` calls it immediately after generating a floor, since it
  already owns floor lifecycle. No new manager.
- `SceneRefs.Loot` added, matching `Run` / `Lift` / `Atmosphere`, so this is
  not a scene search.

**No economy change:** `FillFloor` does `Mathf.Min(itemsPerFloor, available)`.
`itemsPerFloor` is 3 in code and 3 serialized in `Prototype.unity`, and the old
`Slots.Length` was also 3 — so the count per floor is 3 before and after. Only
the positions changed.

**Consequence worth knowing:** the `Slots` fallback in `SpawnItem` is now
unreachable at runtime, because `StockFloorIfPending` never calls `FillFloor`
on a socketless floor. It is left in place rather than deleted — it still
documents what the graybox floors did, and nothing depends on its absence.

**Verified:** compiles, 0 errors. **NOT verified in Play mode** — see CHANGELOG.

## MEDIUM — a floor that fails to generate is not retried

`Generate` returning false leaves that floor on its fixed graybox room, and the
reconcile only runs again when the progression *changes* (cable bought, room
sealed). So a floor that could not be placed stays old until something else
moves.

Low risk in practice — the last measured run generated 10 of 10 — and it fails
in the safe direction: an old floor rather than a hole. Recorded rather than
fixed, because a retry loop that runs on nothing changing is how you get a
generator thrashing every frame.

---

## HIGH — the room prefab set exists twice

- `Assets/_Project/Prefabs/Rooms/` — 7 prefabs, **stale**
- `Assets/_Project/Resources/Rooms/` — 7 prefabs, **live**

`RoomModuleBuilder` now writes only to `Resources/Rooms`; the older copies were
left behind deliberately rather than deleted without asking. Anything still
pointing at the old path will validate a room set the game does not use — which
already happened once during development and was caught.

**Also:** `Resources/Rooms/` and `FloorDirector.cs.meta` are **untracked in
git**. They exist on disk only.

---

## MEDIUM — no save system

Campaign progress — money, cable, upgrades, run number, lost crew — lives in
static fields mirrored to `NetworkVariable`s. **Nothing is written to disk.**
Quitting loses the campaign.

`PlayerPrefs` is used for exactly two things: the local-vs-Steam transport
toggle and a microphone name.

No design document specifies a save system. This is not a regression; it is a
gap nobody has scheduled.

---

## MEDIUM — voice transmits from the Windows default microphone

Documented in `VoiceStream.cs`. Steam's capture API offers no way to select a
device, so the in-game picker changes **only the test meter**, not what is
transmitted. A player with the wrong default device will appear silent and the
in-game UI will suggest they are fine.

---

## MEDIUM — `steam_appid.txt` is 480

480 is Valve's public *Spacewar* test app id. Correct for development; **must
be replaced with the real app id before any public build**, or lobbies and
identity will behave unpredictably in the wild.

---

## HIGH — G is bound to two things at once

Verified 9 Sep 2026, and **not mentioned in any design document**:

- `PlayerPush` reads `keys.gKey.wasPressedThisFrame` -> **shove**
- the `DropPack` action is bound to `<Keyboard>/g` -> `PlayerBackpack.DropAll()`

Both are live. Both fire on the same press. Nothing coordinates them.

Two input paths are in use across the project — the Input System for
Move/Look/Jump/Interact/PutDown/DropPack, and direct keyboard reads through
`PlayerMotor.Keys` for push, the dashboard, the headlamp and backpack slots.
The collision falls exactly on the seam between them, which is why neither
side looks wrong on its own.

**Not fixed here** — the audit does not change gameplay. Whether push moves,
the pack drop moves, or one becomes conditional is a design call.

---

## MEDIUM — dead input actions from the deleted rope

`PlayerControls.inputactions` still binds `ReelIn` (T), `ToggleTether` (F),
`Descend` (Ctrl) and `Ascend` (Shift). **No script handles these ACTIONS.**

**Correction, 9 Sep 2026:** an earlier version of this file said the keys were
unused. That is wrong for **F** — `ElevatorDashboard` reads `kb.fKey` directly
to enter and leave the panel, which is why the lift HUD says *"F at the
panel"*. Removing the dead `ToggleTether` action is safe; treating F as a free
key is not.

`Shift` and `Ctrl` genuinely are unused, and are keys a player will press
expecting sprint and crouch.

---

## LOW — `Assets/_Recovery/` holds 6 unused scene files, 3.4 MB

`0.unity` through `0 (5).unity`. They are imported by Unity (each has a
`.meta`) and are not referenced. Recovery artefacts, presumably from a crash.

---

## LOW — `MASTER.md` is stale and self-describes as the entry point

It tells a reader to start there and then gives an inventory three phases out
of date. See `DECISIONS.md`.

---

# Carried in the design docs

## FIXED 5 Sep 2026 — "the shove misses sometimes"

Recorded here because the *method* matters. The lift was eating it:
`Pushable.Allows` ends with "pushable unless it is loot", and the elevator has
a rigidbody, no `Carryable` and no `Pushable`, so it fell through to `true`.
Standing in the car, the spherecast reached a wall before the person.

Outside the lift it always worked; inside it never did — and the lift is where
two players naturally stand, which made it look intermittent.

**Twelve instrumented swings settled it in one pass.** None of the three leads
carried through all of Phase 4 — the 0.341 s contact delay, the null
`hit.rigidbody`, the observer-side loss — had anything to do with it. All three
were plausible, and reading the code had not distinguished them in two attempts.

## The lesson Phase 4 paid most for

> A symptom that is **asymmetric** — works on the host, fails on the client, or
> only slot 0 is wrong — is a **replication or identity** problem.
> A symptom that **works once and then stops** is a **cached answer going
> stale**.

---

## UNKNOWN — is carrying a downed crewmate actually working in multiplayer?

A commit on **29 Aug 2026** reads:

> *"Carrying a crewmate is cut. It did not survive the netcode."*

But the code today has the whole path: `Carryable.IsPerson`, a
`"E pick them up (70kg - both hands)"` prompt, person-specific handling in
carry/drop/throw, and `DownedPlayer.cs` intact. No later commit explicitly
restores it.

So either it was restored quietly, or the code path exists and does not work
across the network. **This audit cannot tell which without running two clients**
— and it matters, because "a downed player is a `Carryable`" is one of Phase 2's
headline results and one of the arguments the whole game is built on.

**Resolve by testing with two machines before relying on it.**
