# SAFE DEPOSIT — Architecture

How the code is actually organised, from reading it on 9 Sep 2026.

---

## Shape of the codebase

```
Assets/_Project/
  Scripts/           55 files   ~21,500 lines   runtime gameplay
  Scripts/Net/       15 files    ~4,800 lines   networking and voice
  Scripts/Editor/    23 files    ~7,300 lines   tools, prefab builders, windows
  Scenes/            Prototype.unity — the only working scene
  Prefabs/           Player, Elevator, Loot props, Rooms
  Resources/         1 ScriptableObject, 1 generated Mesh, Rooms/ (runtime)
  Animation/         controllers and clips
  Materials/
```

There are **no assembly definition files.** Everything compiles into
`Assembly-CSharp` and `Assembly-CSharp-Editor`. That is simple and it is also
why every script can see every other script — see `TECHNICAL_DEBT.md`.

---

## The five architectural patterns that explain most of the code

### 1. Static gameplay state with a network mirror

`Campaign` is a `static class` holding money, cable, run number, upgrades and
lost crew. Every property has the same shape:

```csharp
public static int Money
{
    get => Net != null ? Net.Money.Value : localMoney;
    set { if (Net != null) Net.Money.Value = value; else localMoney = value; }
}
```

**Why it is like this:** `Campaign` must survive a scene reload between rounds,
which is what made it static originally. When netcode arrived, rather than
un-static it, a `CampaignNet` `NetworkBehaviour` holds the real
`NetworkVariable`s and `Campaign` became a façade over them. Offline it falls
back to plain fields, so **single player keeps working with no network at all.**

This pattern is used by `Campaign` and `Crew`. It is the single most important
thing to understand before touching gameplay state.

### 2. Registries instead of scene searches

`PlayerRegistry` (static) holds every `PlayerMotor` and answers "who is local",
"who is this component's owner", "where is their eye". `SceneRefs` (static)
caches the genuinely-single things: the run, the lift, the atmosphere.

Both exist because Phase 3 surveyed 18 `FindObjectsByType` calls and sorted
them: five were hunting for *a* player and were wrong, nine were looking for
singletons and were merely slow. `RunHudGate` was calling
`FindFirstObjectByType<RunManager>` from nine `OnGUI` methods twice a frame.

**Rule this encodes: never search the scene for a player. Ask the registry.**

### 3. Prefabs are built by editor scripts, not by hand

Twenty-three editor scripts. `ElevatorBuilder`, `Grayboxbuilder`,
`LootPrefabBuilder`, `RoomModuleBuilder`, `AnimatorBuilder`,
`FirstPersonArmsMeshBuilder`, `NetworkBuilder`, `PlayerFbxSetupTool`.

**Why:** exact coordinates and dozens of child objects are what hand-placing
gets 95% right before costing an hour on the rest, and a proportion change
becomes one constant and a rebuild rather than six prefabs nudged with one
missed. The project's own note is that this "has worked every time".

Consequence: **to change room or elevator geometry you edit a builder and
re-run it**, you do not drag things in the Inspector.

### 4. Host decides, everyone is told

Netcode is host-authoritative. The recurring shape:

- the host writes a `NetworkVariable` or runs a `ServerRpc`
- every machine reacts to the value **changing**, not to the event that caused it
- clients never write shared state

`Elevator.EnsureActiveSideForCurrentFloor` is the clearest example: it triggers
on the floor number changing, so a client that learns late still does the right
thing at the moment it learns.

### 5. Deterministic generation instead of replicated geometry

The Phase 5 floor generator derives everything from `(Campaign.RunNumber,
floorIndex)` using `System.Random` — never `UnityEngine.Random`, which is
global mutable state whose sequence depends on how many times anything else
rolled. Same inputs produce the same floor on every machine, so **no geometry
crosses the wire.**

---

## Runtime object graph, as found in `Prototype.unity`

```
Prototype
├── Main Camera, Directional Light
├── Player                    hand-placed; steps aside when NGO spawns bodies
├── RunManager                the round: quota, extraction, collapse, results
├── SHAFT
│   ├── Level_01 … Level_20   one per floor, each rotated so the door differs
│   └── Winch_Anchor
├── LOOT                      spawned items, roster-driven
├── NETWORK                   NetworkManager, transport, CampaignNet, LootNet…
├── ELEVATOR                  car, dashboard, bridge, deck, cable
└── CAMPAIGN
```

---

## Layered dependency, roughly

```
        Editor tools  ──────────────► build prefabs and assets
                                              │
   Net/ (NGO, transport, voice)  ◄────────────┤
                │                             │
        Campaign / Crew  (static + mirror)    │
                │                             │
   RunManager ──┼── Elevator ── LootSpawner ──┤
                │                             │
      Player systems (motor, carry, health, animation, viewmodel)
                │
        PlayerRegistry / SceneRefs
```

Nothing enforces this. There are no assemblies and no interfaces between
layers — the ordering is a convention held by discipline and comments.

---

## The dependencies that actually bite

There are no assembly definitions, so nothing is structurally isolated. These
are the couplings that have caused real work, and the ones to check before
editing.

| Change this | And you have changed |
|---|---|
| `Carryable` mass thresholds (8 kg / 60 kg) | carrying, jumping, walk speed, throwing, the load gauge — **five systems** |
| `Campaign.PlayerMass` (70 kg) | the load gauge, and whether a person is throwable |
| a `Campaign` property | its `CampaignNet` mirror, and every caller |
| `Grayboxbuilder` constants | room modules, the floor generator, loot slots |
| door size (2 x 2.5 m) | every room prefab and the generator's placement frame |
| `Campaign.RunNumber` | **every generated floor** — it is the run seed |
| component order on `Player.prefab` | **arm IK**, silently — `OnAnimatorIK` is ordered by components |
| `PlayerMotor.Keys` | push, the dashboard, the headlamp, backpack slots |

### Two facts behind that table

**The weight thresholds were set in Phase 2 for speed and jumping**, and later
turned out to land exactly right for throwing — a person at 70 kg is Massive
and therefore unthrowable, for free. If a sixth system disagrees with them, the
classes are probably right and the system is wrong.

**Input arrives two ways.** The Input System handles
Move/Look/Jump/Interact/PutDown/DropPack; push, the dashboard, the headlamp and
backpack slots read the keyboard directly through `PlayerMotor.Keys`. A key can
therefore be "unbound" in the actions asset and still very much in use — and
two systems can claim the same key without either looking wrong. See
`CONTROLS_AND_RUNNING.md`.

---

## Player composition — and a distinction that matters

The player is not one class. **`Player.prefab` carries 24 distinct project
components**, each owning one concern (verified by GUID, 9 Sep 2026):

```
PlayerMotor            PlayerCarry           PlayerCarryArms
PlayerBackpack         PlayerHealth          DownedPlayer
PlayerPush             PlayerPushArms        PlayerAnimatorDriver
ProceduralLegs         ProceduralLegsIK      HandFingerCurl
PlayerHeadlamp         PlayerFallDamage      PlayerSkin
LocalFirstPersonBodyCull                     LostSpectator
MedSpray               NetworkPlayer         CrewMemberNet
OwnerNetworkAnimator   VoiceStream           VoiceTransmit
VoiceMouth
```

### Three components are NOT on the prefab, and this is worth knowing

| Component | Where it actually is |
|---|---|
| `FirstPersonCamera` | **in the scene only** — on the hand-placed Player |
| `FirstPersonViewmodel` | **created at runtime** — `AddComponent` from its own builder |
| `ViewmodelArmsIK` | **created at runtime**, with the viewmodel |

**The hand-placed scene player and the NGO-spawned prefab player are different
objects with different components.** `NetworkBootstrap.ClearScenePlayer` exists
precisely because the scene body has to step aside when NGO starts handing out
its own.

This is exactly the shape that produces "works on the host, fails on the
client" — see the diagnostic rule in `docs/KNOWN_ISSUES.md`. If a component
behaves differently in solo play and in a session, **check which of the two
bodies you are actually looking at before anything else.**

**Two bodies exist per player:** a full third-person body other players see,
and a first-person viewmodel only the owner sees. `LocalFirstPersonBodyCull`
keeps you from seeing your own head from inside.

**Animation IK ordering is load-bearing.** `OnAnimatorIK` is delivered in
**component order**, not `DefaultExecutionOrder`, and an Override animator
layer at weight 1 replaces whatever layer 0's IK wrote. Several long-lived
bugs came from this. The rule the project settled on: *release the goals you
wrote, once, on transition — never branch on who else is driving them.*
