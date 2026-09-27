# SAFE DEPOSIT — Testing

How this project is actually verified today, and what to do about the gaps.

---

## The honest summary

**There are no automated tests.** `com.unity.test-framework` 1.6.0 is
installed; no test assembly and no test file exists in the repository.

Verification is four things, all manual or semi-manual:

1. **Play-mode checklists** — nine `TEST_*.md` files at the repository root
2. **Editor validators** — tools that check data and report faults
3. **In-code instrumentation** — temporary `Debug.Log`, read back from
   `Editor.log`
4. **Two-machine sessions** — the only way anything networked gets proven

---

## 1. The manual checklists

`TEST_CHECKLIST.md`, `TEST_STEP2.md` … `TEST_STEP8.md`, `TEST_FP_WALK.md`.

Written per build step, in the form *open this scene, press Play, look for
this*. They are genuinely useful and they are also **step-scoped** — they test
what that step added, not whether anything earlier still works. There is no
regression suite.

## 2. Editor validators — the strongest tool in the project

| Menu | Checks |
|---|---|
| `Tools → Rooms → Validate Twenty Seeds` | generates 20 floors, checks every rule, reports which door counts appeared |
| `Tools → Rooms → Preview Ten Floors` | builds 10 floors side by side to look at |
| `Tools → Rooms → Validate Modules In Scene` | room prefabs against the module contract |
| `SAFE DEPOSIT → …` | prefab builders and repair tools that report what they changed |

`FloorValidator` is worth studying as a model: it **returns faults and never
repairs**, and the generator throws the floor away rather than patching it. The
reasoning is written in the file — *"repairing one is how you get a level that
passes every check and still has a door into the skybox, because the repair is
applied after the check that would have caught it."*

## 3. Instrumentation, read from `Editor.log`

The pattern that has repeatedly ended long debugging loops: add one
`Debug.Log` that prints the values in question, run once, read the log, delete
the log line.

`Editor.log` lives at
`C:\Users\<user>\AppData\Local\Unity\Editor\Editor.log` and can be read
directly — screenshots of the console are not needed.

**This is the single most effective debugging technique in this project's
history.** Two separate multi-round arguments in one day were settled by one
log line each, after reading the source had settled neither.

## 4. Two-machine sessions

The only proof for anything networked. Phase 4 kept an explicit table of five
done-whens that had **never been seen working with two people**, and did not
close the phase until they were run.

---

## Verification rules this project already follows

**Compiling is not verifying.** `ROADMAP.md` keeps a whole section called
*"what is built vs what is proven"*. Respect the distinction in every report.

**Check that the code under test is the code running.**
- Compare `Library/ScriptAssemblies/Assembly-CSharp.dll` mtime against the
  source file's mtime.
- Unity **defers assembly reload while in Play mode**, so a recompile mid-
  session only lands after you stop. This has caused at least three false
  "your fix didn't work" reports.
- A standalone build never picks up script edits at all.

**A serialized value and a code default are different things.** Changing a
field's default in C# does nothing to a value already saved in a prefab or
scene. This project has been bitten **four times**. Always check the prefab.

**Watch out for these two search mistakes**, both of which produced confidently
wrong conclusions during development:
- .NET stores string literals as **UTF-16** in a dll — an ASCII `grep` will
  say a string is absent when it is present.
- `grep` silently skips files it judges binary; use `-a` on Unity YAML.

---

## What to build first, when tests are wanted

Highest value per hour, and none of it needs a scene or a network:

1. **`FloorGraph`** — pure integer logic with real invariants. Every node's
   degree must equal its door count; room caps must hold; leaves must be
   1-door rooms. These have already been checked once by reproducing the
   arithmetic in Python, which is exactly the signal that they want to be
   EditMode tests instead.
2. **`Campaign` formulas** — quota, rescue cost, rescue deadline, scaled
   prices. Pure functions over integers.
3. **`Carryable` weight classes** — three thresholds that four systems now
   depend on. A test here is a tripwire on a shared assumption.

A PlayMode suite is more work and lower value until the IMGUI layer is
replaced.

---

## Regression checklist until then

After any change, before calling it done:

- [ ] It compiles — confirmed in `Editor.log`, not assumed
- [ ] The dll is newer than the source you edited
- [ ] A full round still plays: descend, loot, load, RETURN, results, shop
- [ ] Solo play still works with no network active — this has been an
      invariant through every phase and is worth keeping
- [ ] If anything networked changed: two machines, not two windows on one
      Steam account
- [ ] If a prefab default changed: the prefab was checked, not just the C#
