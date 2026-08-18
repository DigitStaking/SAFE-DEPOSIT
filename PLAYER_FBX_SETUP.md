# SAFE DEPOSIT — Player FBX Setup

Your real model is already in the project:

`Assets/_Project/Models/Player.fbx`

The old generated/procedural character script is removed.

---

## Important: your Player prefab has no camera

From the project inspection:

- `Assets/_Project/Prefabs/Player.prefab` has gameplay/body/arms/tether objects.
- `Prototype.unity` has a scene-level `Main Camera`.
- So do **not** look for the camera inside the prefab. That is normal for your current setup.

---

## Fix prefab missing script

The prefab had a stale deleted component:

`PrototypeDiverVisuals`

That was removed from the prefab file, so Unity should stop saying:

`You are trying to save a Prefab with a missing script`

If Unity still shows it:

1. Select `Assets/_Project/Prefabs/Player.prefab`.
2. Right click → **Reimport**.
3. Reopen the prefab.

---

## Automatic setup method

I added a Unity menu tool:

`SAFE DEPOSIT -> Player -> Setup Player FBX Prefab`

Run that in Unity.

It will:

- Open `Assets/_Project/Prefabs/Player.prefab`
- Add `Assets/_Project/Models/Player.fbx` as a visual child
- Name it `PlayerModel_FBX_VISUAL`
- Remove any colliders/Rigidbodies from the FBX child
- Create close materials in:
  `Assets/_Project/Materials/PlayerFbx/`
- Assign materials based on source names:
  - `Player` / `Suit` / `Torso` → red/orange suit
  - `Glass` / `Visor` → dark transparent visor
  - `Light` / `Lamp` → yellow emissive lamp
  - `Rope` → rope brown
  - `Badge` → dark badge
  - `AntiLight` / `Rubber` / `Boot` / `Glove` → black rubber
  - `Body` / `Metal` / `Trim` → grey trim

---

## Manual setup method

If you do it manually:

1. Open `Assets/_Project/Prefabs/Player.prefab`.
2. Drag `Assets/_Project/Models/Player.fbx` into the prefab hierarchy.
3. Make it a child of the root `Player`.
4. Rename it:

```text
PlayerModel_FBX_VISUAL
```

Start transform:

```text
Position:  X 0     Y 0     Z 0
Rotation:  X 0     Y 180   Z 0
Scale:     X 1     Y 1     Z 1
```

Keep these on the **root Player only**:

- `Rigidbody`
- `CapsuleCollider`
- `PlayerMotor`
- `PlayerTether`
- `PlayerCarry`
- `PlayerBackpack`

Remove/disable these if they appear on the FBX child:

- Colliders
- Rigidbodies
- Character Controllers

---

## If the FBX fills the camera

Because your camera is not in the prefab, this is probably a scene/camera placement issue.

Quick fixes:

- Move the FBX child down/back slightly.
- Hide helmet/head mesh for local first-person if needed.
- Later we can add a proper camera culling layer for first-person hidden head/helmet.

---

## Material reference

Your Blender material names from the screenshot:

- `Body`
- `Player`
- `AntiLight`
- `Badge`
- `Rope`
- `Glass`
- `Light`

The editor tool creates close Unity materials for those names.

If materials still look wrong after running the tool:

1. Select `PlayerModel_FBX_VISUAL`.
2. Expand its renderers.
3. Check material slots.
4. Manually assign materials from:

`Assets/_Project/Materials/PlayerFbx/`

---

## Test

Use:

`TEST_STEP7.md`
