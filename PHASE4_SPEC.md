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

### Step 3 · The shared pot — SCALARS DONE
`Campaign` becomes host-owned with a replicated view.
**Done when:** the host buys cable and the client's shop shows it.

Ten scalars are networked: money, cable, run number, capacity upgrades, both
per-round caps, campaign-over, the epitaph, cable strain, loot-seeded. The
static API is unchanged, so **all 97 read sites were left alone** — the only
edits were inside `Campaign` itself.

Only the host may spend. A client's press is a `ServerRpc`; the host re-runs
the same rules and the new number replicates back.

**Deliberately deferred, and why:** the three COLLECTIONS — `DestroyedRooms`,
`LootRoster`, `LostCrew` — move with the systems that own them.
`DestroyedRooms` needs the lift (Step 5), `LootRoster` needs loot (Step 6),
`LostCrew` needs the rescue contract (Step 9). Replicating a list before the
system that writes it is networked means guessing at when it changes, and
that guess would be rewritten at each of those steps anyway.

### Step 4 · Per-person state
`Crew` slots bind to client ids. HP, injury, bleed-out.
**Done when:** two players have different HP and both HUDs are right.

### Step 5 · The lift — DONE (load gauge waits on Step 6)
Floor, moving, doors, bridge. **Riders in sync** — the hard one from Part 3.
**Done when:** two people ride down together and nobody rubber-bands.

**The design, which is not the obvious one.** The obvious answer is to
replicate the car and let physics carry the riders. It cannot work: `Elevator`
does not push riders with friction — that was tried, and the note at
`Elevator.cs:336` records what happened — it *teleports* them by exactly the
distance the car moved. And a rider's body is owner-authoritative, so if the
host teleported your body down the shaft, `NetworkTransform` would drag it
back up every frame. That **is** the rubber-banding the done-when forbids.

So: **the host decides where the floor is; every machine answers "and
therefore where am I" for itself**, using the distance the car actually moved
since the last physics step. Same number, same teleport, same code path — and
the only body any machine touches is one it owns. Nobody is corrected, so
nobody rubber-bands.

`ElevatorBridge.RequestGoToFloor` was already the only way anything commanded
the car, so the client redirect is **one branch**. Second time this phase that
Phase 1–2 architecture turned a rewrite into a single `if`.

**Still per-machine, and honestly so:** the load gauge and the overload
countdown read what is physically inside the car, and most of that is loot.
Loot is Step 6. The gauge is not broken — it is correctly weighing two
different piles.

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

### Step 10 · 🎙️ PROXIMITY VOICE — and it has to sound like concrete

**Done when:** somebody two floors down cannot be heard at all, somebody one
floor down is a muffled thump you can *just* tell is a person, and a voice in
the shaft has a tail on it.

Specified on request, 21 Aug 2026, because "realistic" is not a feeling here —
it is four measurable things, and Dissonance can do all four because it hands
you an ordinary Unity `AudioSource`. Everything below is Unity audio on top of
a human voice, not a feature of the voice library.

**1. Distance.** `spatialBlend = 1`, logarithmic rolloff, `maxDistance` tuned
to roughly one room. Voice should die inside the space it was spoken in.

**2. Occlusion — the one that sells it.** A raycast from the speaker to the
listener's ear. Concrete in the way drives an `AudioLowPassFilter` cutoff down
and drops the volume. Floors are 5 m apart with a slab between them, so a
raycast ALWAYS hits: two floors away is silent with no special case, and one
floor away is a muffled thump you can *almost* identify. That "almost" is the
horror — worse than hearing them clearly and worse than not at all.

**3. Reverb by space.** A tight side room and a hundred-metre concrete shaft
are not the same acoustic. The shaft wants a long metallic tail; a room wants
tighter and drier. An `AudioMixer` group swapped by where the LISTENER stands,
rather than per-`AudioReverbZone`, so voice, footsteps and the collapse all
share one answer about where you are.

**4. The radio filter.** Band-pass plus a little distortion for the
walkie-talkie, on its own Dissonance channel so it ignores distance entirely.

**5. THE RADIO IS HALF-DUPLEX. ONE VOICE AT A TIME.**

Push to talk. First press holds the channel; anyone else pressing while it is
held gets nothing but a click, and the crew hears only the person who got
there first.

This is how a real radio works, and it is also the better game. It makes the
walkie-talkie a genuine TRADE rather than an upgrade:

|  | proximity | walkie-talkie |
|---|---|---|
| Range | one room | the whole building |
| Clarity | muffled by concrete | clear |
| Who can talk | everyone at once | **exactly one person** |

Four people panicking into one channel produces the thing the design keeps
reaching for: somebody has to shut up so somebody else can be heard. "Get off
the radio" is a sentence this mechanic writes by itself, and it costs nothing
to build - the host arbitrates who holds the channel, everyone else is muted
on it until released.

It also protects the moment on the departure screen. `DEMO_PLAN` says three
people shouting one name is the moment; three people shouting it over a radio
that only carries one of them is worse in exactly the right way.

### Why the concrete winning is a FEATURE

"I cannot hear you from room 3" is not a limitation to be tuned away. It is
the mechanic the economy already sells: `ECONOMY` Part 5 prices a
**walkie-talkie at 30** and a **radio relay at 75** — *"extends proximity
voice by one floor."*

Those items are only worth money because the building wins by default. If
voice carried across floors, that is two shop items nobody would ever buy, and
the key-with-a-triangle puzzle — which exists to sell the walkie-talkie — has
nothing left to sell.

Same shape as the Bandage and the cable. **The realism IS the economy.**

Polish lands in Phase 8's audio pass, which already lists "survivors screaming
through concrete" — the same system, pointed at an NPC. Step 10 owes the
mechanism; Phase 8 owes the tuning.

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

## The Epic fork — a planned decision, not a surprise

**Decided 21 Aug 2026: Steam now. Epic later, only if the game earns it.**

Steam relay only works for Steam players. An itch.io or Epic buyer could not
connect at all, and there would be no crossplay between stores.

**Epic Online Services is the answer when that day comes.** Free, no CCU
limit, works on Steam / Epic / itch / standalone, and crossplay between all of
them. Epic funds it to promote crossplay — their business is the store, not
selling services. There is an EOS transport for Netcode for GameObjects, the
same shape as the Facepunch one.

### Why not just start on EOS

Because the cost of being wrong runs one way:

- **Steam now, EOS later** → swap one component, re-run this phase's tests
- **EOS now, Steam only forever** → a dev portal, product and sandbox IDs, and
  an Epic account requirement imposed on every player, permanently, for a
  store you never shipped on

Steam is not optional here — Next Fest *is* a Steam event, so a Steam page
exists either way. And it is where this genre sells: Lethal Company, R.E.P.O.
and PEAK are all Steam. itch is mostly free and small games; Epic requires an
application.

### The trigger, written down

Epic takes **12%** where Steam takes **30%**. That is the real reason to go,
and it is a reason that only pays once there are sales to take a percentage
of.

**If multi-store happens, EOS stops being optional and becomes mandatory** —
for *everyone*, Steam players included. Shipping a four-player co-op game
across stores WITHOUT crossplay is worse than not shipping it there: "buy it
on Epic, but you can only play with other Epic players" is a bad deal in a
game that needs four people in a lift.

### What this costs today: nothing

This is why the transport sits behind Netcode for GameObjects rather than
being the SDK itself:

```
game code  →  Netcode for GameObjects  →  [ transport ]
                                               ↑
                        Steam · EOS · Unity Relay · direct IP
```

One component on one object. Game code never learns which one it is. Photon
would have been the opposite — there, the SDK *is* the networking, and
changing it is a rewrite rather than a swap.

**Revisit at Step 11**, when lobbies and invites arrive. Not before.

---

# PART 5b — SETUP, AND THE TESTING PROBLEM NOBODY MENTIONS

## Two transports, not one

Steam's relay needs a Steam client, and **one machine can only run one Steam
account.** Two windows on your PC therefore cannot talk to each other over
Steam networking — which would make Step 1 untestable until somebody else is
free, in the phase that most needs fast iteration.

So the project carries **both**, and swapping between them is one field on one
component:

| Transport | For | Needs |
|---|---|---|
| **Unity Transport** (ships with NGO) | daily work — two windows on this PC over 127.0.0.1 | nothing |
| **Facepunch Steam** | real play, real friends, the actual shipping path | Steam running |

This is not a workaround. It is the reason the transport sits behind Netcode
for GameObjects rather than being the SDK — the same swappability that makes
the Epic fork cheap makes local testing free.

**Steps 1–10 are built and tested on Unity Transport.** Steam transport gets
verified with a second person, and is the default from Step 11 when lobbies
and invites arrive.

## Install list

1. **Netcode for GameObjects**
   `Window > Package Manager > Unity Registry`, search "Netcode for
   GameObjects", Install. Unity Transport comes with it.

2. ~~**Facepunch transport**~~ — **REMOVED 21 Aug 2026. It does not compile.**

   ```
   FacepunchTransport.cs(288,9): error CS1028: Unexpected preprocessor directive
   ```

   One stray `#endregion` at line 288 of a 291-line file — region depth goes
   to -1 and never recovers. A genuine bug in the community package, nothing
   to do with this project.

   The risk was logged at Step 1 as "verify before Step 11 rather than
   discovering it there", and it turned up immediately instead. That is the
   better outcome: it cost a compile error on the day the plan already said
   not to depend on it, rather than a week of Step 11.

   **Removed from `manifest.json`** so the project compiles. Nothing in
   Steps 1–10 touches it.

3. **Nothing else.** No accounts, no App Id, no Steam, no money. Steam App Id
   480 (Spacewar) and a running Steam client are needed only when the
   Facepunch transport is switched on.

4. **Dissonance** is Step 10, and is the only money in this phase.

## The Steam transport question is now open, and is Step 11's

Three ways forward when the shipping transport is needed. Deciding is Step
11's job; knowing the options is today's.

**a. Patch it.** The fix is deleting one line. But `Library/PackageCache` is
regenerated from git, so the package has to be *embedded* into `Packages/`
first — which means owning a fork of it forever, including missing whatever
upstream does next. Cheap to do, annoying to keep.

**b. A different Steam transport.** Steamworks.NET-based transports exist and
are more actively maintained than this one. Same idea, same Steam relay, same
free.

**c. EOS.** The Epic fork from Part 5 arriving early. Free, no CCU limit, and
crossplay across Steam / Epic / itch — the thing that would be needed anyway
if the game ever leaves Steam.

None of these are urgent, and that is the point. **Ten steps of work sit
between here and needing any of them**, which is exactly why the plan put
them on Unity Transport.

## How every step from here is tested

Editor as **host**, a built .exe as **client**, both on this machine.

`File > Build Settings > Build` once, then re-build whenever networked code
changes. The built window joins `127.0.0.1`.

That is the "two windows, every time" rule from Part 7, and it is now a thing
one person can actually do.

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
