# SAFE DEPOSIT — TEST STEP 6

Latest checklist: real Player.fbx + realistic low shaft smoke.

---

## 1. Confirm generated character is gone

Expected in project scripts:

- `PrototypeDiverVisuals.cs` should be gone.
- `ShaftSmoke.cs` should be gone.
- Real model is:
  `Assets/_Project/Models/Player.fbx`

---

## 2. Add Player.fbx to the gameplay Player

Follow:

`PLAYER_FBX_SETUP.md`

Short version:

1. Drag `Assets/_Project/Models/Player.fbx` into the scene.
2. Make it a child of the existing gameplay `Player` object.
3. Keep scripts/physics on the root Player.
4. FBX is visual only.

Start transform:

```text
Position: 0, 0, 0
Rotation: 0, 180, 0
Scale:    1, 1, 1
```

Adjust scale if needed.

---

## 3. Smoke like reference

Press Play.

Expected:

- Smoke is visible mainly in the **lower shaft**.
- It looks like soft haze/fog, not cubes.
- It is wide and subtle, like the reference image.
- It does not block the full screen.

Smoke script:

`Assets/_Project/Scripts/RealisticSmokeVolume.cs`

Spawner:

`Assets/_Project/Scripts/SceneAtmosphere.cs`

Tune on `SceneAtmosphere`:

- `Smoke Center` — lower/raise smoke layer
- `Smoke Radius` — wider/narrower shaft haze
- `Smoke Vertical Spread` — taller/shorter smoke column
- `Smoke Tint` alpha — stronger/weaker smoke

Suggested if smoke is too weak:

```text
Smoke Tint alpha: 0.16
Smoke Emitters: 9
Smoke Radius: 8
```

Suggested if smoke is too strong:

```text
Smoke Tint alpha: 0.07
Smoke Emitters: 4
```

---

## 4. Lighting with smoke

Expected:

- Headlamp beams show through haze better.
- Scene is not pure black.
- Walls/doors still readable.

If too bright:
- Lower `Ambient Intensity` to `0.65`.

If too dark:
- Raise `Ambient Intensity` to `0.95`.
- Lower `Fog Density` to `0.020`.

---

## 5. Quick regression

Also check:

- Shift/Ctrl blocked on ground.
- Shift/Ctrl blocked at 10m tether.
- T reels only in air.
- Start over keeps atmosphere + smoke.
