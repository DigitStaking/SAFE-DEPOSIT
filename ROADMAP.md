# SAFE DEPOSIT — Master Plan (build + ship)

Last updated: 2026-08-06  
Stack: Unity 6.3 LTS, URP, PEAK-style flat art, online co-op FP  
Project path: `C:\Users\Digitstak\SAFE DEPOSIT`

This file is the working agreement between Marouane and Hermes.
Design truth lives in `GAME_DESIGN.md`. Art target = concept board (orange divers, yellow rope, absurd loot, dark shaft, **gameplay camera = first person**).

---

## 0. Honest status (what exists on disk)

### Built and real (~4k lines C#)
| System | Files | Notes |
|---|---|---|
| Graybox shaft gen | `Editor/GrayboxBuilder.cs` | 5 floors, rotated doors, loot cubes |
| FP camera | `FirstPersonCamera.cs` | Not parented to RB — correct |
| Motor | `PlayerMotor.cs` | Rigidbody + accel budget |
| Main rope | `MainRope.cs` | Anchor, length, bend, load limit |
| Tether | `PlayerTether.cs` | 2.5m swing / 10m rooms, cut = invisible |
| Hook / pin | `RopeHook.cs` | Doorway kink |
| Carry + pack | `Carryable`, `PlayerCarry`, `PlayerBackpack` | Weight classes |
| Arms (proto) | `PlayerArms.cs` | Shared body, no viewmodel — keep this |
| Run loop | `RunManager.cs` | Quota, extract, deadline |
| Campaign | `Campaign.cs` | Money, rope length, floors destroyed |

### Not built (blocks “friends can play”)
- Netcode / second player
- Real art (meshes, PEAK materials, lighting pass)
- Shop UI closing the meta loop
- Cross-room puzzles, threats, survivors, Collector
- Audio, polish, Steam build pipeline
- Discord / Steam page / trailer

### Reality check
You have a **strong solo vertical slice of feel** (rope + weight + run).  
You do **not** yet have a shippable co-op game. Co-op is ~40–50% of remaining risk.

---

## 1. Product goal

**One sentence:** Four friends hang on one yellow rope in a collapsing bank-shelter, arguing out loud whether to haul gold, people, or evidence before the government blows the floor.

**Why it can win:** Co-op + physics comedy + moral weight limit = **clip factory**. Free marketing is the real CAC strategy — correct instinct.

**Not the goal:** Photorealism. Photoreal fights PEAK DNA and your timeline.

---

## 2. Scope gates (do not skip)

| Gate | Definition of done | Approx when* |
|---|---|---|
| **G0 — Solo fun** | Shop closes loop; atmosphere matches art board; 3 loot silhouettes; 1 full graybox run feels tense alone | Month 1–2 |
| **G1 — 2P local or online** | Two bodies on one rope, load shared, extract requires both | Month 3–5 |
| **G2 — Demo (4P)** | 4 players, 3 floors, 5 room types, 2 puzzles, 1 survivor choice, collapse, load limit. Grey-or-art OK if fun | Month 6–9 |
| **G3 — Full 1.0** | Campaign depth, Collector, 2–3 threats, shop full, 10–15 floors of content, polish, Steam Deck | Month 16–22 |

\*Assumes **you** full-time-ish on Unity + Hermes as co-dev on systems/art pipeline/tools. Slips if netcode fights you or art is blocked. Solo hobby pace ≈ ×1.5–2.

**Demo is NOT an infinite run.**  
Your design’s soul is the **ratchet** (floors die when you surface) + **quota/mafia pressure**. Infinite roguelike deletes the story and the panic.  
Demo = short campaign slice: few runs, rope progression lite, collapse visible, “do we save them?” once.

---

## 3. Build phases (work together)

### Phase A — Make the graybox look like the trailer (2–4 weeks)
- URP: dark ambient, fog, headlamp, yellow rope mat, PEAK-flat materials
- Placeholder loot silhouettes (vending, piano, bust, extinguisher…) wired to `Carryable`
- Shop between runs (money → rope) so campaign is playable end-to-end solo
- ART_BIBLE.md + capsule readability test
- **Exit:** 60s clip that looks like panel 1/5 energy even with block characters

### Phase B — Netcode spine (6–12 weeks, highest risk)
- Pick stack early: **Netcode for GameObjects (NGO) + Unity Relay/Lobby** or **FishNet** / **Mirror** — decide in week 1 of B
- Host-authoritative rope state (one rope sim, all clients read)
- Player spawn, tether attach, carry ownership, run timer sync
- Voice: proximity optional later; start with Discord/Steam overlay
- **Exit:** you + 1 friend finish a run without desync disasters

### Phase C — Demo content (8–14 weeks overlapping B polish)
- 3 floors, 5 room modules, 2 co-op puzzles (keycard + 3-handle or ledger)
- 1 survivor choice, load limit readable UI, collapse beat
- 4 player colors from character sheet
- Tutorial that teaches rope in 90s without a novel
- **Exit:** closed playtests score “would wishlist / would stream”

### Phase D — Steam presence (start during C, not after)
- Steamworks page the week you have **one great clip** (even pre-demo)
- Capsule = panel 5 composition; trailer = money shot + FP glove shot
- Build Discord, creator key form, press kit
- **Exit:** page live, weekly content cadence

### Phase E — Full game after demo data
- Only build systems demo players begged for
- Collector, threats, evidence endgame, more floors
- Localisation pass near end (EN first; FR useful for Morocco/EU)
- **Exit:** 1.0 launch checklist green

---

## 4. Marketing plan — what you got right, what to change

### You got right
- Wishlists first, page early, post often
- Free keys to creators > paid spam early
- Co-op with friends = organic reach
- Post-launch: patch fast, stay human
- Revenue is not sticker price (refunds, VAT, Steam 30%, art, tax)

### Corrections (important)

| Your assumption | Better reality (2026) |
|---|---|
| Need **+20k** wishlists to be “top of trends” | **Popular Upcoming** bar moved a lot; some reports put visible slots at very high wishlist counts after Steam ranking changes. Treat **velocity + conversion + tags** as the real game. 7–12k was old indie folklore; **20k is a strong goal**, not a guarantee of “top trends.” Success ≠ only Popular Upcoming. |
| Demo after ~4 months | Only if G1–G2 are real. **Ship demo when 4 friends laugh**, not on a calendar. 6–9 months from now is more honest given netcode. |
| Demo = infinite run | **No** for SAFE DEPOSIT. Finite runs + collapse + rope buy = the hook. Infinite is a different game. |
| Launch on **weekend** | Your own design said **Tue–Thu**. For Steam discovery, midweek launches usually get a cleaner algorithm window; weekends compete with AAA leisure time. Prefer **Tue–Thu**. |
| Publishers will come and I accept | Possible after a hot demo. **Do not plan on it.** Self-publish first; only take a deal if they buy **reach you cannot**, and you keep creative control. Many co-op hits stay indie. |
| Paid creator video after success | Yes — but **only** after organic proof. Clip-first genre: seed funny creators who already play PEAK / LC / R.E.P.O-likes. |

### Wishlist math (directional, not promise)
- Launch-week buy rate often cited ~5–10% of wishlist in good cases (genre/sentiment dependent).
- 10k wishlists → rough ballpark 500–1000 copies week 1 before refunds (wildly variable).
- Price example $17.99: after Steam ~30%, regional VAT, refunds ~10–15%, payment noise → **you might keep ~40–50% of gross** before your own costs (art, music, tools, tax). Publisher split cuts that again.

### Content machine (free marketing)
Weekly loop once page exists:
1. One **clip** (rope fail, piano drop, betrayal cut tether)
2. One **dev log** (screenshot + one sentence of design honesty)
3. One **question** to audience (“gold or the guy?”)
Platforms: X, TikTok, Instagram Reels, YouTube Shorts, Discord.  
Tags: Co-op, Multiplayer, Horror-comedy, Physics, Extraction-adjacent — research exact Steam tags when page is drafted.

### Creator seeding
- Free keys, no script, ask for honest fun
- Target: small–mid co-op chaos channels, not only mega streamers
- Discord long-time members = playtest + loyalty, not only marketing

### Next Fest
- **One** Next Fest ever for this title (your design is right)
- Enter only with a demo that already converts playtime → wishlist

---

## 5. When can we “finish”?

| Milestone | Target window | Marketing action |
|---|---|---|
| Atmosphere + shop solo (G0) | **~1–2 months** | Private clips only; refine art bible |
| 2-player online stable | **~3–5 months** | First public teaser page OK if clip is god-tier |
| **Demo freeze** | **~6–9 months** | Steam page push, creator keys, Next Fest plan |
| Wishlist campaign peak | Demo → +3–6 months | Cadence, not silence |
| **1.0 launch** | **~16–22 months** from now | Midweek, patch team ready day 0 |

**“Finish the game” = 1.0 ≈ 18 months** if you stay scoped and netcode doesn’t eat a year.  
**“Finish something players can love with friends” = Demo ≈ 6–9 months.**  
That demo is what marketing actually sells.

If you only have nights/weekends, multiply by ~1.5–2.

---

## 6. How we work together (Unity)

1. You stay in Editor (Play, feel, art placement, final calls).
2. Hermes edits scripts, builders, materials YAML, docs, tools in `Assets/_Project`.
3. One phase goal at a time; no feature soup.
4. Playtest rule: if graybox isn’t funny with friends, art won’t save it.
5. Netcode decision is a **team checkpoint** — don’t delay past Phase B start.
6. Repo: keep git commits small; never commit `Library/`.

### Immediate next sprint (start now)
1. Phase A atmosphere (fog, lamp, mats, rope color)
2. Shop stub so campaign loop closes
3. Loot placeholder set matching concept props
4. Short `ART_BIBLE.md` from concept board
5. Then Phase B research spike: NGO vs FishNet written decision

---

## 7. Anti-goals (kill these if they appear)

- Weapons loadout / PvP shooter creep
- Photoreal PBR bank museum
- Infinite run demo
- Waiting for publisher before building netcode
- Expanding floors before 4P extract works
- Weekend launch without a reason
- Paid ads before organic clip proof

---

## 8. Open decisions (resolve soon)

1. Netcode stack: NGO+Relay vs FishNet vs other  
2. Online-only vs + listen server / Steam P2P  
3. Demo includes shop or pure 3-floor raid? (Design says no shop in demo — OK)  
4. Solo developer art: buy kitbash PEAK-like pack vs commission character  
5. Price target band ($12.99 / $17.99 / $24.99)  

---

## 9. Bottom line

Your systems brain is ahead of most first games. Your marketing instincts are ~70% right; the fixes are: **finite demo, midweek launch bias, don’t bet the farm on Popular Upcoming or publishers, and treat co-op netcode as the critical path.**

We build for **clip-native co-op fun**, ship a **tight demo**, then grow the full shelter.

**Next message to start production:** confirm Phase A kickoff (atmosphere + shop + loot placeholders).
