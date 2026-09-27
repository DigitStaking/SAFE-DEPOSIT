# SAFE DEPOSIT — Game Vision

Sourced from `GAME_DESIGN.md`, `MASTER.md`, `ECONOMY_AND_CAMPAIGN.md`,
`DEMO_PLAN.md` and `INHABITANTS.md` at the repository root. Nothing invented.

---

## What the game is

**A 4-player co-op first-person horror-heist.** Unity 6 / URP, flat-shaded in
the style of PEAK. A crew rides an elevator down into a bank tower converted to
a civilian shelter, loots it for the mafia, and tries to come back up.

**Status: the premise is IMPLEMENTED and playable end to end** — descend, loot,
load, RETURN, results, shop, repeat.

## The world, in three facts

1. **The war ended and the surface lost.** Nothing grows or is manufactured.
   Value is metal, medicine, art, and above all **food**.
2. **The building is being demolished floor by floor, from the roof down.**
   Officially because it is unsafe.
3. **Actually, the war was planned in that shelter and the paperwork is still
   in it.** Every floor destroyed is evidence turning to dust.

That third fact is the design's load-bearing wall. It makes the collapse **an
antagonist with a motive** rather than a timer, gives the deepest floors a
reason to be the most dangerous, and means somebody is actively racing you —
and winning.

## The crew

Four friends who offered to help the rescue effort and were refused. They
signed with the mafia because the mafia has the tools and a reason to go down.
They want three things: **resources**, **the people nobody is coming for**, and
**one friend's family on the bottom floor**.

## The argument that IS the game

The lift has one load limit, and everything competes for it.

| What | Pays | Costs |
|---|---|---|
| **Treasure** | the mafia, which keeps you alive | nothing — the safe choice |
| **Survivors** | nothing | a person weighs as much as your best loot |
| **Evidence** | nothing | backpack slots, and only on deep floors |

The mafia does not care about people or paperwork. So every run is the same
argument, held out loud with your friends listening: **this person, or the
gold?**

It is not abstract. It is a weight limit — which is why the mass system and
proximity voice are not features but the game's core, and why both were built
before any monster.

## Design principles the documents keep returning to

- **Everything that matters is behind a puzzle.** Every rare item, every
  survivor, every document. A puzzle is never a detour; it is the objective.
- **Traps are punctuation, not prose.** One per floor, two on deep floors.
- **No two inhabitants tax the same resource.** A crew that meets seven things
  which all punish carelessness has met one thing seven times.
- **Verbs before creatures.** Four of the demo's seven inhabitants have an
  answer that is "push it", so push was built first — building creatures before
  the verb means building them twice.

## Scope, decided 18 August 2026

**The demo is built first and alone: 20 floors, 5 Tier-1 puzzles, 10 rounds.**
Nothing from the full game is built until the demo ships.

**Target: Steam Next Fest, June 2027. Submission 31 May 2027.** A game appears
in only one Next Fest, so this is the only shot.

## Player count

`GAME_DESIGN.md` says 4 players, up to 6. Everything since — economy, netcode,
crew slots — is written for **4**. Treat 4 as current and 6 as PROPOSED.
