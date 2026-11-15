# Boardwalk: design decisions, options and trade-offs

This document explains **why** the system is built the way it is. Each decision lists the realistic options, what we
chose, what that cost us, and **the conditions under which you should change your mind**. Read it as practice for
system-design discussions: the skill is not knowing the "right" answer, it is naming the trade-off and the trigger that
flips it.

> **Honest status note.** The code in this branch was written without a .NET SDK available, so it has been reviewed by
> reading but **not compiled or run**. Treat "it works" claims as "it is designed to work" until `dotnet build` and a
> bot-vs-bot test game confirm it (see decision 25).

## The system on one page

```
 Browser (humans) ──REST + SignalR──┐
                                    ▼
                       ┌────────────────────────┐        ┌───────────────┐
                       │  BoardWalk.Api (host)   │◄──────►│  PostgreSQL   │
                       │  users, friends, lobby, │        └───────────────┘
                       │  auth, controllers,     │        ┌───────────────┐
                       │  SignalR hub            │◄──────►│     Redis     │  presence + per-game lock
                       │   ┌──────────────────┐  │        └───────────────┘
                       │   │BoardWalk.Monopoly│  │
                       │   │ rules, state,    │  │
                       │   │ event log,       │  │
                       │   │ snapshots, timer │  │
                       │   └──────────────────┘  │
                       └────────────▲───────────┘
                                    │ same public REST API (X-Bot-Key + X-Act-As)
                           ┌────────┴────────┐
                           │ BoardWalk.Bot   │  separate process, plays bot-controlled seats
                           └─────────────────┘
```

Projects in the one solution: `BoardWalk.Api` (host), `BoardWalk.Monopoly` (the game), `BoardWalk.Monopoly.Contracts`
(shared data shapes), `BoardWalk.Bot` (computer player).

---

## A. Structure and boundaries

### 1. One deployable with separate projects (modular monolith)

| Option | For | Against |
|---|---|---|
| **A. Everything in one project** | Fastest to write | Boundaries exist only by discipline; every new game tangles further |
| **B. Several projects, one deployable (chosen)** | Compiler-enforced boundaries; one deploy; one transaction; cheap calls | Still scales as one unit; a bad module can still crash the process |
| **C. Microservices (game as its own service)** | Independent scaling and deploys; fault isolation | Network calls, distributed transactions, versioning, ops cost, far more to build |

**Why B:** the pain B solves (tangling) is real today; the pain C solves (independent scaling, many teams) is not.
**Accepted cost:** one process shares CPU/memory; a game bug can slow lobbies.
**Change it when:** game traffic needs different scaling than the lobby (e.g. thousands of concurrent games but few lobbies),
a different team owns games, or you need independent release cycles. Because the boundary is already an interface, extracting
`BoardWalk.Monopoly` into a service later means replacing in-process calls with HTTP/gRPC, not rewriting rules.

### 2. The game knows nothing about the host (ports and adapters)

The game module defines small interfaces (`IGameRealtimeNotifier`, `IGameCompletionHandler`, `IGameActorContext`, `IGameLock`,
`IDiceRoller`); the host implements them. The game never references users, lobbies, SignalR or HTTP.

| Option | For | Against |
|---|---|---|
| **A. Game references host types** (User, Lobby entities) | Less code up front | Cannot reuse or test alone; second game copies the coupling |
| **B. Interfaces the host implements (chosen)** | Game is testable alone; a second game can reuse the same host | More indirection; you must design the interfaces well |

**Accepted cost:** extra interfaces and a small amount of mapping (host user -> `PlayerSeat`; names are copied into the game at start).
**Change it when:** the interfaces start leaking host concepts (a sign the boundary is in the wrong place), or you have only ever one game and
the indirection costs more than it saves (then collapse it).

### 3. One database, one DbContext shared by modules; no foreign keys across the boundary

| Option | For | Against |
|---|---|---|
| **A. One DbContext, modules map their tables into it (chosen)** | One transaction across lobby + game (starting a game is atomic); one migration history | Modules share a schema/lifecycle; a bad migration affects both |
| **B. One DbContext per module, same database** | Cleaner ownership; separate migrations | Cross-module changes are two commits, so you need compensation/outbox |
| **C. Database per service** | Full independence | Distributed transactions or eventual consistency everywhere |

Cross-module ids (`UserId`, `RoomId`) are plain columns **without foreign keys**, so the game never depends on host table design.
**Accepted cost:** the database will not stop you referencing a deleted user; the application must.
**Change it when:** modules need separate release cadence or separate scaling of storage -> move to B, and replace "same transaction"
with the outbox pattern (decision 19).

### 4. A Contracts project for shared data shapes

| Option | For | Against |
|---|---|---|
| **A. Duplicate DTOs in each project** | No coupling | They drift; bugs show up at runtime |
| **B. Shared Contracts project (chosen)** | One definition; compile errors instead of runtime errors; the bot gets the same types | Every consumer must be .NET (or you generate clients); changes ripple |
| **C. Generate clients from OpenAPI** | Works for any language; versioned contract | Generation tooling and a build step |

**Accepted cost:** .NET-only consumers. **Change it when:** the frontend or a non-.NET bot needs typed access -> publish OpenAPI and generate
(the frontend already needs this; the TypeScript types should come from OpenAPI, not hand-written).

---

## B. API design

### 5. The server is the single source of truth; clients send intents, not state

Clients say "roll", "buy", "build on tile 9" and the server decides. The response includes `availableActions` so the UI does not re-implement rules.

| Option | For | Against |
|---|---|---|
| **A. Client computes, server stores** | Snappy UI | Anyone can cheat; rules duplicated in every client |
| **B. Server-authoritative (chosen)** | Cheating impossible; one rules implementation (humans and bots identical) | A round trip per action; server holds all logic |

**Accepted cost:** latency per click. **Change it when:** you need optimistic UI for latency (do it in the client, but still reconcile with the server) or
offline play (then you need conflict resolution - a different, much harder problem).

### 6. Endpoint style: intent-specific REST on "my current game"

`POST /api/games/roll`, `/buy`, `/trades/{id}/accept`, `GET /api/games/current`. History endpoints take a `gameId` because they also serve finished games.

| Option | For | Against |
|---|---|---|
| **A. One generic `POST /command {type, payload}`** | One endpoint, easy to extend | Weak typing, poor docs/caching/rate-limit per action, everything is a POST to one URL |
| **B. Resource/intent endpoints (chosen)** | Self-documenting, per-action auth and throttling, typed bodies | More endpoints |
| **C. GraphQL / RPC-over-WebSocket** | Flexible reads; no round trip overhead for sockets | New tooling, harder caching/auth, more to learn than it earns here |

"Current game" (no id in the URL) is convenient but assumes **one active game per user**. **Change it when:** a user can be in several games at once ->
move the id into the path (`/games/{id}/roll`); the service already takes the user id and could take a game id.

### 7. Realtime: thin "something changed" events, then re-fetch

The server sends `GameUpdated` (no payload) and the client calls `GET /games/current`. Rich events (`DiceRolled`, `CardDrawn`) exist only for animation.

| Option | For | Against |
|---|---|---|
| **A. Push full state** | One message, no re-fetch | State differs per player (`availableActions`), so you must send N personalised copies |
| **B. Push diffs** | Smallest messages | Clients must apply diffs correctly; a missed message corrupts state until a resync |
| **C. Thin notify + re-fetch (chosen)** | Simple; self-healing (a missed event is fixed by the next fetch); per-player views stay server-side | Extra request per event; a burst of events causes a burst of fetches |

**Change it when:** profiling shows the re-fetch load matters (many viewers, big states) -> add diffs with sequence numbers and fall back to re-fetch on a gap.
We reuse the existing lobby hub/group (the room id *is* the lobby id) instead of a second hub to avoid a second connection and group-management code;
**change that when** a second game with different realtime needs arrives.

### 7b. Wire format of the activity log: compact positional arrays

Events travel as `[sequence, turn, type, ...args]` and are stored as `type` + `integer[]`. Seats (small numbers) replace user GUIDs. A schema endpoint
(`GET /api/games/events/schema`) says what each position means.

| Option | For | Against |
|---|---|---|
| **A. Text sentences** | Trivial to display | Not machine-readable, not translatable, large |
| **B. JSON objects per event** | Self-describing, easy to evolve | ~5-10x larger; repeated key names; slower to parse |
| **C. Positional int arrays + schema registry (chosen)** | Smallest and fastest to scan; trivial for a bot; language-neutral | Meaning lives outside the data; a wrong position is a silent bug |

**Guardrails that make C safe:** the type enum is append-only (never renumber), args are append-only per type, the schema endpoint is the contract, and
a single formatter on the client turns events into sentences.
**Change it when:** events need many optional fields or nested data (e.g. full trade contents) -> switch that event type to a JSON payload column; keep arrays for the hot, fixed-shape events.

### 8. History uses cursor pagination

`GET /games/{id}/events?afterSequence=120&limit=200` returns a page and `nextAfterSequence`.

| Option | For | Against |
|---|---|---|
| **A. Offset/limit** | Familiar | Slow on big tables; duplicates/gaps when rows are inserted mid-read |
| **B. Cursor on a monotonic sequence (chosen)** | Stable while the log grows; uses an index (`GameId, Sequence`); lets a client "tail" the log | Cannot jump to "page 7" |

**Change it when:** you need random access by page number (an admin table) -> add offset for that view only.

### 9. Not done yet: stale-action protection and idempotency

If a bot (or a flaky client) retries a request, or acts on an old view, the server currently relies on **rules** to reject it ("not your turn", "already rolled").
Most retries are rejected harmlessly, but not all (a double `mortgage` request on different tiles would both succeed).
Options: a state **version** the client echoes back (reject if stale), or an **Idempotency-Key** header (replay the stored response). **Add it when:** you
see duplicate effects in logs, or when the bot runs on an unreliable network. It is the first thing a reviewer will ask about.

---

## C. Data and correctness

### 10. Concurrency: a per-game lock (Redis, with a safe release)

Every mutation takes `lock:game:{id}` -> loads state -> validates -> mutates -> saves -> releases.

| Option | For | Against |
|---|---|---|
| **A. In-memory lock** | Trivial | Works on one server only |
| **B. Optimistic concurrency (version column, retry)** | No lock; scales | Players see "conflict, try again" errors; every action needs retry logic |
| **C. Database row lock (`SELECT ... FOR UPDATE`)** | Uses the DB you already have | Holds a DB connection/transaction during the whole action; awkward with EF |
| **D. Redis lock `SET NX PX` + token + compare-and-delete (chosen)** | Works across servers; auto-expires if a server dies | New dependency in the hot path; TTL tuning; not perfectly safe if a holder stalls longer than the TTL |
| **E. Single-writer / actor per game** | No locks at all | A routing layer so each game always lands on one worker; real infrastructure |

Games are low-contention (one active player at a time), so a coarse lock is cheap and the simplest *correct* answer. The token prevents deleting someone else's lock;
the TTL prevents a crashed server leaving a game stuck forever. The in-memory lock stays as the default for tests/single server.
**Known weakness:** if an action takes longer than the 15 s TTL, two writers could overlap. **Change it when:** you need stronger guarantees (use a fencing token stored with the
game, or optimistic concurrency *in addition*), or throughput per game gets high (actors).

### 11. Event log: append-only with a per-game sequence

Events are never edited. Each has `Sequence` (1,2,3...) unique per game. The log is the **audit trail and the replay source**, but the current state is stored directly (not rebuilt from events).

| Option | For | Against |
|---|---|---|
| **A. State only** | Simple | No history, no activity feed, no debugging |
| **B. Full event sourcing (state = replay of events)** | Perfect audit; time travel | Every rule change risks replaying old games differently; snapshots become mandatory; much harder |
| **C. State + append-only log (chosen)** | History and feed for little cost; state reads stay trivial | Two things to keep consistent (we write both in one save, so they cannot diverge) |

**Change it when:** you need exact replays of old games after rule changes (then version the rules and move toward B).

### 12. Snapshots per turn (and why both log and snapshots)

At each turn end (and at start and finish) we store one row per player: cash, net worth, properties, houses, position, who controls the seat, the dice, and the log position.

| Option | For | Against |
|---|---|---|
| **A. Replay the log to answer "turn 12?"** | No extra storage | Slow, and needs every event to carry every change |
| **B. Snapshot per turn (chosen)** | One cheap indexed read; powers the net-worth chart and "state at turn N"; a bot taking over reads current state directly | Extra writes and rows (players x turns, a few thousand rows per game) |
| **C. Snapshot every N events/turns** | Less storage | Answers between snapshots need replay again |

**Accepted cost:** denormalised data (net worth is computed once and stored). **Change it when:** storage or write volume matters (snapshot every 5th turn; or archive finished games to cold storage and
drop snapshots after summarising), or when rules change the definition of net worth (re-derive from the log, which is why we keep both).

### 13. Dice are a pure function of (seed, counter)

`roll = f(game seed, roll number)`. The seed is stored on the game and **never sent to clients** (it would reveal future rolls). Dice are injected (`IDiceRoller`).

| Option | For | Against |
|---|---|---|
| **A. `Random` on every call** | Simplest | Not reproducible; hard to test; cannot replay a game |
| **B. Store each roll only** | Reproducible after the fact | Cannot pre-script a test game |
| **C. Seed + counter, injectable (chosen)** | Reproducible both ways; tests can inject fixed dice; simulations are deterministic | The seed is a secret to protect; a custom mixer to maintain (we use SplitMix64, stable across .NET versions, unlike `System.Random`) |

**Change it when:** real-money or high-trust play needs unpredictable, auditable randomness -> use a CSPRNG and publish a commit-reveal proof; a seeded generator is *predictable if the seed leaks*.

### 14. Moving data between modules: same transaction vs eventual consistency

When a game ends, the host must close the lobby and free the players. We run the host's cleanup (`IGameCompletionHandler`) **inside the game's save**, in one DbContext, so the final game state and
the lobby cleanup commit together or not at all.

| Option | For | Against |
|---|---|---|
| **A. Same transaction (chosen)** | No window where the game is over but the lobby is open | Couples modules at the database level (decision 3) |
| **B. Domain events + outbox table + worker** | Modules fully independent; reliable | A table, a worker, idempotent handlers, eventual (seconds-late) consistency |
| **C. Fire and forget** | Simplest | Lost messages leave players stuck "in a lobby" forever |

**Change it when:** the game moves to its own service/database -> B (outbox) is the standard way to avoid distributed transactions.

**A place where we chose eventual consistency on purpose:** when someone *leaves mid-game*, the lobby change commits first, then the game takeover. If the second step fails, the **turn timer heals it**
(it auto-plays missed turns and then gives the seat to a bot). That is a small example of designing for failure: make the bad state self-correcting instead of preventing every failure.

### 15. The timer's database scope (a subtle failure mode)

The turn timer handles each overdue game in its **own DI scope (its own DbContext)**. If one game's handling throws halfway, its half-changed objects stay tracked in that context; reusing it for the next game would
save that garbage. A fresh scope per unit of work is the general rule for background jobs. **Change it when:** never relax this; if you move to a job queue, the same rule becomes "one transaction per job".

---

## D. Computer players

### 16. The bot is a separate process using the public API

| Option | For | Against |
|---|---|---|
| **A. Bot logic inside the API process** | No network, no auth problem | Cannot scale or deploy separately; bot bugs/CPU hit humans; tempting to bypass rules |
| **B. Separate service using the same REST API (chosen)** | Cannot cheat (same rules as humans); proves the API is complete; scales and deploys independently; can be replaced by a smarter bot | Needs auth for delegation, a way to find work, extra latency |
| **C. Bots as real users who log in and connect over SignalR** | Looks exactly like a human client | Needs passwords/tokens per bot, many connections, no clean "take over a human's seat" story |

**Change it when:** bot strategies become heavy (search/ML) -> keep B, scale workers horizontally, and give them a read-optimised endpoint. If latency to the API dominates simulation, run simulations against the engine directly (it is a pure library) rather than through HTTP.

### 17. Seat control: `ControlledBy = Human | Bot`

A seat is "who decides this player's moves". A human who leaves, or misses 2 turns in a row, hands the seat to a bot; **for now the human cannot come back**.

| Option | For | Against |
|---|---|---|
| **A. Eject the player (remove their pieces)** | Simple | Destroys the game for others |
| **B. Forfeit (bankruptcy)** | Fair and quick | Still ends someone's position abruptly |
| **C. Hand the seat to a bot (chosen)** | Game continues; others are told | Needs a bot service; "reclaim" needs identity rules later |

**Accepted cost:** an unfair bot could harm/benefit the human's old position (keep it dumb and neutral). **Change it when:** you add reclaim -> need a rule for *when* (their next turn only?) and a handover event; when
you add ranked play -> a leave should probably count as a loss.

### 18. How bots authenticate and what they may do

The bot sends a shared secret (`X-Bot-Key`) and names the seat (`X-Act-As`). The key only proves "this is the bot service". **The game decides which seats are allowed**: it only finds seats whose control is `Bot`, so a leaked key
still cannot move a human's pieces. Comparison is constant-time.

| Option | For | Against |
|---|---|---|
| **A. Shared secret header (chosen)** | Trivial; fine inside a private network | One key for everything; rotation is manual; a leak lets an attacker play bot seats |
| **B. Service JWT with per-seat delegation claims** | Short-lived, scoped, auditable | A token issuer and refresh logic |
| **C. mTLS between services** | Strong, no secrets in headers | Certificate management |

**Change it when:** the bot runs outside your network or you have several services -> B or C; at minimum add key rotation (accept two keys at once).

### 19. How the bot finds work: polling

The bot calls `GET /api/bot/turns` about once a second; the API returns the bot-controlled seats that must act (their turn, or a pending trade offer).

| Option | For | Against |
|---|---|---|
| **A. Polling (chosen)** | Stateless, trivial, survives restarts/network blips, no missed events | Up to ~1 s latency; wasted requests when idle; load grows with the number of bots |
| **B. SignalR push to the bot** | Instant | Connection management, missed events while disconnected |
| **C. Queue / Redis Stream** (API enqueues "seat X must act") | Scales to many workers; exactly-once-ish with consumer groups | A queue and ack/retry logic; duplicate/ordering handling |

**Change it when:** many bots/games make the poll expensive (> a few hundred polls/second) -> C, with the turn timer as a safety net. Note the polling endpoint scans all games with a bot seat; at scale that needs an indexed "needs bot" flag.

### 20. Bot accounts are real rows in the Users table, seeded at startup

| Option | For | Against |
|---|---|---|
| **A. Separate Bots table** | Clean separation | Every join/feature (lobbies, friends) must handle two kinds |
| **B. Virtual players with no user row** | No pollution of Users | The lobby and game need special cases everywhere |
| **C. Real user rows, `IsBot = true` (chosen)** | Lobbies and game seats treat them uniformly | Must exclude bots from login, search and invites (done); the table mixes kinds |

Seeding happens in code on startup and is **idempotent** (inserts only what is missing). Alternatives: seed in a migration (versioned, but EF's seeding with the concurrency column is awkward) or a manual SQL script (not repeatable).
**Change it when:** bots need their own attributes (skill level, personality) -> a `BotProfile` table keyed by user id.

### 21. The "dumb" strategy, and testing with bots

Rules in order: decline trades; pay debts (mortgage the cheapest property if short, else bankrupt); buy if affordable; if it owns another street of that colour, mortgage other properties to afford it; otherwise pass; roll; end turn.
It never builds or trades. **Why dumb first:** a trivial, predictable bot lets you test the whole pipeline (delegation, polling, takeover) before any intelligence is added.
**Change it when:** you want it to be fun to play -> add building, value-based trade evaluation, and risk limits (keep a cash reserve). **Use bots as test tools:** bot-vs-bot games with fixed dice (decision 13) will find stuck states and infinite games faster than hand tests.

---

## E. Time, failure and scope

### 22. Server-side turn clock

Each game stores `TurnDeadlineAt`; a background service finds overdue games and plays them out; the UI shows a countdown from the same value (plus the server time, to correct for a wrong browser clock).

| Option | For | Against |
|---|---|---|
| **A. Client-side timer** | Trivial | Cheatable and unreliable (closed tab = no timer) |
| **B. Server deadline + background poller (chosen)** | Authoritative; survives restarts; works for any number of servers | A scan every second; up to ~1 s late |
| **C. Per-game scheduled jobs (Quartz/Hangfire)** | Precise; no scan | A scheduler and its storage; cancel/reschedule on every action |
| **D. Redis sorted set of deadlines / keyspace notifications** | Efficient at scale | More Redis logic; notifications are not guaranteed |

Several servers can run the poller at once: the per-game lock plus a **re-check of the deadline under the lock** mean only one acts, and a player who acted a moment ago is not penalised.
**Timeout behaviour:** roll if needed, pass on purchases, end the turn; unaffordable debt -> bankruptcy (deliberately harsh and simple). **Two misses in a row -> the seat becomes a bot.**
**Change it when:** many concurrent games make the scan costly (the index on `Status, TurnDeadlineAt` helps; beyond that use D), or when you want gentler timeout rules (auto-mortgage before bankruptcy).

### 23. Scope cuts (deliberate, and when to revisit)

- **No auctions** ("Pass" leaves the property unowned). Add them when games stall or feel unfair; they need a timed bidding phase, so they also stress concurrency and the clock.
- **A card that makes you pay every player becomes a debt to the bank if unaffordable**, and "collect from everyone" takes what each can pay. Fix when you want exact rules.
- **No mid-game rejoin.** Add with a reclaim rule (decision 17).
- **Single region, single Redis, single Postgres.** Fine for learning; a real deployment adds replicas/failover.

### 24. Security model

Every action is authorised by *seat ownership + turn + game state*, enforced in the engine (not the controller). History is readable only by participants. There is no hidden information in this game (all state is public), which removes a whole class of leaks; the **dice seed** is the one secret.
**Not done (add before real users):** rate limiting per user and per bot key, request size limits, audit of failed bot-key attempts, and CORS tightening (currently one localhost origin).

### 25. Testing and observability (the gaps)

None of this is verified by running code yet. **First steps:** `dotnet build`, then EF migration, then (a) unit tests with fixed dice for rent, jail and bankruptcy, (b) a bot-vs-bot soak test that plays hundreds of seeded games and asserts invariants (cash never negative, every property has at most one owner, the game ends).
For observability: log the `gameId` and sequence in every line, and export metrics for lock wait time, timer lag (now - deadline), actions per second and bot poll latency. **Change what you measure** when you see where it hurts.

---

## Quick reference: "when would I change this?"

| Decision | Flip it when... |
|---|---|
| Modular monolith (1) | Games need independent scaling or release cycles |
| Shared DbContext (3) | Modules move to separate databases or services |
| Thin events (7) | Re-fetch load becomes the bottleneck |
| Int-array events (7b) | Events need rich or nested data |
| Redis lock (10) | You need fencing/stronger guarantees, or per-game throughput is high |
| Event log + state (11) | You need exact historical replays after rule changes |
| Snapshots (12) | Storage/write cost bites, or the net-worth definition changes |
| Seeded dice (13) | Randomness must be unpredictable and auditable |
| Same-transaction cleanup (14) | The game becomes its own service (use an outbox) |
| Bot via public API (16) | Strategies get heavy (scale workers or simulate in-process) |
| Shared-secret auth (18) | The bot leaves your private network |
| Polling (19) | Many bots/games make polling expensive |
| Poller-based clock (22) | Many concurrent games (use Redis deadlines or a scheduler) |

## Interview angles this project exercises

Modularity vs microservices; server-authoritative design and why; distributed locking and its failure modes (TTL, fencing, token release); idempotency and retries; designing for failure with self-healing paths;
transaction boundaries and the outbox pattern; event logs vs event sourcing; read models/snapshots; cursor pagination; polling vs push vs queues; service-to-service auth; background job scoping; deterministic testing.
