# SAFE DEPOSIT — Real animations (Mixamo) + visible hands

## Honest limits

I **cannot** create real skeletal walk/grab/emote animations from code alone.

What I can do:
- gameplay code
- Animator parameter driver
- hide fake backpack cube
- first-person head cull

What **you** need for real animation:
- **Mixamo** (or Blender) animation clips
- Player FBX set to **Humanoid**
- Animator Controller with Idle / Walk / Carry

Your current `Player.fbx` import is **not Humanoid yet** (`human: []` in meta), and it has **no clips**, so it stays in **T-pose**. That is why:
- no real walk
- arms stuck out sideways
- hands not comfortably in the camera

---

## Part A — Mixamo (you do this)

### 1. Get animations
Go to: https://www.mixamo.com

For your diver character:

**Option A (easiest if Mixamo accepts your FBX):**
1. Upload `Player.fbx` (or a cleaner humanoid version)
2. Download these animations as **FBX for Unity**:
   - `Idle`
   - `Walking`
   - `Running` (optional)
   - `Idle Holding Object` / `Carry`
   - `Climbing` (optional for later)
3. Settings:
   - Format: **FBX for Unity**
   - Skin: **With Skin** for one full character pack, or **Without Skin** if retargeting
   - Frames: 30
   - In Place: **ON** for walk/run (root motion off is easier for rigidbody player)

**Option B (if your custom diver fails Mixamo upload):**
1. Use a Mixamo character temporarily to download clips
2. In Unity, retarget those clips onto your diver Humanoid avatar

Put downloaded FBX files here:

```text
Assets/_Project/Models/Animations/
```

Suggested names:
```text
Anim_Idle.fbx
Anim_Walk.fbx
Anim_Carry.fbx
Anim_Climb.fbx
```

### 2. Make Player Humanoid in Unity
1. Select `Assets/_Project/Models/Player.fbx`
2. Inspector → **Rig** tab
3. Animation Type: **Humanoid**
4. Avatar Definition: **Create From This Model**
5. Click **Apply**
6. Click **Configure...**
7. Check all required bones are green
8. Done / Apply

If Configure fails:
- the rig is not Mixamo-compatible
- you must re-export from Blender with a standard humanoid skeleton
- or use Mixamo auto-rigger on a mesh that works

### 3. Import each animation FBX
For each `Anim_*.fbx`:
1. Rig → Animation Type: **Humanoid**
2. Avatar: **Copy From Other Avatar** → your Player avatar
3. Animation tab:
   - Loop Time: ON for Idle/Walk
   - Root Transform Position (Y): Bake Into Pose
   - Root Transform Rotation: Bake Into Pose
4. Apply

### 4. Create Animator Controller
1. `Assets/_Project/Animation/`
2. Right click → Create → Animator Controller
3. Name: `AC_PlayerDiver`
4. Open it
5. Create parameters:
   - `Speed` (Float)
   - `Grounded` (Bool)
   - `Moving` (Bool)
   - `Carry` (Int)  // 0 none, 1 small, 2 heavy, 3 massive
   - `Climbing` (Bool)
6. States:
   - `Idle` (default)
   - `Walk`
   - `CarryIdle` (optional)
   - `Climb` (later)
7. Transitions:
   - Idle → Walk when `Moving` true
   - Walk → Idle when `Moving` false
   - Any/Idle → CarryIdle when `Carry` > 0
   - CarryIdle → Idle when `Carry` == 0

### 5. Put controller on the character
1. Select Player prefab child `PlayerModel_FBX_VISUAL`
2. Add **Animator** if missing
3. Controller = `AC_PlayerDiver`
4. Avatar = Player Humanoid avatar
5. Apply root Player has component **PlayerAnimatorDriver** (auto-finds Animator)

I already added:

`Assets/_Project/Scripts/PlayerAnimatorDriver.cs`

It writes Speed / Grounded / Moving / Carry / Climbing every frame.

---

## Part B — Hands visible in camera

### Why you cannot see hands now
1. Model is T-pose (arms horizontal)
2. No Idle animation putting arms down / forward
3. First-person camera is at eye height looking forward — T-pose hands are out of frame sideways
4. I did **not** edit your bone rig (I cannot safely auto-rig your FBX without Blender)

### What you should do

**Minimum (shared body, PEAK style):**
1. Add Mixamo **Idle** (arms down)
2. Add Mixamo **Walk**
3. Look slightly down in game — hands/arms become visible
4. Add carry animation so arms come forward when holding loot

**Best for grab + emotes in camera:**
1. Mixamo clips:
   - Idle
   - Walk
   - Standing React / Wave
   - Holding Object
2. Optional later: Unity **Animation Rigging** package
   - Two Bone IK on left/right arms
   - Hand targets parented in front of camera for grab poses
3. Emotes = Animator states triggered by keys (I can wire keys once clips exist)

I will **not** fake this with cubes/capsules again. Real hands = real clips on a real Humanoid rig.

---

## Part C — What I already fixed in code

1. **Removed fake “walk bob = animation” as default**
   - `PlayerProceduralAnim.useProceduralWalk = false`
2. **Hidden gray backpack cube** when FBX visual exists  
   (that black box on the back in your screenshot)
3. **PlayerAnimatorDriver** ready for Mixamo controller params
4. Head still culled in FP so helmet doesn’t fill the screen
5. Press **V** for third person to inspect body while testing

---

## Part D — Exact checklist for you

- [ ] Mixamo download Idle + Walk (+ Carry)
- [ ] Put FBX in `Assets/_Project/Models/Animations/`
- [ ] Player.fbx Rig = Humanoid + Configure OK
- [ ] Animation FBX copy Player avatar
- [ ] Create `AC_PlayerDiver` with params above
- [ ] Assign controller on `PlayerModel_FBX_VISUAL` Animator
- [ ] Play → walk → real legs/arms move
- [ ] Press V to verify from behind

---

## If you want, after Mixamo import
Send me screenshots of:
1. Player Rig tab (Humanoid + avatar OK)
2. Animator Controller window
3. Hierarchy of `PlayerModel_FBX_VISUAL`

Then I can:
- wire transitions precisely
- add keybind emotes
- add carry-state blend
- add climb blend later

I cannot invent convincing skeletal animation data without those clips.
