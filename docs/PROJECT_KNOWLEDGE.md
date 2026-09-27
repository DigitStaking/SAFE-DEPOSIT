# SAFE DEPOSIT — Project Knowledge

**The entry point for this `docs/` folder.** Written 9 Sep 2026 from a direct
audit of the repository: source files, packages, project settings, scenes,
prefabs and the twenty design documents at the repository root.

---

## How to read this folder

| File | Answers |
|---|---|
| `PROJECT_KNOWLEDGE.md` | this file — what was audited, what is trustworthy |
| `GAME_VISION.md` | what the game is and who it is for |
| `GAME_DESIGN.md` | a **map** to the design docs, not a second copy of them |
| `CONTROLS_AND_RUNNING.md` | what the player presses, and how to start the game |
| `ARCHITECTURE.md` | how the code is organised and why |
| `SYSTEMS.md` | every system, with a status label |
| `PROGRESS.md` | phases, what is finished, what is next |
| `DECISIONS.md` | decisions taken, and the ones that were reversed |
| `KNOWN_ISSUES.md` | open faults |
| `TODO.md` | what is queued |
| `CHANGELOG.md` | history from git |
| `TECHNICAL_DEBT.md` | what will cost later |
| `TESTING.md` | how this project is verified today |

---

## The one thing to understand about this repository

**The design documents at the repository root are the project's real memory,
and they are excellent — but they are not uniformly current.**

There are twenty of them, ~350 KB of design writing, and they record not just
decisions but the reasoning and the reversals. That is rare and valuable. It
also means a document can be authoritative for one thing and stale for another.

`MASTER.md` names itself the index and says which documents are true. **It was
last updated 14 August 2026** and its system inventory is now wrong in a way
that matters: it lists netcode as "not built at all", when Phases 2, 3 and 4 —
including the entire networking layer — have since been completed.

**`ROADMAP.md` is the current map of phases.** `MASTER.md` is still correct
about *which documents exist and which are superseded*, and its handover prompt
and four working rules still hold.

See `DECISIONS.md` Part 1 for the **four** places where the documents and the
code disagree, and which one is current.

---

## What was audited

- **95 C# files, ~33,800 lines** — every file listed and sized, key systems read
- `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`
- `Assets/_Project/Scenes/Prototype.unity` — the working scene
- All prefabs under `Assets/_Project/Prefabs` and `Assets/_Project/Resources`
- The two `Resources/*.asset` files: **one** ScriptableObject
  (`FirstPersonViewmodelSettings`) and **one generated Mesh**
  (`PlayerArmsViewmodel`, built by `FirstPersonArmsMeshBuilder`) — this project
  has almost no ScriptableObject-driven data
- All twenty root-level `.md` documents
- 319 commits of git history

**No PDFs, `.docx` or binary design documents exist in the repository.** The
design record is entirely Markdown. Nothing was skipped.

## What was NOT done

Per instruction, this audit changed no gameplay code, deleted nothing, and
refactored nothing. The only files written were this `docs/` folder, the
private `.claude/` folder, and one line added to `.gitignore`.

**Nothing here was verified by running the game.** Status labels below come
from reading code and documents. Where a document claims something is finished
and the code agrees, it is marked IMPLEMENTED; where only one says so, it is
marked and the disagreement is recorded.

---

## Status vocabulary used throughout

| Label | Means |
|---|---|
| **IMPLEMENTED** | code exists and the design docs consider it done |
| **PARTIAL** | code exists, incomplete or unverified against its own spec |
| **PLANNED** | specified in a design doc with a build order, not yet built |
| **PROPOSED** | discussed in a doc, not committed to a phase |
| **UNKNOWN** | cannot be determined without running the game |
| **DEPRECATED** | superseded; code or docs still present |

---

## Facts worth knowing before touching anything

1. **Unity 6000.3.13f1, URP 17.3.0.** Not an LTS release line you can assume
   about — check before upgrading packages.
2. **Netcode for GameObjects 2.13.1** with a **hand-written Steam transport**.
   Not Facepunch, despite what `ROADMAP.md` says. See `DECISIONS.md`.
3. **Voice is Steam Voice**, written in-house. Not Dissonance, despite what
   `ROADMAP.md` says.
4. **There is no save system.** Campaign progress lives in static fields and
   NetworkVariables and is lost when the process exits. `PlayerPrefs` holds
   exactly two things: a transport toggle and a microphone name.
5. **There are no automated tests.** `com.unity.test-framework` is installed;
   no test assembly exists. Verification is manual checklists and editor tools.
6. **Prefabs are built by editor scripts, not by hand.** Twenty-three editor
   scripts exist for this. It is a deliberate, repeatedly successful choice.
7. **One scene.** `Prototype.unity` contains the shaft, twenty levels, the
   elevator, loot and network roots. There is no menu scene.
