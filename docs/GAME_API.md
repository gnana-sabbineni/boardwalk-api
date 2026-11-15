# Boardwalk game API (for the frontend)

All endpoints need `Authorization: Bearer <jwt>` and return the usual envelope
`{ isSuccess, message, data, statusCode }`. Rule violations are `400` with the reason in `message`.
The game acts on the **caller's current game**, so no game id is ever sent.

## Flow
1. Lobby host calls `POST /api/lobbies/start` (existing). This now also creates the game.
2. Everyone receives `GameStarting` (existing lobby event). Navigate to the board, then
   `GET /api/games/board` (once) and `GET /api/games/current`.
3. Play. After every change the server sends **`GameUpdated`** to all players: re-fetch `GET /api/games/current`.
4. Buttons: use `data.availableActions` instead of re-implementing rules.

## Realtime events (existing lobby hub, `/hubs/lobby`)
| Event | Payload | When |
|---|---|---|
| `GameUpdated` | none | any state change, re-fetch `/games/current` |
| `DiceRolled` | `userId, die1, die2` | a roll (animate the dice) |
| `CardDrawn` | `userId, { id, title, description }` | a Fortune card (show the card modal) |
| `TradeUpdated` | none | trade proposed/accepted/declined/cancelled, re-fetch `/games/trades` |
| `GameFinished` | `winnerUserId` | last player standing; lobby is closed |

## Endpoints
| Method + path | Purpose |
|---|---|
| `GET /api/games/board` | 40 tiles: `index, name, type, colorGroup, price, houseCost, taxAmount, rentTiers[6]` |
| `GET /api/games/current` | full state (404 if not in a game) |
| `POST /api/games/roll` | `{ die1, die2, isDoubles, state }` |
| `POST /api/games/buy` / `pass` | resolve the property you landed on (`pendingPurchaseTileIndex`) |
| `POST /api/games/end-turn` | end the turn (after rolling, no pending decision/debt) |
| `POST /api/games/jail/pay` / `jail/card` | leave Detention Cove before rolling |
| `POST /api/games/debt/pay` | settle `pendingDebt` once you have the cash |
| `POST /api/games/bankruptcy` | give up; assets go to the creditor or the bank |
| `GET /api/games/players/{userId}/properties` | properties panel: groups, houses, mortgages, `canManage` |
| `POST /api/games/properties/{tile}/build` / `sell-building` / `mortgage` / `unmortgage` | own turn only |
| `GET /api/games/trades` | pending trades you proposed or received |
| `POST /api/games/trades` | body `{ recipientUserId, offerCash, requestCash, offeredTileIndexes[], requestedTileIndexes[] }` |
| `POST /api/games/trades/{id}/accept` / `decline` / `cancel` | recipient accepts/declines; proposer cancels |

`availableActions` can contain: `roll, buy, pass, endTurn, payJailFine, useJailCard, payDebt, declareBankruptcy, manageProperties, proposeTrade`.

## Board colour groups (map each key to a design-token colour)
`sand, seafoam, coral, amber, orchid, sky, rose, indigo` for streets; `station` and `utility` are not colour groups.
Tile `type` is one of `Start, Property, Station, Utility, Tax, Chance, Detention, RestStop, GoToDetention`.
Corners: 0 Departure Gate, 10 Detention Cove, 20 Rest Stop Meadow, 30 Go To Detention.

## Rules summary
Start with $1500, collect $200 passing Departure Gate. Doubles roll again; three doubles go to Detention.
Detention: pay $50, use a Harbour Pass, or roll doubles; third failed roll forces the fine.
Rent: streets use `rentTiers[houses]` (base rent doubles with a full unbuilt colour group), stations 25/50/100/200,
utilities 4x or 10x the dice. Build evenly; mortgage = half price, lift = half + 10%.
Bankruptcy: last player left wins and the lobby closes.

## Known simplifications
- No auctions: "Pass" leaves the property unowned.
- A Fortune card that makes you pay every player, if unaffordable, becomes a debt to the bank.
- "Collect from every player" takes only what each can pay.
- No turn timer or auto-handling of a disconnected player yet.
- Game locks are in-process (single API instance).

---

# UPDATE: activity log, snapshots, bots (branch feat/modular-monopoly-bots)

The shapes above are superseded where they conflict with this section.

## Activity log
Events are positional int arrays: `[sequence, turn, type, ...args]`. Seats are `TurnOrder` numbers; `-1` means the bank.
- `GET /api/games/events/schema` : the meaning of every type and its args (the contract; types are never renumbered).
- `GET /api/games/{gameId}/events?afterSequence=0&limit=200` : `{ events: int[][], nextAfterSequence, hasMore }` (participants only).
- `GameStateResponse.recentEvents` : newest first, same array format.

## Summaries (per-turn snapshots)
- `GET /api/games/{gameId}/summary` : one row per player per turn (turn 0 = start): cash, net worth, position, properties, houses, hotels, mortgaged, dice, controlledBy.

## Turn clock
`GameStateResponse.turnDeadlineAt` and `serverTime` (use the difference to count down). On timeout the server auto-plays; 2 missed turns hand the seat to a bot.

## Lobbies and bots
- `POST /api/lobbies/bots` : leader adds a computer player to the lobby.
- Leaving a lobby that is in a game is allowed: a bot takes the seat (event `SeatTakenOverByBot`, SignalR `PlayerReplacedByBot`). No rejoin yet.

## Bot service (not for browsers)
- `GET /api/bot/turns` with header `X-Bot-Key` : bot seats that must act.
- Game endpoints accept `X-Bot-Key` + `X-Act-As: <seat userId>`; only Bot-controlled seats are accepted.
