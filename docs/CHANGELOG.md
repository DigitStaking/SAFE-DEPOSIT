# SAFE DEPOSIT — Changelog

Reconstructed from git history and the phase documents on 9 Sep 2026.
**This project did not keep a changelog before now**; this is a summary of what
the 319 commits actually did, not a running log.

Going forward, add an entry per phase step.

---

## Repository facts

- **First commit:** 4 Aug 2026, "Initial commit"
- **319 commits** in ~5 weeks — 181 in August, 138 in September
- Single branch `main`, no tags, no releases
- Commit messages are unusually good: they say **what changed and why**, often
  including the reasoning that was wrong first

---

## Phase 5 — the room kit · IN PROGRESS (Sep 2026)

**10 Sep — a floor unlocked on the results screen was empty forever.** *(bug, mine)*
**Reported:** "in round 2 i didnt find any loot in floor 4 after i open it".
**Found in Editor.log, not by reasoning.** The order was decisive:

```
[Loot] floor 1 stripped bare and banked - taped shut.   <- extraction
[Loot] floor 4 built - placed 3 items ... $181 owed     <- AFTER banking
[Loot] floor 5 built - placed 3 items ... $180 owed
[Loot] round 2: restored 5 items                        <- round 2 starts
```

**Cause:** `Campaign.LootRoster` is captured the moment a run banks. Buying
cable on the RESULTS SCREEN raises `DeepestReachableFloor`, so `FloorDirector`
generates the newly reachable floors right there - and `StockFloorIfPending`
stocked them into a scene that was about to be destroyed. The items were never
written to the roster, so they died with the reload, while their budget had
already been spent and removed from `PendingLootBudget`. The floor then owed
nothing and was never stocked again: empty for the rest of the campaign.
A second run in the same log shows the identical pattern.
**Fixed:** `StockFloorIfPending` returns early unless `RunManager.State` is
`Active`, leaving the floor owed. It is generated again next round and stocked
then, into a scene that will live long enough to keep it.
**Also:** the round-1 log line reported items PLACED, which is now almost always
0 by design - "0 items across 20 floors" is true and reads like a failure. It
now reports what was allotted and says the rest fill as floors are built.
**Known consequence for saves in progress:** a campaign that already hit this
has floors whose budget was spent and lost. They stay empty. Only
`Campaign.Reset()` (start over) restores them.
**Verified:** compiles, 0 errors. Cause confirmed from the actual log rather
than asserted. **The fix is NOT yet verified in Play mode.**

**10 Sep — both doorway barriers were floating three metres inside the shaft.**
**Symptom:** screenshots showed the caution tape and the rubble plug hanging in
open air in front of their doorways, with the room visible behind them.
**Cause:** `RoomSeal` hardcoded the doorway at local x 4.0. Grayboxbuilder has
since been widened - `ShaftInner` 14 gives a half-width of 7, and `WallThick`
0.5 puts the east wall at x 7.0-7.5, so the doorway plane is 7.25 (`wallMid`,
Grayboxbuilder line 127). The 4.0 was correct for the narrower shaft it was
written against and nothing failed when the shaft grew - it just stopped being
in the doorway. `RoomTape` copied the number when it was written, inheriting
the fault on its first day.
**Fixed:** both now derive the plane from
`FloorDirector.ShaftDoorwayLocal.x - WallThick * 0.5`, so widening the shaft a
second time cannot repeat this. Tape strips were also resized from 2.24-2.40 to
the actual 2m opening plus a little overhang, anchors moved to the real frame
edges, and the blocker now fills the opening rather than sitting proud of it.

**Same fix, worse bug — `RoomSeal.IsPlayerInside` was miscalibrated too.**
It tested x 3.6 to 11.5, z +/-4.2, y up to 4.2. The room is really x 7.5 to
13.5, z +/-7, y 0 to 5. Wrong at BOTH ends, and silent: three metres of open
shaft counted as "inside the room", so a player standing safely in the lift
could be taken by a seal, while a player in the back two metres or the outer
thirds of a real room could be missed entirely. Now one `InRoomVolume` helper,
derived from Grayboxbuilder and shared with `DestroyLootInRoom`, which had the
same numbers copied.
**This changes who dies in a seal.** Flagged rather than buried.

**Also:** `RunManager.SealRoomIndex` now calls `RoomTape.Untape` before laying
rubble. A floor that was finished and then demolished shows the demolition -
the more useful thing to know - and two barriers in one doorway would sit
inside each other. `RebuildTapeFromCampaign` already skipped sealed floors, so
this closes the other order: cleared first, sealed after.
**Verified:** compiles, 0 errors. **NOT verified in Play mode.**

**10 Sep — loot spawned on rooms that were about to be deleted.** *(bug, mine)*
**Symptom:** the placement audit reported 1 of 9 items moved - one crate fell
5.55m, dead vertical, zero lateral. Vertical with no lateral component is the
signature of spawning into thin air, not of rolling off an edge.
**Cause:** `FloorGenerator.cs:447` discards the rooms of a failed placement
attempt with `Object.Destroy`, which deletes nothing immediately - it marks the
object and Unity deletes it at the END OF THE FRAME. The generator backtracks
up to 24 times, so for the rest of that frame the floor is full of ghosts:
rejected rooms, still in the hierarchy, still carrying loot sockets, still
returned by `GetComponentsInChildren`. The loot-deferral change earlier the
same day started stocking one line after generation - in that same frame - so
`SocketsUnder` could hand back a socket belonging to a room with minutes to
live. Before that change loot spawned in `Start()` and never met them.
**Arithmetic that confirmed it:** the crate rested at y -10.50. Level origins
are floor-top, spaced 5m; the ceiling box spans +4.0 to +4.5 above its origin,
so floor 3's ceiling has its top face at exactly -10.5. It fell past floors 1
and 2 and landed there.
**Fixed:** `FloorDirector.StockWhenTheGhostsAreGone` yields one frame before
stocking, letting the deferred destroys flush, and re-checks the floor still
exists on resume.
**Also moved into that frame:** `StaticBatchingUtility.Combine`. It had been
running while the ghosts were present, baking rejected-room geometry into the
combined mesh - never drawn, but carried for the life of the floor. It now
bakes only rooms that survived.
**Also added:** `Physics.SyncTransforms()` before stocking (rooms are
instantiated then MOVED, and Unity does not push transform changes into the
physics scene until the next physics step), and a downward probe in `SpawnItem`
that names the room and socket when a loot socket has nothing under it. The
probe is kept permanently - room modules are authored by hand, and a socket
typed one digit wrong puts loot inside geometry with no other symptom.
**Wrong theory discarded first:** that `slotJitter` (0.9m) was pushing crates
off the slabs. Measured instead of assumed - the tightest socket margin in the
whole room set is 1.35m (`Room_End_Store`, depth 9, socket at d*0.85). Jitter
cannot reach an edge.
**Verified:** compiles, 0 errors. **NOT verified in Play mode.**

**10 Sep — round two built no floors.** *(bug)*
**Changed:** `FloorDirector` now hooks `SceneManager.sceneLoaded` and calls its
own `Clear()`, which already existed and which **nothing had ever called**.
**Why:** the director survives the between-round reload by design
(`DontDestroyOnLoad`) but the floors it built do not. `built` filled with
husks, and - the part that actually broke it - `lastReachable` still held round
one's value, so `Update` returned early and never reconciled. Round two began
with the director convinced it had already built everything.
The symptom was exact: the floor you rode to appeared, because `Elevator` calls
`ShowFloor` directly and that tests for a real object rather than a remembered
one; every floor unlocked but not yet visited stayed empty. Reported as "floor
3 and 4 and 5 are not open there is no map there".
Textbook case of the rule in the netcode skill: **works once and then stops is
a cached answer going stale.**
**Verified:** compiles, 0 errors. **NOT verified in Play mode.**

**10 Sep — a floor you strip bare is taped shut.**
**Changed:** new `Campaign.ClearedRooms`, replicated as a `uint` bitmask
through `CampaignNet.Cleared`, mirroring `DestroyedRooms`/`Sealed` exactly.
`LootSpawner.MarkClearedFloors` runs at the end of `CaptureRemaining` - the one
moment the roster is exactly what the building still holds.
`FloorDirector.Access` gains `Cleared`, and `ElevatorDashboard` refuses the
floor with `"NN DONE"`.
**Why:** requested - empty a floor completely and bank it and it is finished;
leave one item and it stays open. It is also an optimisation, and the reason it
is one grows with the art: a cleared floor is never generated again, and the
entire cost of a floor is the rooms in it.
**The trap:** "has no loot left" is not sufficient. A floor still owed its loot
via `PendingLootBudget` has none either, and so does a sealed one. Taping
either would lock the crew out of loot the building still owes them -
permanently, invisibly, with no way back. Both are excluded; a floor must have
been STOCKED and then EMPTIED. The loot-deferral change earlier the same day is
what makes that distinction expressible at all.
**Client safety:** a client may guess wrong locally, but `PublishClearedRooms`
is host-only and `ApplyClearedMask` overwrites the set wholesale - the same
handover `LootNet` uses for the roster.
**Verified:** compiles, 0 errors.

**10 Sep, same day, revised:** the dashboard refusal was WRONG and is removed.
The lift now travels to a cleared floor and the DOORWAY is taped instead - new
`RoomTape`, built to mirror `RoomSeal`'s doorway exactly (level-local
(4.0, 1.25, 0), 2m x 2.5m opening, Environment layer). Seven tilted yellow
strips, two grey anchor runs down the frame, and one invisible blocker that
does the actual stopping - the strips carry no colliders, so nobody gets wedged
between two pieces of tape.
`RunManager.RebuildTapeFromCampaign` mirrors `RebuildRubbleFromCampaign`
exactly, including being called again from `CampaignNet.OnClearedChanged` so a
client that learns late still sees it. A floor demolished after being cleared
gets rubble, not tape - two barriers in one doorway would fight.
**Why the change:** asked for directly - "even if you click floor 1 in elevator
it can go but you can't go inside". It is also simply better: refusing at the
panel hides the crew's own progress behind a text string, while a taped doorway
seen from the lift shows it. Costs nothing extra, because the floor behind it
is never generated either way.

**10 Sep — loot waits for the floor it belongs to.**
**Changed:** `LootSpawner` now allots each floor its budget at `Start` but
places nothing until that floor has real loot sockets;
`FloorDirector.ShowFloor` calls `StockFloorIfPending` the moment a floor is
generated. Owed-but-unplaced budget lives in `Campaign.PendingLootBudget` so it
survives the between-round scene reload. `SceneRefs.Loot` added.
**Why:** `Start()` runs before the first `Update()` and floors generate in
`Update`, so loot was placed on bare platforms using the hardcoded three-slot
grid and the rooms were then built on top of it. Crates inside walls, in the
one system the whole game loop is about. Logged as MEDIUM in KNOWN_ISSUES,
which had already identified deferral as the right architecture over
re-placing.
**Checked for regressions:** item count per floor is unchanged — `itemsPerFloor`
is 3 in code and 3 in `Prototype.unity`, and the retired `Slots` grid was also
3, so `Mathf.Min` gives 3 either way. `RestoreRoster` and `ClearAndRebuild`
rebuild from stored positions and are untouched. `LootNet`'s host-published
roster still overrides every client, so nothing here depends on machines
agreeing.
**Verified:** compiles, 0 errors (`dotnet build Assembly-CSharp.csproj`);
all seven room prefabs confirmed to carry 1–2 Loot sockets each, so the new
socket requirement can actually be met.
**NOT yet verified:** that loot lands in rooms in Play mode.

**9 Sep — floors are static-batched when generated.**
**Changed:** `FloorDirector` calls `StaticBatchingUtility.Combine` on each
floor once it is built, via `BatchFloorGeometry`, which first detaches anything
carrying a `Rigidbody` and re-parents it afterwards.
**Why:** measured, not guessed — each room prefab is 13–26 objects with as many
colliders and renderers, so a 14-room floor is ~250 objects / ~170 colliders /
~170 renderers. Three unlocked floors is ~500 renderers and a fully opened run
is ~3,400. The renderers were the cost: the objects and colliders are static
and never move, which PhysX and the transform hierarchy both handle for almost
nothing, while every renderer is a thing to cull and draw.

Batching pays here specifically because `RoomModuleBuilder` assigns one shared
material asset (`M_Graybox.mat`) to every piece — had each cube owned its own
material instance, combining the meshes would have saved culling work and no
draw calls at all. Confirmed `m_StaticBatching: 1` for Standalone in
`ProjectSettings.asset`; without it the call would have been decoration.

**The trap this avoided:** static batching bakes vertices in WORLD SPACE, so a
combined object that later moves leaves its picture behind. `FloorLocks`
parents the floor's key inside a room and gives it a `Rigidbody`, so combining
it would have produced an invisible key that still had collision — no error and
no clue. Hence the detach/re-attach. The locked door needs no such care: it
opens by disabling its renderer and collider, and disabling a batched renderer
works fine. Only MOVING is forbidden.

`GameObject.isStatic` was deliberately NOT set. It is an editor-time flag for
the build-time bake and does nothing for geometry created at runtime;
`StaticBatchingUtility.Combine` is the runtime equivalent and is sufficient on
its own.
**Verified:** compiles, 0 errors (`dotnet build Assembly-CSharp.csproj`).
**NOT yet verified:** the actual draw-call saving. That needs Play mode with
the Stats window — see below.

**9 Sep — floor access gate.**
**Changed:** `FloorDirector.ShowFloor` now refuses to generate a floor that is
sealed or beyond the cable, via a new `FloorDirector.AccessTo(floor)`.
**Why:** the requirement was "never generate a locked floor". That was already
true in practice — `ElevatorDashboard` refuses both cases before `GoToFloor` is
ever called — but `ShowFloor` is public and trusted the dashboard completely.
`Elevator.cs` records that the debug floor keys once bypassed dashboard checks
entirely, which is the exact shape of the failure this prevents.
**Reused, not rebuilt:** `Campaign.DeepestReachableFloor` and
`Campaign.DestroyedRooms` — the same two facts the dashboard reads. No second
progression system, no unlock flag.
**Verified:** braces balanced; **not compiled, not run.** `FloorDirector` is
still on no scene object, so none of this executes yet.
**Risk:** none at runtime today, because the component is inert.

**9 Sep — the floor system made self-installing, and validated harder.**
**Changed:** `FloorDirector` installs itself via
`RuntimeInitializeOnLoadMethod`; `FloorValidator` gained two geometric tests —
a **clear corridor out of the lift** and **branching measured on real
connections** rather than on prefab door counts.
**Why:** the director needed a scene object nobody would remember to add, and
failed silently without one. And "3+ doors" only equals "3+ neighbours" while
placement never fails — which is exactly the case a validator exists to catch.
**Verified:** **compiled — `dotnet build` on both `Assembly-CSharp` and
`Assembly-CSharp-Editor`, 0 errors.** Preview and runtime confirmed to load the
same 7 prefabs from the same `Resources/Rooms` folder (1-door ×2, 2-door ×2,
3-door ×2, 4-door ×1). **Not yet executed** — Unity holds the project lock, so
the validators have not been run.
**Risk:** `DontDestroyOnLoad` means the director survives scene reloads; if a
future scene wants a different configuration it must hand-place one, which
`Ensure` respects.

**9 Sep — the generator actually ran, and two real faults fell out.**
**Changed:** preview and runtime now share `FloorDirector.ShaftDoorwayLocal` /
`ShaftKeepOutFor`; `FloorGenerator` retries with a shrinking room ceiling.
**Why:** the preview passed `Vector3.zero` as the lift doorway and the runtime
passed `(7.5,0,0)`, so the preview planted every start room inside the shaft
and rejected all ten floors — a correct rejection of a building the game would
never build. And the graph always asked for 14 rooms, which does not always
pack; two floors in ten fell back to the fixed graybox room.
**Verified:** **run in Unity batch mode.** Build Room Set 0 errors; Validate
Twenty Seeds **20/20 valid**, all four door types placed; Preview Ten Floors
**10/10 valid** (11-14 rooms, 3-5 junctions, 5-9 dead ends). Both assemblies
compile with 0 errors.
**Risk:** floors can now be smaller than 14 rooms when packing is tight. That
is deliberate and preferable to falling back to the fixed room.

**9 Sep — unlocked floors stay alive; only sealed ones are destroyed.**
**Changed:** `FloorDirector` now holds a dictionary of floors rather than one.
A cheap per-frame check on `DeepestReachableFloor` and the sealed mask drives a
coroutine that builds newly-unlocked floors **one per frame** and destroys
sealed ones **only when no player is standing on them**.
**Why:** the previous version destroyed a floor when the lift left it, which
breaks the moment two players are on different floors. Also fixed
`SetFixedRoom`, which disabled *every* child of a level — including the shaft
walls and the doorway frame, so arriving at a floor would have opened the lift
onto a hole. It now disables only `Room_*`.
**Reused, not rebuilt:** `Campaign.DeepestReachableFloor` (cable) and
`Campaign.DestroyedRooms` (seals) — both already host-authoritative and
replicated, so every machine reconciles its own geometry with no RPC and
deterministic generation keeps the layouts identical.
**Verified:** compiles, 0 errors. Generator validation still stands from the
batch run (20/20 seeds, 10/10 floors). **The multi-floor runtime behaviour has
not been played.**
**Risk:** a fresh run now builds three floors at once instead of one — staggered
a frame apart to avoid a hitch, but it is more geometry than before.

**9 Sep — a new game is now a new building.**
**Changed:** added `Campaign.LayoutSeed` (`CampaignNet.Layout`), rolled once in
`Campaign.Reset()` and mixed into `FloorLayout.SeedFor`.
**Why:** `Reset()` sets `RunNumber = 1`, so two separate games both generated
the identical building from seed 1. The seven room types were being mixed the
same way every time. Reported as *"each time i start a new game the floors need
to be different"*.
**Reverses** the 5 Sep decision that `RunNumber` was the seed — see
`DECISIONS.md`. It answers "which round", not "which game".
**Verified:** compiles, 0 errors. Arithmetic checked outside Unity: two seeds
give different room picks, the same seed gives identical ones. **Not played.**
**Risk:** the seed is rolled from the clock on the host only, so a client that
somehow generated before receiving it would build a different building. Every
floor is built from replicated state after `Reset`, so this should not arise —
but it is the shape to suspect if two players ever see different rooms.

**9 Sep — Step 7: doors, keys and locked states.**
**Changed:** `RoomDoor` (Locked / Unlocked / Jammed), `DoorKey` (a `Carryable`
with an id), `FloorLocks` (picks a connection to lock and places the key),
`PlayerCarry` gained one exception so **E opens a door when you hold its key**,
and `FloorValidator` gained an independent solvability check.
**Why:** a locked door is the first thing that makes a floor a place rather
than a corridor. And because a key is a `Carryable`, carrying it costs a hand,
it can be put down and forgotten, and it can be **thrown to a crewmate** —
none of which anybody implemented; it is what happens when a key is a thing
rather than a flag.
**The invariant:** the floor is a tree, so locking a connection puts the whole
subtree beyond it out of reach. The key must live on the entrance side or the
round is unwinnable — and nothing about such a floor *looks* wrong. `FloorLocks`
computes it from the graph; `FloorValidator` re-checks it by walking the real
door links and refusing to cross a locked one, because a rule enforced only by
the code that implements it has one witness.
**Verified:** compiles, 0 errors, both assemblies. **Ran in play mode 9 Sep** —
a locked door and its key both appear on a generated floor and were found by
the developer. **Opening it with the key is still untested**, which is the
step's actual done-when. Temporary `[LOCK]` instrumentation added and removed.
**Risk:** `FloorLocks` returns false and installs nothing on small floors. A
floor with no locked door is a lesser floor, not a broken one.

**5 Sep** — Steps 1–3 done in one day, then the generator rewritten repeatedly:
- Q places an item down; **E stops doing two jobs** (drop-and-repick fixed)
- Hold Q to throw, with a wind-up gauge; weight scales wind-up and range
- The shove made reliable — **the lift was eating it**; known issue closed
- Room module contract: sockets that a module declares but never fills
- The room set, built by script
- Floor generator: spine → branching tree → **door-connectivity graph**
- Doors made mandatory connections; sealing a door outlawed
- The shaft made part of the topology; floors given a one-at-a-time lifecycle

**2–4 Sep** — first-person hands on a second camera with a cloned skeleton;
procedural legs; carry arms measured rather than animated.

## Phase 4 — netcode and proximity voice · DONE (Aug–Sep 2026)

**Closed 5 Sep** after testing with a crew. Written 23–30 Aug.

- **23 Aug** — stack chosen twice in one day: Photon Fusion 2 + Photon Voice,
  then reversed to **NGO + Steam relay** the same day on cost grounds
- **24 Aug** — two windows connected; the body on the wire
- **~26–29 Aug** — campaign, crew, loot and elevator replicated
- **29 Aug** — *"Carrying a crewmate is cut. It did not survive the netcode."*
  (see `KNOWN_ISSUES.md` — the code path is present today)
- **30 Aug** — occlusion and reverb built **before any voice existed**, then
  *"voice actually travels now"*
- **~31 Aug** — Facepunch transport removed, in-house Steam transport written
- **2 Sep** — *"Phase 4 built, and an honest line between built and proven"*

## Phase 3 — de-single-player · DONE (23 Aug 2026)

Seven steps in one day. A player knows if it is local; every player owns its
camera; per-player state moved into `Crew`; the crew became a list rather than
a player; per-player input; the two-body test. `PlayerRegistry` and `SceneRefs`
introduced here.

## Phase 2 — mass, health, downed · DONE (Aug 2026)

Health with no regeneration, downed with bleed-out, Lost state, rescue
contracts, cable fray. **A downed player became a `Carryable`** — the decision
that makes "this person, or the gold?" a mechanical question rather than a
roleplay one.

## Phase 1 — the elevator · DONE (19 Aug 2026)

Twelve steps. The rope was deleted (~2,470 lines) and replaced by the lift:
car, movement, dashboard, bridge, deck and load gauge, price scanner, return to
surface, graybox rebuild, economy retune.

Four things that were not in the plan and are worth remembering:
- Step 1 was not "commit everything" — a **942 MB unreferenced `.glb`** sat
  above GitHub's 100 MB per-file limit and had been silently blocking every
  push. The entire project existed on one disk.
- **Four real bugs fell out of deleting the rope**, none rope-related.
- **The shaft was too narrow to be frightening** — 2 m clearance against a
  3.7 m jump, measured from `PlayerMotor`'s own constants. Now 4.9 m.
- **Floor 0 was inside the ceiling slab**, which is why players and loot were
  left behind on extraction. Geometry, not code.

## Pre-phase — prototype (4–14 Aug 2026)

First-person controller, carry and weight classes, backpack, run loop, campaign
persistence, animation and hand IK, atmosphere, graybox generator, and the rope
traversal system that Phase 1 removed.

---

## Suggested format going forward

```markdown
## [Phase N Step M] — short title · YYYY-MM-DD
**Changed:** what a player would notice
**Why:** the reason, including what was tried first if it failed
**Verified:** how — compiled / validator / two machines / not verified
**Risk:** what this could have broken elsewhere
```

The "verified" line matters most. This project's own hardest-won lesson is that
**written and compiles is not the same as proven.**
