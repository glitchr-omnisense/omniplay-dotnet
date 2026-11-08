# omniplay/dotnet

[omnisense/omniplay](https://github.com/glitchr-omnisense/omniplay) inside a game server, in plain
C#: verify the ticket a player presents at connection, mint tickets, send the server's heartbeats to
the application. netstandard2.1, no dependency, no UnityEngine: a .NET server, Unity (any netcode),
Godot's C#. It calls omniplay; omniplay never calls it. omniplay/mirror and omniplay/netcode run on
it.

```csharp
using Omniplay;

var omniplay = ServerEnvironment.Read();   // what omniplay/docker hands a server: secret, id, room, service, heartbeat URL
Ticket ticket = omniplay.Verify(tokenTheClientSent);   // or Tickets.Verify(token, secret, room)
if (ticket == null) { /* refuse the connection */ }
var player = ticket.Player;                // "sub", as omniplay's seating names the player

using var heartbeat = HeartbeatClient.FromEnvironment(omniplay);
await heartbeat.SendAsync(players: 5, Health.Healthy);   // every 10 s or so; Start() runs a timer for a plain .NET server
```

- `Tickets.Verify(token, secret, room, now)`: the `Ticket` (token, room, expiry, the whole payload as
  an ordered `JsonObject`) when the signature holds (the last dot, lower-case hex, constant time),
  `room` is the string served and `exp` (a number, its integer part) is not before now; null
  otherwise.
- `Tickets.Sign(JsonObject, secret)`, `SignJson(json, secret)`, `Mint(secret, room, ttl, claims)`:
  JSON written as PHP's `json_encode()` writes it (`Json.Write`), so a ticket signed here is byte for
  byte the one PHP signs.
- `HeartbeatClient`: `Token()`, `Body()`, `SendAsync(players, health, registration)` (a
  `Registration` with the server's address registers one started by hand), `Start(state, every)`
  on a thread-pool timer for a plain .NET server; `FromEnvironment()`.
- `ServerEnvironment.Read()`: `OMNIPLAY_TICKET_SECRET` (or the provider's `secret_env`),
  `OMNIPLAY_SERVER_ID`, `OMNIPLAY_ROOM`, `OMNIPLAY_SERVICE`, `OMNIPLAY_CAPACITY`, `OMNIPLAY_PORT`,
  `OMNIPLAY_ENGINE`, `OMNIPLAY_HEARTBEAT_URL`.

The format is omniplay's `docs/tickets.md`, the heartbeat its `docs/heartbeat.md`;
`Dotnet~/Tests/vectors.json` is a copy of omniplay's `Tests/vectors.json`.

## Installing

- **.NET** (a server, Godot's C#): the project `Dotnet~/Omniplay.Dotnet.csproj` (NuGet id
  `Omniplay.Dotnet`, `dotnet pack` it, or reference the project).
- **Unity**: this repository is also the UPM package `io.omniplay.dotnet` - its root `package.json`,
  the sources in `Runtime/` with the assembly `Omniplay` (no engine references). Unity ignores
  `Dotnet~/` (a folder ending in `~`), where the .NET projects, their tests and their builds live:

  ```json
  "io.omniplay.dotnet": "https://github.com/glitchr-omnisense/omniplay-dotnet.git#1.x"
  ```

  In Unity, call `SendAsync()` from the main thread (a coroutine or `InvokeRepeating`): `Start()`'s
  timer reads the state on a thread of the pool, where Unity's API may not be called.

## Tests

```sh
cd Dotnet~/Tests && dotnet test
```

On 2026-11-08, .NET SDK 10.0.403: 35 tests green - the 5 sign vectors (the JSON as PHP writes it,
the ticket from the payload and from the JSON), the 22 verify vectors (the payload key by key, or
null, as each says), the expiry's edge, minting, JSON byte for byte as PHP 8.4.27 writes a value
with slashes, accents, an emoji, controls and numbers, and heartbeats sent to a handler of the
test's own process (the body, a token that holds for `heartbeat:<server>` and not for the server's
room, the heartbeat vector, the timer's beats with the state of the moment), the environment read.
The library itself builds for netstandard2.1 with warnings as errors.

Not verified: inside Unity (the package was not imported in an editor; the asmdef and the metas
are written by hand); against an application running omniplay's `Heartbeats`.

## Licence

MIT.
