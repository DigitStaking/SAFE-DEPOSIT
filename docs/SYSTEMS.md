# SAFE DEPOSIT — System Inventory

Every system found in the code, with a status label. Line counts are real.
Status vocabulary is defined in `PROJECT_KNOWLEDGE.md`.

---

## Player

| System | Files | Lines | Status |
|---|---|---|---|
| Movement, jump, crouch, speed penalties | `Playermotor.cs` | 740 | **IMPLEMENTED** |
| First-person camera | `FirstPersonCamera.cs` | 342 | **IMPLEMENTED** |
| Carry, weight classes, hold anchors | `PlayerCarry.cs`, `Carryable.cs` | 1,896 | **IMPLEMENTED** |
| Carry arm IK, per-item grip library | `PlayerCarryArms.cs`, `HandFingerCurl.cs` | 1,198 | **IMPLEMENTED** |
| Backpack — 2 slots, convenience only, mass still counts | `PlayerBackpack.cs` | 456 | **IMPLEMENTED** |
| Health, four conditions, speed penalties, no regen | `PlayerHealth.cs` | 338 | **IMPLEMENTED** |
| Downed, bleed-out, carried as cargo | `DownedPlayer.cs` | 361 | **IMPLEMENTED** |
| Med spray revive | `MedSpray.cs` | 271 | **IMPLEMENTED** |
| Fall damage | `PlayerFallDamage.cs` | 159 | **IMPLEMENTED** |
| Push (shove a crewmate) | `PlayerPush.cs`, `PlayerPushArms.cs`, `Pushable.cs`, `PushProfile.cs` | 1,514 | **IMPLEMENTED** |
| Headlamp | `PlayerHeadlamp.cs` | 540 | **IMPLEMENTED** |
| Third-person animation driver | `PlayerAnimatorDriver.cs` | 438 | **IMPLEMENTED** |
| Procedural legs + leg IK | `ProceduralLegs.cs`, `ProceduralLegsIK.cs` | 1,708 | **IMPLEMENTED** |
| First-person viewmodel (separate arms) | `FirstPersonViewmodel.cs`, `ViewmodelArmsIK.cs`, settings SO | 1,535 | **PARTIAL** — no carry grip on the viewmodel; documented as parked |
| Own-body culling | `LocalFirstPersonBodyCull.cs`, `PlayerSkin.cs` | 596 | **IMPLEMENTED** |
| Identity / who is local | `PlayerRegistry.cs` | 203 | **IMPLEMENTED** |

**Input:** Unity Input System 1.19.0, `PlayerControls.inputactions`, delivered
by `PlayerInput` in **SendMessages** mode (`OnMove`, `OnJump`, `OnInteract`,
`OnPutDown`, `OnDropPack`).

Note: five actions — `HookRope`, `ReelIn`, `ToggleTether`, `Descend`, `Ascend`
— are **DEPRECATED**, left over from the deleted rope. Only `HookRope` was
reused (renamed `PutDown` for Q).

---

## Elevator — the traversal layer

| System | Files | Lines | Status |
|---|---|---|---|
| Car, floors, doors, shutters, movement | `Elevator.cs` | 836 | **IMPLEMENTED** |
| Dashboard and buttons | `ElevatorDashboard.cs`, `ElevatorButton.cs` | 839 | **IMPLEMENTED** |
| Bridge to the floor doorway | `ElevatorBridge.cs` | 488 | **IMPLEMENTED** |
| Cargo deck and load gauge | `ElevatorDeck.cs` | 169 | **IMPLEMENTED** |
| Cable, strain and fray | `ElevatorCable.cs`, `CableWear.cs` | 333 | **IMPLEMENTED** |
| Price scanner | `PriceScanner.cs` | 160 | **IMPLEMENTED** |
| Ride diagnostics | `LiftRideAudit.cs` | 173 | **IMPLEMENTED** (dev tool) |

All twelve steps of `ELEVATOR_SPEC.md` are recorded complete (19 Aug 2026).

---

## Run and campaign

| System | Files | Lines | Status |
|---|---|---|---|
| Round loop: quota, extraction, collapse, results, room clock | `RunManager.cs` | 1,369 | **IMPLEMENTED** |
| Campaign money, cable, upgrades, lost crew, rescue contracts | `Campaign.cs` | 1,183 | **IMPLEMENTED** |
| Crew members, slots, health record | `Crew.cs` | 263 | **IMPLEMENTED** |
| Loot spawning, five tiers, value budget | `LootSpawner.cs`, `LootItem.cs` | 738 | **IMPLEMENTED** |
| Room sealing (rubble blocker) | `RoomSeal.cs` | 99 | **IMPLEMENTED** |
| Spectator when Lost | `LostSpectator.cs` | 196 | **IMPLEMENTED** |
| **Shop UI** | — | 0 | **PLANNED** — prices exist in `Campaign`; no interface |
| **Save / load to disk** | — | 0 | **NOT PLANNED ANYWHERE** — see below |

**`RunState`**: `Active`, `Extracted`, `Buried`, `Lost`.

---

## Networking — Phase 4, all 11 steps done

| System | File | Lines | Status |
|---|---|---|---|
| Connect and spawn bodies | `NetworkBootstrap.cs` | 504 | **IMPLEMENTED** |
| Networked player body | `NetworkPlayer.cs`, `OwnerNetworkAnimator.cs` | 481 | **IMPLEMENTED** |
| **Hand-written Steam transport** | `SteamTransport.cs` | 373 | **IMPLEMENTED** |
| Steam init and app-id handling | `SteamBoot.cs` | 133 | **IMPLEMENTED** |
| Lobby, host/join, Steam-vs-local toggle | `CrewLobby.cs` | 899 | **IMPLEMENTED** |
| Campaign replication | `CampaignNet.cs` | 364 | **IMPLEMENTED** |
| Crew replication | `CrewMemberNet.cs` | 240 | **IMPLEMENTED** |
| Loot replication (pickup/stow/drop/throw) | `LootNet.cs` | 480 | **IMPLEMENTED** |
| Elevator replication | `ElevatorNet.cs` | 381 | **IMPLEMENTED** |

**Transport:** `com.rlabrecque.steamworks.net` (Steamworks.NET). The transport
is written in-house — about eight methods — because Facepunch's ready-made one
did not compile. `UnityTransport` over 127.0.0.1 remains available and is the
default for solo testing.

**Solo play still works with no network active.** This is deliberate and held
through all eleven steps.

---

## Voice — proximity chat, in-house

| System | File | Lines | Status |
|---|---|---|---|
| Capture and transmit | `VoiceMic.cs`, `VoiceTransmit.cs`, `VoiceStream.cs` | 589 | **IMPLEMENTED** |
| Playback, distance, occlusion, reverb | `VoiceMouth.cs` | 217 | **IMPLEMENTED** |
| Walkie-talkie, one speaker at a time | `WalkieChannel.cs` | 183 | **IMPLEMENTED** |

**Built on Steam Voice** (`GetVoice` / `DecompressVoice`), not Dissonance —
speech at ~16 kbit/s compressed rather than ~256 kbit/s raw, and no licence
cost. Occlusion and reverb are plain Unity audio on top.

**Known limitation, documented in the code:** Steam captures from the *Windows
default* recording device and offers no API to change it. The in-game picker
selects the device for the **test meter only**, not for what is transmitted.

---

## Level generation — Phase 5, in progress

| System | File | Lines | Status |
|---|---|---|---|
| Graybox shaft and 20 levels | `Editor/Grayboxbuilder.cs` | 382 | **IMPLEMENTED** |
| Room module contract (sockets) | `RoomModule.cs`, `RoomSocket.cs` | 275 | **IMPLEMENTED** |
| Doors as connections | `RoomExit.cs` | 103 | **IMPLEMENTED** |
| Room set by door count (1/2/3/4) | `Editor/RoomModuleBuilder.cs` | 337 | **IMPLEMENTED** |
| Topology as a graph | `FloorGraph.cs` | 309 | **IMPLEMENTED** — 20/20 seeds valid |
| Placement, door-to-door, backtracking | `FloorGenerator.cs` | ~445 | **IMPLEMENTED** — 10/10 floors placed and validated |
| Validation, rejects rather than repairs | `FloorValidator.cs` | ~350 | **IMPLEMENTED** — doors, overlaps, reachability, shaft clearance, branching, lock solvability |
| Seed derivation | `FloorLayout.cs` | 50 | **IMPLEMENTED** |
| Runtime floor lifecycle | `FloorDirector.cs` | 586 | **IMPLEMENTED** — self-installing; unlocked floors stay alive, sealed ones destroyed when empty; each floor static-batched on build, movable bodies excluded |
| Locked doors and keys | `RoomDoor.cs`, `DoorKey.cs`, `FloorLocks.cs` | ~430 | **PARTIAL** — compiles, never run |
| Editor preview and 20-seed validation | `Editor/FloorPreview.cs` | ~265 | **IMPLEMENTED** — shares the runtime's doorway and keep-out constants |

**Verified by batch run on 9 Sep 2026** (`-executeMethod`, not asserted):
20/20 seeds valid, 10/10 floors placed, all four door counts exercised.

**Still the least PLAYED area of the project** — validation is not play, and
the locked-door system has never executed at all. See `KNOWN_ISSUES.md`.

---

## Atmosphere and presentation

| System | Files | Lines | Status |
|---|---|---|---|
| Fog, smoke, light shafts | `SceneAtmosphere.cs`, `RealisticSmokeVolume.cs`, `LightShaft.cs`, `AtmosphereBootstrap.cs` | 650 | **IMPLEMENTED** |
| HUD gating | `RunHudGate.cs` | 18 | **IMPLEMENTED** |

**All UI is `OnGUI` / IMGUI.** There is no uGUI canvas, no UI Toolkit
document, and no HUD prefab. This is a deliberate prototype choice and a
documented debt.

---

## Audio

**Status: PLANNED.** No `AudioSource` management system, no mixer, no sound
library. Voice is the only audio pipeline that exists. `ROADMAP.md` places the
full audio pass in **Phase 8**.

---

## Editor tooling — unusually strong

23 scripts, ~7,300 lines. Prefab builders (`ElevatorBuilder`,
`Grayboxbuilder`, `LootPrefabBuilder`, `RoomModuleBuilder`,
`FirstPersonArmsMeshBuilder`, `AnimatorBuilder`, `NetworkBuilder`), tuning
windows (`GripLibraryWindow`, `PushLibraryWindow`), repair tools
(`FirstPersonFixer`, `PlayerPrefabRepair`, `PlayerFbxSetupTool`), diagnostics
(`AnimationDiagnose`, `RoomModuleValidator`, `FloorPreview`), and a Steam build
post-step.

Menus live under **`SAFE DEPOSIT →`** and **`Tools → Rooms →`**.
