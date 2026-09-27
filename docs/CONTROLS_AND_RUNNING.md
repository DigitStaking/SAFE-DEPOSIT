# SAFE DEPOSIT — Controls, and how to run it

Written 9 Sep 2026. **Every key below was read out of the source**, not from a
design document. Added because a cold reader could not previously answer "what
does the player actually press" from anywhere in `docs/`.

---

## How to run the game

1. Open **`Assets/_Project/Scenes/Prototype.unity`** — the only working scene.
2. If the shaft is missing: **`SAFE DEPOSIT → Build Graybox Shaft`**.
3. Press **Play**. Solo works with no networking active, and always has —
   every phase kept that invariant.

For two players: use the lobby (`CrewLobby`). It has a **local / Steam
toggle** stored in `PlayerPrefs`, because one machine runs one Steam account
and two windows on one PC cannot both use Steam. `UnityTransport` over
127.0.0.1 is the solo-iteration path; Steam is the shipping path.

---

## Controls, as actually implemented

Input arrives two ways, and **that split matters** — see the collision below.

### Via the Input System (`PlayerControls.inputactions`, SendMessages)

| Key | Action | Handler |
|---|---|---|
| **W A S D** / arrows | Move | `PlayerMotor.OnMove` |
| mouse | Look | `PlayerMotor.OnLook` |
| **Space** | Jump — blocked while carrying anything not Small | `PlayerMotor.OnJump` |
| **E** | Interact / pick up. **Takes only** — it no longer drops | `PlayerCarry.OnInteract` |
| **Q** | **Tap** = put down. **Hold** = wind up, release to throw | `PlayerCarry.OnPutDown` |
| **G** | Drop the whole backpack | `PlayerBackpack.OnDropPack` |

**Q depends on an interaction and will die silently without it.** The action
carries `Press(behavior=2)` (PressAndRelease). `PlayerInput`'s SendMessages
path only delivers `canceled` for **Value** actions, and `PutDown` is a
**Button** — so without that interaction the release half never arrives and
hold-to-throw does nothing. If hold-Q ever stops working, check the action's
`interactions` field before reading any code.

### Read directly from the keyboard (not actions)

| Key | Does | Where |
|---|---|---|
| **G** | **Push / shove** | `PlayerPush` via `PlayerMotor.Keys` |
| **F** | Enter / leave the elevator dashboard | `ElevatorDashboard` |
| **Esc** | Leave the dashboard | `ElevatorDashboard` |
| **L** | Headlamp toggle | `PlayerHeadlamp.toggleKey` |
| **1 2 3 4** / numpad | Use backpack slot | `PlayerBackpack` |

`PlayerMotor.Keys` exists so that seven scripts which used to read
`Keyboard.current` directly go through one place — Phase 3 Step 6 fixed the
raw reads, because a global keyboard is wrong the moment there are two bodies.

### Dead bindings — the action exists and nothing handles it

`ReelIn` (**T**), `ToggleTether` (**F**), `Descend` (**Ctrl**), `Ascend`
(**Shift**). Leftovers from the deleted rope.

**But note: F is not a free key.** The *action* is unhandled; the *key* is used
by the elevator dashboard, which reads it directly. Removing the dead action is
safe; assuming F is unused is not.

### Debug keys — off by default

`PlayerHealth` has `debugKeys = false` and an **H** damage key behind it.
Do not enable these in anything a player will see.

---

## The G collision — verified, and probably a bug

**G is bound twice:**

- `PlayerPush` reads `keys.gKey.wasPressedThisFrame` → shove
- `DropPack` is bound to `<Keyboard>/g` → `PlayerBackpack.DropAll()`

Both are live and both fire on the same press. Nothing in the code coordinates
them, and no design document mentions the overlap.

**This is reported here as a finding, not fixed** — this audit does not change
gameplay. Whether the intended behaviour is "push, and drop the pack only when
not pushing", or one of the two should move to another key, is a design call.

See `KNOWN_ISSUES.md`.

---

## The round loop

`RunManager` owns it. `RunState` is `Active → Extracted | Buried | Lost`.

```
Board the lift → F at the panel → choose a floor → descend
        ↓
Step out over the bridge → loot the floor
        ↓
Carry loot back to the lift deck — the load gauge counts what is
physically in the car, including players at 70 kg each
        ↓
RETURN → RunManager.Extract()
        ↓
Results → the mafia takes its quota → shop → next round
```

### What pressures you

- **The room clock.** `roomChargeTime = 600f` — ten minutes per charge. The
  **rooms** die, not you: there is no hard cap on the run, but rooms seal on
  each charge and one more seals when you surface
  (`Campaign.RoomsLostOnSurface = 1`).
- **The load limit.** `BaseCapacity = 550f`, `+50` per upgrade, max 9 upgrades.
  Players count toward it.
- **The quota.** `200 × 1.072^(run-1)`, and settlement counts your **bankroll
  plus this haul** — so savings absorb a bad round. You lose when they cannot.
- **Cable length.** `StartingCable = 15f`, `+5` per chunk, max 2 chunks per
  round. This is what gates depth.

### Failure

- Three crew lost or abandoned ends the campaign (`MaxLostBeforeOver = 3`).
- Losing somebody loses what they were carrying.
- A rescue contract runs **2 rounds** (`RescueRounds`).

Full numbers and formulas: `ECONOMY_AND_CAMPAIGN.md` and `Campaign.cs`.
**Where they disagree, the code is current** — see `DECISIONS.md` Part 1.
