# SAFE DEPOSIT — Decisions

Decisions the project has taken, the ones it reversed, and the places where
documents and code now disagree. Sourced from the design docs and the code.

---

# PART 1 — CONFLICTS BETWEEN DOCUMENTS AND CODE

These are the four places where following a document would lead you wrong.
**In all four the code is current.**

## 1. The Steam transport is NOT Facepunch

| Source | Says |
|---|---|
| `ROADMAP.md` line 157 | "Unity Netcode for GameObjects + **Facepunch** Steam transport" |
| `PHASE4_SPEC.md` line 484 | proposes Facepunch |
| `PHASE4_SPEC.md` line 646 | *"~~Facepunch transport~~ — **REMOVED 21 Aug 2026. It does not compile.**"* |
| `Packages/manifest.json` | `com.rlabrecque.steamworks.net` |
| `SteamTransport.cs` | `using Steamworks;` — hand-written transport |

**Current: Steamworks.NET plus an in-house transport.** Facepunch's transport
failed to compile (`error CS1028: Unexpected preprocessor directive`) and the
project wrote its own — about eight methods, on the grounds that owning one
class is cheaper than depending on somebody else's broken copy.

`PHASE4_SPEC.md` records the reversal further down and is self-consistent if
read in full. **`ROADMAP.md` was never updated** and is the misleading one.

## 2. Voice is NOT Dissonance

| Source | Says |
|---|---|
| `ROADMAP.md` line 177 | "**Voice: Dissonance** — one-time Asset Store purchase" |
| `PHASE4_SPEC.md` lines 391, 547 | proposes Dissonance |
| `Packages/manifest.json` | no Dissonance |
| `VoiceStream.cs` | Steam `GetVoice` / `DecompressVoice` |

**Current: Steam Voice, in-house.** The stated reason is in the code: speech at
16 kHz is ~256 kbit/s raw and Unity has no voice codec, while Steam's returns
the same speech at roughly 16 kbit/s. *"Sixteen times less traffic for free is
the entire reason Steam Voice was chosen over paying for Dissonance."*

## 3. `MASTER.md`'s system inventory is stale

`MASTER.md` §4 lists as "not built at all": the elevator, health/downed/Lost,
rescue, and **netcode**. All are now built. The file is dated **14 Aug 2026**;
Phases 1–4 completed after it.

**Still trustworthy in `MASTER.md`:** which documents are authoritative vs
superseded (§1), what the elevator change did not touch (§2), the handover
prompt and the four working rules (§6).

**Also worth flagging:** `MASTER.md` §3 describes a floor as a complex of 2–3
spaces. The Phase 5 generator produces 7–14 rooms. That is deliberate evolution
recorded in `PHASE5_SPEC.md`, but §3 has not been updated.

## 4. The walkie-talkie costs 20 per person, not 30 a pair

| Source | Says |
|---|---|
| `ECONOMY_AND_CAMPAIGN.md` line 108 | *"the 30 walkie-talkie"* — a fixed pair at 30 |
| `Campaign.cs` | `WalkieBaseCost = 20`, **per person** |

**Current: 20 each.** Unlike the three above, **the code documents its own
change** with a date and a reason:

> *"ECONOMY priced a fixed pair at 30. Changed 30 Aug 2026 on request, and it
> is the better shape: the crew decides how many radios it wants and WHO gets
> them, rather than the item deciding for them. Two is still the obvious buy at
> 40, but a crew that can afford four can arm everybody - and that changes
> nothing about the tension, because the channel still only holds one voice."*

`ECONOMY_AND_CAMPAIGN.md` was not updated, so its worked example on line 108
("enough for the 30 walkie-talkie and 10 saved") no longer balances.

**Also:** the **radio relay** appears in design discussion but has **no
constant and no item in `Campaign.cs`.** It is PLANNED, not built — do not
quote a price for it.

---

# PART 2 — DECISIONS THAT STAND

## Stack and platform

- **Unity 6000.3.13f1 / URP 17.3.0.**
- **Netcode for GameObjects 2.13.1, host-authoritative.** Chosen over Photon
  Fusion because Fusion bills monthly forever while Steam Datagram Relay is
  free to Steam developers with no CCU limit. Comparable: Lethal Company runs
  this stack — one developer, four-player co-op, first person, scavenge against
  a quota, proximity voice, Steam.
- **Trade knowingly accepted:** rider sync on a moving platform is ours to
  solve rather than the SDK's.
- **Steam now; Epic is a planned fork, not a surprise.** If Epic becomes worth
  it, the answer is Epic Online Services — free, no CCU limit, crossplay. The
  transport being one swappable component is the whole reason that stays cheap.

## Architecture

- **`Campaign` stays static, with a network mirror.** It must survive scene
  reloads; surviving a reload and surviving a network boundary are different
  problems, and only one was already solved.
- **Never search the scene for a player.** `PlayerRegistry` answers.
- **Prefabs are built by editor scripts.** Repeatedly successful; also the only
  option, since the AI assistant cannot drag objects in the Inspector.
- **Deterministic generation over replicated geometry.** `System.Random` seeded
  from `(RunNumber, floor)`. **Never `UnityEngine.Random`** — it is global
  mutable state, so determinism through it depends on call order, which nobody
  can maintain and which fails silently and rarely.
- **~~`Campaign.RunNumber` is the run seed.~~ REVERSED 9 Sep 2026.**
  The original reasoning was that a separate seed would be a second source of
  truth for "which run is this". That was right about the question and wrong
  about which question mattered.

  **`Campaign.Reset()` sets `RunNumber = 1`.** So every new game started from
  seed 1 and generated an identical building. Reported directly: *"each time i
  start a new game the floors need to be different"*.

  `RunNumber` answers "which round of this campaign". The generator needs
  "which **game**" — and `RunNumber` provably cannot answer it, because it
  resets. So **`Campaign.LayoutSeed`** now exists: rolled once in `Reset` from
  the clock, host-owned, replicated exactly like the money, constant for the
  whole campaign, and mixed into `FloorLayout.SeedFor`.

  Not a second source of truth — a first source for a different fact.

  **The lesson:** "do not add a second variable" is a good instinct that
  becomes a bad one the moment the existing variable answers a *different
  question*. Check what the number means, not just whether one is available.

## Design

- **The demo is built first and alone** (18 Aug 2026): 20 floors, 5 Tier-1
  puzzles, 10 rounds. Nothing from the full game until the demo ships.
- **Verbs before creatures.** Push and Q-throw precede every inhabitant.
- **Everything that matters is behind a puzzle.**
- **The quota is non-linear.** Recorded as the single most important line
  changed in Phase 1.
- **Weight classes derive from rigidbody mass, never set by hand**, so
  behaviour cannot disagree with the stated weight.
- **Doors are connections, not decoration** (5 Sep 2026). A room with three
  doors must get three connections; a generator may never seal, block or close
  a door to make a layout work — it backtracks or regenerates.

## Working method — the four rules

1. **One step per session.**
2. **Explanation before code.**
3. **Commit after every step.**
4. **Read before write.**

And the two things the AI assistant cannot do: **drag things in the Unity
editor**, and **see the game** — so screenshots do heavy lifting.

---

# PART 3 — REVERSALS WORTH REMEMBERING

| Was | Now | Why |
|---|---|---|
| Rope and tether traversal | Elevator | the platform became the lift; ~2,470 lines deleted |
| Facepunch transport | in-house Steam transport | it did not compile |
| Dissonance voice | Steam Voice | 16× less bandwidth, no licence |
| Cargo bands / Traverse | cargo on the deck | followed the rope's deletion |
| Floor = 2–3 spaces | floor = 7–14 rooms, branching | Phase 5 |
| Generator seals unconnectable doors | generator backtracks | a sealed door is a hidden failure |

---

# PART 4 — STILL OPEN (from `MASTER.md` §7)

1. Does the mafia quota survive alongside the 30% cut? Two money pressures may
   be one too many.
2. Sub-spaces per room complex — 2 or 3? *(Arguably answered by Phase 5's
   7–14 rooms, but not formally closed.)*
3. The elevator at 250 in round 5 costs a whole round's surplus — allow saving
   across two rounds, or drop to 200?
4. Room dimensions were informed by the rope's 10 m reach, which is gone.
   Re-check against the bridge instead.
