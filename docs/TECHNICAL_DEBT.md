# SAFE DEPOSIT — Technical Debt

Things that work today and will cost later. Ordered by what they will cost.

**Nothing here is an accusation.** Most of it is the correct trade for a solo
prototype racing a submission date. The point is to know the bill before it
arrives.

---

## 1. No automated tests, and no test assembly

`com.unity.test-framework` 1.6.0 is installed. **Zero test files exist.**

Verification today is manual play, editor validators, and reading
`Editor.log`. That has worked because the developer is also the only player —
but the project is now 34,000 lines with a networking layer, and the docs
themselves record the pattern: *"written and compiles" is not "verified"*.

**Cost:** every refactor is a full manual regression. Phase 4 needed a second
human before five of its done-whens could be checked at all.

**Cheapest first move:** an EditMode test assembly around the pure logic that
already exists — `FloorGraph` (degree invariant, room caps), `Campaign`'s
formulas (quota, rescue cost, deadlines), `Carryable`'s weight-class
thresholds. None of these need a scene or a network.

## 2. No assembly definitions

Everything is `Assembly-CSharp`. Consequences: every script can reference every
other script, so architectural layering is convention only; a one-line change
recompiles the whole project; and there is no seam to put tests behind.

**Cost:** compile time now, tangled dependencies later. **Adding asmdefs to a
34k-line project is not a small job** and will surface accidental couplings —
which is exactly the value, and exactly the pain.

## 3. All UI is IMGUI (`OnGUI`)

No uGUI canvas, no UI Toolkit, no HUD prefab. Every readout is drawn in
`OnGUI` inside the system that owns the data.

`OnGUI` runs at least twice a frame and allocates. `SceneRefs` exists partly
because `RunHudGate` was doing **eighteen full scene searches per frame**
through it.

**Cost:** IMGUI is not a shippable UI layer for a commercial game — no
controller navigation, no scaling story, no styling to speak of. Phase 7 needs
a shop interface, and Phase 8 is the polish pass. **One of those two is the
moment this gets replaced**, and doing it late means porting every readout at
once.

## 4. Campaign state is static

Documented and deliberate — it must survive scene reloads. The network mirror
makes it work. But static state is global state: anything can write
`Campaign.Money` from anywhere, ordering bugs are possible at scene load, and
it cannot be instantiated twice for a test.

**Cost:** this is the thing most likely to make item 1 hard.

## 5. No save system

See `KNOWN_ISSUES.md`. Listed here too because the *architecture* is the
obstacle: state spread across static fields and NetworkVariables has no single
place to serialise from. Adding saving later means touching every one.

## 6. One scene, hand-maintained

`Prototype.unity` holds the shaft, twenty levels, the elevator, loot roots and
network roots. There is no menu scene and no additive loading.

**Cost:** the scene is a large YAML file that only one person can edit at a
time, and Unity re-saves it on its own schedule — the project has already been
bitten by scene edits being clobbered. It is also why the twenty levels are
all instantiated at once, which is exactly what Phase 5's `FloorDirector` was
written to stop.

## 7. Duplicated room prefab set

Two folders, seven prefabs each. See `KNOWN_ISSUES.md`.

## 8. Dead code and dead bindings

- Five unused input actions from the deleted rope
- `Assets/_Recovery/` — 6 scene files, 3.4 MB, unreferenced
- `Assets/TutorialInfo/` — Unity template leftovers
- `Assets/Scenes/SampleScene.unity` — template leftover

## 9. Very large files

`RunManager` 1,369 · `Campaign` 1,183 · `FirstPersonViewmodel` 1,100 ·
`ProceduralLegs` 1,089 · `PlayerCarry` 1,068.

Much of that length is genuinely-valuable comment explaining *why* — which is a
project requirement, not padding. But a 1,300-line `MonoBehaviour` is hard to
test and hard to reason about, and `RunManager` in particular owns the round,
the quota, extraction, collapse, room state and results.

**Do not split these speculatively.** Split one when a change forces you to
read all of it.

## 10. IK ordering depends on component order

`OnAnimatorIK` is delivered in **component order on the Animator's
GameObject**, not by `DefaultExecutionOrder`. Several long bugs came from this,
and the rule the project settled on is written down. But it means **reordering
components in the Inspector can silently break arm IK** — an invisible
dependency no compiler will catch.
