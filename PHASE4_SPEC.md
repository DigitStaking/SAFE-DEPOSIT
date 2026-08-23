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

# PART 5 — DECIDED, 21 AUG 2026, FROM WHAT THE COMPARABLES SHIPPED

Researched rather than guessed. All three reference games use **Photon**, and
the third one is the closest comparable this project has.

| Game | Studio | Stack |
|---|---|---|
| **R.E.P.O.** | Semiwork | Photon (PUN 2) |
| **PEAK** | Aggro Crab + Landfall | Photon |
| **We Were Here** | Total Mayhem | Photon PUN + **Photon Voice** |

Two details in that table matter more than the unanimity.

**Semiwork chose Photon on Landfall's recommendation** — so REPO and PEAK are
the same lineage, not two independent votes. And **We Were Here runs its
walkie-talkie on Photon Voice**, which is the nearest thing that exists to
this game's proximity-voice design: a co-op game whose entire mechanic is two
people talking to each other.

## THE DECISION: Photon Fusion 2 + Photon Voice 2

This **overrides `ROADMAP.md`'s "Netcode for GameObjects + Steam transport"**,
which was written before any of this was checked, and it overrides my own
recommendation of NGO in the first draft of this file. Four reasons, in order
of weight:

**1. Physics.** Fusion supports networked physics objects natively; PUN 2 does
not. Photon's own writeup of REPO quotes Semiwork — *"making all the physics
feel smooth for clients was something we struggled with for a long time"* —
and says plainly that this is *"much easier with Photon Fusion"*. SAFE DEPOSIT
is a physics game: a kinematic lift that carries rigidbody riders, loot with
real mass, a person who becomes 70 kg of cargo. Part 3 of this file already
names rider sync as the hardest problem in the phase. **This is the reason.**

**2. Voice is in the same SDK.** Photon Voice 2 puts a `Speaker` on the
network object and lets the engine position it in 3D. Step 10 stops being an
integration project against a second vendor and becomes a component on a
prefab that already exists. We Were Here shipped four games on exactly this.

**3. Free to build, and nearly free to launch — with one number to watch.**
Checked properly on 21 Aug 2026, because "it's free" needed to be true rather
than approximately true:

| | Free | Then |
|---|---|---|
| **Fusion** (the game) | **100 CCU**, commercial use | $125/mo at 500 CCU |
| **Voice** (proximity chat) | **20 CCU** — dev plan, 60 GB/mo | **$95 once for 12 months** at 100 CCU |

CCU is CONCURRENT, not total — Photon put 100 CCU at roughly 40,000 monthly
players. So development is free outright: you will never have twenty people
testing at once.

**Voice is the ceiling that breaks first, at 20.** It is a separate app with
separate CCU counting, and it hits its cap five times sooner than the game
does. The failure looks like the game working fine while nobody can hear
anybody — which, in a game whose puzzles are built on talking, is the same as
being down.

**Budget $95 before Next Fest.** One payment, twelve months, lifts voice to
match the game's 100. Against a submission deadline that is not a decision,
it is a line item.

⚠️ **And watch the dashboard during the Fest.** PEAK shipped on Photon and
players hit "Disconnected from Photon" when it spiked — this exact ceiling,
in the wild, on a game that did well. A demo that goes better than expected
is the scenario where this costs money, and it is the good scenario.

**4. Relay is built in.** No NAT punching, no Steam dependency to get two
people connected. Steam invites can arrive at Step 11 as a convenience rather
than as the thing standing between you and a working test.

### What this costs, stated honestly

Fusion has **fewer tutorials than PUN 2**, which is the price of it being the
current SDK rather than the popular one. And it is a **hosted service** — NGO
is free forever and Photon is not, at scale. Both are worth it here: the
tutorial gap costs days, and the physics gap would cost weeks.

### PUN 2 is not an option

Deprecated, and no native physics support. REPO ships on it because Photon
found out about REPO *weeks before launch*, too late to change SDK — not
because it was chosen on merit for a physics game.

---

# PART 5b — WHAT YOU HAVE TO DO BEFORE STEP 1

I cannot do these. They need an account and the Unity UI.

0. **Nothing costs anything today.** Both SDKs are free to import and the
   free tiers cover every day of development. The only money in this phase is
   the optional $95 for voice before launch — see Part 5.
1. **Create a Photon account** at `dashboard.photonengine.com`
2. **Create an app of type `Fusion`** — copy the App Id
3. **Create a second app of type `Voice`** — copy that App Id too
4. **Import Photon Fusion 2** (Unity Package Manager, from Photon's dashboard
   download or the Asset Store)
5. **Import Photon Voice 2**
6. Paste both App Ids into `Fusion > Realtime Settings`

Tell me when those are in and Step 1 starts. Everything before that point is
mine to write; none of it can be written first.

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
