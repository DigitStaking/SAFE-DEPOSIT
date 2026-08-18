# SAFE DEPOSIT — TEST STEP 7

Latest checklist: Player prefab missing script fix, real Player.fbx setup, material tool.

---

## 1. Reimport Player prefab

In Unity:

1. Select `Assets/_Project/Prefabs/Player.prefab`.
2. Right click → **Reimport**.
3. Open the prefab.

Expected:

- No red missing script component.
- Saving prefab should work.
- Error should be gone:

```text
You are trying to save a Prefab with a missing script
```

---

## 2. Run automatic FBX setup

Menu:

`SAFE DEPOSIT -> Player -> Setup Player FBX Prefab`

Expected:

- `Player.fbx` is added as a child of the root prefab.
- Child is named:

```text
PlayerModel_FBX_VISUAL
```

- Root `Player` keeps gameplay scripts.
- FBX child has no Rigidbody/Collider.
- Materials are created here:

```text
Assets/_Project/Materials/PlayerFbx/
```

---

## 3. Camera note

Expected:

- Player prefab does **not** have camera.
- Scene has `Main Camera`.

That is normal for now.

Do not delete root Player because it has:

- `PlayerMotor`
- `PlayerTether`
- `PlayerCarry`
- `PlayerBackpack`
- Rigidbody / CapsuleCollider

---

## 4. Material check

The FBX material names from Blender were:

- `Body`
- `Player`
- `AntiLight`
- `Badge`
- `Rope`
- `Glass`
- `Light`

Expected after running tool:

- `Player` material = red/orange suit
- `Glass` = dark transparent visor
- `Light` = yellow emissive lamp
- `Rope` = brown rope
- `AntiLight` = dark rubber
- `Body` = grey trim
- `Badge` = dark badge

If wrong:

1. Select `PlayerModel_FBX_VISUAL`.
2. Check child Renderers.
3. Manually drag materials from:

```text
Assets/_Project/Materials/PlayerFbx/
```

---

## 5. If model blocks camera

Try on `PlayerModel_FBX_VISUAL`:

```text
Position: 0, -0.1, -0.15
Rotation: 0, 180, 0
Scale: 1, 1, 1
```

If still inside helmet/head:

- Hide helmet/head mesh for now.
- Later we add a first-person hidden layer.

---

## 6. Smoke quick check

Expected:

- Low shaft haze visible near bottom.
- Billboard/fog look, not cube smoke.
- Headlamp beams read better through haze.
