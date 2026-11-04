# 🐺 Werewolf

A real-time multiplayer take on the classic social deduction game, built to showcase **ASP.NET Core SignalR** with an **Angular** client.

Players join a room from their phones, get a secret role, and survive alternating night and day phases. All of it is synchronized live by the server.

> 🎬 _Demo GIF coming soon_

---

## What this project demonstrates

| SignalR concept | Where it's used |
|---|---|
| **Groups** | One group per room, plus a private **wolf-only** group for the night chat |
| **Per-connection messages** | Each player receives **only their own role**; Seer results go only to the Seer |
| **Strongly typed hubs** | `Hub<IGameClient>`, so server-to-client calls are compile-checked instead of magic strings |
| **`IHubContext` outside the hub** | `GameService` pushes updates from a background timer, not just in response to client calls |
| **Server-driven timers** | A `BackgroundService` + `PeriodicTimer` advances phases, so the game never depends on a client being online |
| **Server-side identity** | The server maps each connection to a player; clients can't act as someone else by sending a different ID |
| **Error handling** | Engine rule violations become `HubException`s with readable messages shown in the UI |
| **Thread safety** | One `SemaphoreSlim` per room serializes all game mutations |

**Hidden information is enforced on the server.** The client never receives anyone else's role until the game ends, so nobody can open DevTools and find the wolves.

---

## Architecture

```
Werewolf/
├── src/
│   ├── Werewolf.Engine/        # Pure C# game rules — no ASP.NET or SignalR dependency
│   └── Werewolf.Api/
│       ├── Hubs/               # GameHub (thin entry point) + IGameClient
│       └── Rooms/              # RoomManager, GameService, PhaseTicker, DTOs
├── tests/
│   └── Werewolf.Engine.Tests/  # xUnit tests for game rules
└── werewolf-client/            # Angular + Tailwind client
```

- **Engine:** roles, phases, night resolution, voting, and win conditions as plain, unit-tested C#.
- **Hub:** works out who is calling, locks the room, and delegates. No game logic lives here.
- **GameService:** orchestrates phase transitions, private messages, and bot players.
- **Client:** Angular with signals for state, and `@microsoft/signalr` for the connection.

---

## Rules

| Role | Night action |
|---|---|
| 🐺 **Werewolf** | Chooses a victim together with the pack (private wolf chat) |
| 🔮 **Seer** | Inspects one player to learn whether they're a wolf |
| 💉 **Doctor** | Protects one player, possibly themselves, from being killed |
| 🏡 **Villager** | No night action. Survive and vote wisely. |

- **Night (20s):** special roles act in secret.
- **Day (20s):** the death is revealed and living players discuss in the town chat.
- **Voting (15s):** a live tally; the player with the most votes is eliminated.
- **Ties** mean nobody dies (both at night and in the vote).
- **The village wins** when all wolves are dead. **The wolves win** once they equal or outnumber everyone else.

**Bots** can fill empty seats, so the game can be tried solo. They act and vote randomly.

---

## Tech stack

- **Backend:** .NET 10, ASP.NET Core, SignalR
- **Frontend:** Angular, Tailwind CSS, `@microsoft/signalr`
- **Testing:** xUnit
- **State:** in-memory (games are short-lived; no database needed)

---

## Running locally

### Prerequisites
- .NET 10 SDK
- Node.js + Angular CLI

### 1. Backend
```bash
dotnet test
dotnet run --project src/Werewolf.Api
```
Note the `http://localhost:<port>` address it prints.

### 2. Frontend (dev mode)
Set the port in `werewolf-client/proxy.conf.json`, then:
```bash
cd werewolf-client
npm install
ng serve --proxy-config proxy.conf.json
```
Open `http://localhost:4200`.

### 3. Try it
1. Create a room.
2. Open a **second tab** and join using the room code.
3. Back in the first tab, add bots until there are at least 5 players, then start.
4. Each tab receives its own secret role, and every update appears live in both.

---

## Playing with friends (temporary public link)

Build the client into the API so everything runs from one address:
```bash
cd werewolf-client
ng build
mkdir -p ../src/Werewolf.Api/wwwroot
cp -R dist/werewolf-client/browser/* ../src/Werewolf.Api/wwwroot/
cd ..
dotnet run --project src/Werewolf.Api
```

Then expose it with a free Cloudflare quick tunnel:
```bash
cloudflared tunnel --url http://localhost:<port>
```
Share the printed `https://….trycloudflare.com` link. It works as long as the API and the tunnel are both running.

---

## Roadmap

- [ ] Reconnection: rejoin the same seat after a phone sleeps
- [ ] Ghost chat for eliminated players
- [ ] Bots that say a few lines in the town chat
- [ ] Docker image + free hosting on Render
- [ ] GitHub Actions CI (build + test on every push)
- [ ] Scale-out notes: Redis backplane / Azure SignalR Service