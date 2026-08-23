# SAFE DEPOSIT — Phase 4: Netcode, and proximity voice

Corresponds to **Block 4** in `DEMO_PLAN.md` — 7 weeks, 2 Nov – 20 Dec 2026.
**The biggest unknown in the project**, and the one that can eat the whole
five-week buffer.

Written 21 Aug 2026, after Phase 3 shipped, from a survey of the code as it
actually stands.

---

# PART 1 — WHAT THIS PHASE IS FOR

Half the design does not exist without it. The key-with-a-triangle puzzle
exists to sell the walkie-talkie. The ledger is *"pure voice-chat gameplay"*.
Naming the missing crewmate on the departure screen only works because
**"three people shouting one name is the moment."**

Everything built so far is a rehearsal for four people in a lift arguing about
whether to leave.

## What is different about this phase

Every phase until now was testable by one person at one desk. **This one is
not.** Two builds on one machine — a host window and a client window — covers
most of it. Voice needs a second human.

That is why Phase 3 existed, and why it earned its keep: the two-body rig
already found a shuffled crew slot and a shared keyboard. Both would have
arrived here disguised as replication bugs.

---

# PART 2 — THE SURVEY

## 59 public statics, and that is the whole phase in one number

| File | Public statics | What it holds |
|---|---|---|
| `Campaign.cs` | **38** | money, cable, rooms, loot roster, Lost crew, the round |
| `PlayerRegistry.cs` | 12 | who the players are |
| `Crew.cs` | 5 | HP, bleed-out, Lost, packs — per person |
| `SceneRefs.cs` | 4 | the lift, the run |

**A static is one copy per PROCESS.** Host and client each get their own. Right
now `Campaign.Money` on a client is simply a *different number* from the host's
— not stale, not lagging, unrelated.

This is not a flaw in how they were written. `Campaign` and `Crew` are static
because they must survive `ReloadScene`, which was the correct reason and is
still true. What changes is that surviving a scene reload and surviving a
*network boundary* are different problems, and only one of them has been
solved.

**The shape of the fix is host-authoritative:** the host owns the numbers,
clients hold a replicated read-only view. Nothing on a client writes to
`Campaign` directly ever again — it asks, and the host decides.

## The scene reload is a networked event now

`RunManager.ReloadScene()` calls `SceneManager.LoadScene`. Every client has to
make that transition **together**, and anyone mid-load must not be counted as
having left the run. This is a bigger deal than it looks and it sits right on
the seam between a round ending and the shop opening.

## 17 runtime spawn sites

Loot, downed players becoming `Carryable`, the headlamp rig, rubble seals.
Each is either network-spawned by the host, or deterministically rebuilt
per-client from replicated data. **The loot roster already makes the second
option viable** — `Campaign.LootRoster` is a complete description of every item
in the building, so clients can rebuild it rather than receive 60 spawns.

## What Phase 3 already got right for this

Worth listing, because it is the reason 7 weeks is plausible:

- `PlayerMotor.MarkLocal` exists and is public — the network calls it, and
  every "is this mine" gate in the game already reads the result
- `Crew` is keyed on a **slot**, which maps directly onto a client id
- `PlayerRegistry` is the one answer to "who are the players"
- No `Camera.main` anywhere — every camera has an owner
- The lift is a position and a state, not a 32-node simulated rope.
  `ROADMAP` credits deleting that rope with buying three of these seven weeks

---

# PART 3 — WHAT WILL BREAK LOUDLY

**1. The elevator carries riders by writing their positions directly.**
`Elevator.FixedUpdate` does `r.position += delta` on every rider, every step.
With client prediction on your own body, that is two authorities writing one
transform — the classic rubber-band. `ELEVATOR_SPEC` already insisted riders
move in the *same* physics step as the car; doing that across a wire is the
single hardest technical problem in the phase.

**2. A held `Carryable` goes kinematic and is positioned from the camera in
`LateUpdate`.** That is a client-side visual on an object the host owns.

**3. `Campaign.Settle()` mutates money and can end the campaign.** It must run
on the host and only the host, or two machines will disagree about whether you
survived.

**4. The ten-second overload countdown is local.** `CableWear` reads the deck
and kills the run. Two machines counting separately will not agree on when the
cable parted.

---

# PART 4 — BUILD ORDER: ELEVEN STEPS

**One step per session.** Each ends with something that runs and a commit.

### Step 1 · Two windows, connected
Netcode for GameObjects + the transport. Direct connect, no lobby, no
gameplay. **Done when:** a host window and a client window agree they are
connected.

### Step 2 · The body on the wire
Player is a `NetworkObject`. Position, rotation, animation state. `IsOwner`
drives `MarkLocal`, so Phase 3's gates light up for free.
**Done when:** you watch your friend walk, and neither of you is headless.

### Step 3 · The shared pot
`Campaign` becomes host-owned with a replicated view. Money, cable, destroyed
rooms, the loot roster, the Lost crew, the round number.
**Done when:** the host buys cable and the client's shop shows it.

### Step 4 · Per-person state
`Crew` slots bind to client ids. HP, injury, bleed-out.
**Done when:** two players have different HP and both HUDs are right.

### Step 5 · The lift
Floor, moving, doors, bridge, load gauge, the overload countdown. **Riders in
sync** — the hard one from Part 3.
**Done when:** two people ride down together and nobody rubber-bands.

### Step 6 · Loot
Host spawns from the roster; clients rebuild rather than receive 60 spawns.
Carrying, dropping, the deck's load.
**Done when:** one player watches another carry a crate into the car and the
gauge moves for both.

### Step 7 · Downed, carried, and REVIVE ★
*Deferred here from Phase 2 Step 7.* `DownedPlayer.Revive()` is already
finished — this is the med spray, the use interaction, and the first honest
test of any of it.
**Do this the moment Steps 2 and 6 work.** "One player sprays another back
onto their feet" is the cheapest possible proof that downed, carry and revive
all replicate at once.

### Step 8 · The run loop
Extraction, results, shop, and the **networked scene reload** — everyone
transitions together, and nobody mid-load is counted as having left.
**Done when:** two players finish a round and both see the same shop.

### Step 9 · Rescue contract
*Deferred here from Phase 2 Step 9.* `Rescue(R,f) = Mafia(R) × (1 + f/10)`,
partial payment carried over, three deaths ends the campaign.
`Campaign.LostCrew` already records who and on which floor.
**Done when:** the crew argues about cable versus their friend — which is the
whole point, and needs a crew to happen at all.

### Step 10 · 🎙️ PROXIMITY VOICE
**Done when:** you can hear somebody through a wall and it is quieter.

### Step 11 · The crew screens
Leader, Change Leader vote, and the departure vote that names whoever is not
aboard.
**Done when:** three players ride together and the vote names the person still
in a room.

---

# PART 5 — DECIDED, 21 AUG 2026: THE FREE STACK

## Unity Netcode for GameObjects + Facepunch Steam transport + Dissonance

**No monthly bill, ever. No player ceiling.**

### How this decision moved twice, and why

The first draft recommended NGO — a guess, from preference. Then I researched
what the reference games ship, found all three on Photon, and switched to
Photon Fusion 2. Then the actual constraint arrived: **no recurring cost.**

That is not a preference. It is a solo developer with no revenue and a Next
Fest deadline, and it changes which evidence matters. Researching against it
turned up a closer comparable than any of the first three.

### Lethal Company is the real comparable

| | Lethal Company | SAFE DEPOSIT |
|---|---|---|
| Team | one developer | one developer |
| Players | 4-player co-op | 4-player co-op |
| View | first person | first person |
| Loop | scavenge a dangerous place | scavenge a dangerous place |
| Pressure | **meet a quota or you are fired** | **meet the mafia's quota** |
| Exit | a ship that leaves | a lift that leaves |
| Core mechanic | **proximity voice** | **proximity voice** |
| Platform | Steam | Steam |

Closer to this game than PEAK, We Were Here or R.E.P.O. — and it runs on:

- **Unity Netcode for GameObjects** — free, Unity's own
- **Facepunch.Steamworks transport** — free, in Unity's own
  `multiplayer-community-contributions` repo
- **Dissonance Voice Chat** — a ONE-TIME Asset Store purchase, no subscription

### Why it costs nothing to run

Steam Datagram Relay is **free to Steam developers, with no CCU limit**. Valve
carries the traffic over the same backbone as CS:GO and Dota 2, hides player
IPs from each other, and often finds a faster route than the open internet.
There is no tier to outgrow and no dashboard to watch during Next Fest.

Photon is the opposite model: an excellent service with a bill that scales
with your success. PEAK and We Were Here pay it monthly because they are
companies with millions of sales. **You are not, yet.**

### What the free stack costs instead

Stated plainly, because it is real.

**Fusion has networked physics prediction built in. NGO does not.** Part 3 of
this file names rider sync as the hardest problem in the phase, and on this
stack it is ours to solve rather than the SDK's. That is the trade: money
saved, work added.

It is the right trade here for three reasons. The lift is a **kinematic
platform on a scripted path** — not ragdolls, not vehicles. It is "the floor
moved down 5 cm this step and everyone standing on it moved too", which
replicates as a floor number and a lerp. Lethal Company's ship does the same
thing on the same stack. And a monthly bill on a game with no revenue is a
risk with no ceiling, while a hard problem is a risk with a bottom.

### Voice: two options, neither a subscription

**Dissonance Voice Chat** — one-time Asset Store purchase, official Netcode
for GameObjects integration, and exactly what Lethal Company's proximity chat
runs on. Check the current price; it is regularly on sale. **Recommended.**

**Steam Voice** (`ISteamUser` voice API) — genuinely free, and you build the
3D positioning and falloff yourself. Choose this only if the Dissonance price
blocks, because what you save in money you spend in Step 10.

---

# PART 5b — WHAT YOU HAVE TO DO BEFORE STEP 1

All free. No account signup for the networking at all.

1. **Install Netcode for GameObjects** — Package Manager → Unity Registry →
   search "Netcode for GameObjects"
2. **Install the Facepunch transport** — Package Manager → Add package from
   git URL:
   `https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch`
3. **Steam App Id 480** (Spacewar, Valve's public test app) covers
   development. Your real one arrives with the Steam page.
4. **Steam running and logged in** on the machine — Steam networking needs it.
5. **Dissonance — not yet.** That is Step 10, and it is the only money in
   this phase.

Tell me when 1–4 are done and Step 1 starts.

---

# PART 6 — WHAT IS EXPLICITLY NOT HERE

- **Dedicated servers.** Host-authoritative, one player hosts.
- **Anti-cheat.** A four-player co-op game with friends.
- **Reconnection.** If somebody drops, the run continues without them and the
  Lost roster already knows how to describe that.
- **More than 4 players.** `Crew.MaxMembers` is 4 and says so loudly.

---

# PART 7 — THE FOUR RULES (unchanged)

1. **One step per session.**
2. **Explanation before code.**
3. **Commit after every step.**
4. **Read before write.**

And the new one, which this phase adds: **two windows, every time.** A netcode
change that was only ever run as a host is a netcode change that has not been
tested.
