# Playtest — Step 2 (tether / rooms / pack)

## Fixed this pass

### Tether / T / F
- **No auto-reel** after jump (line stays out until you choose)
- **T only works hanging under the rope** (near clip). Deep in a room: refused
- **"reeling in..." only when T is actually shortening the line** (not when grounded)
- **No auto-reclip after F.** Must press **F** again near rope
- Intended room entry: **Q pin → F cut → loot → F reclip**

### Rooms (not invisible floors)
- Mid-run timer threatens a **random reachable room**
- When it hits 0: **rubble fills the doorway** (you cannot enter)
- If you are **inside that room** → run lost
- Then a **new countdown** for another room
- HUD: `room XX seals in M:SS` + list of sealed rooms
- Surfacing (**go back down** shop): seals **2 more random rooms** permanently

### Backpack
- Starts at **2 slots**
- Visible pack mesh on back
- Keys **1 / 2** (and 3+ if upgraded) withdraw that slot into hands
- **G** still dumps whole pack
- Small items still auto-stow on pickup when there is room

## What to test

1. Hang under rope, hold T → shortens, message only while reeling  
2. Jump with long line → does **not** auto climb  
3. On ground hold T → refuse, no fake “reeling in”  
4. F cut → stay cut until F near rope  
5. Q then F into a room  
6. Wait for room seal → rubble door, not missing floor  
7. Pick small loot → pack slots; press **1** to pull out  
8. Extract → shop says surfacing seals 2 rooms  

## Not yet (next)
- Real character / loot mesh animations (need art)
- Smooth rubble VFX / dust / camera shake on seal
- Sound
