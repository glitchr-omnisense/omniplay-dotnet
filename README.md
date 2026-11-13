# omniplay/dotnet

[omnisense/omniplay](https://github.com/glitchr-omnisense/omniplay) inside a game server, in plain
C#: verify the ticket a player presents at connection, mint tickets, send the server's heartbeats to
the application. netstandard2.1, no dependency, no UnityEngine: a .NET server, Unity (any netcode),
Godot's C#. It calls omniplay; omniplay never calls it. omniplay/mirror and omniplay/netcode run on
it.

## Install and a first call

```sh
dotnet add package Omniplay.Dotnet
```

No account, no key, no network: a ticket is checked with the secret the server shares with the
application. This one was signed by omniplay's PHP `Tickets` (a recorded exchange, from omniplay's
vectors); `dotnet run --project Dotnet~/Examples/FirstCall` runs it from a checkout.

```csharp
using System;
using System.Collections.Generic;
using Omniplay;

// A ticket the PHP application signed (omnisense/omniplay's Tickets, recorded in its vectors):
var fromPhp = "eyJzdWIiOiJwbGF5ZXItNDIiLCJuYW1lIjoiQWRhIiwicm9vbSI6ImV1LTEtYTNmOSIsImV4cCI6NDEwMjQ0NDgwMH0.0bb9f63e3f6c9d7237c18855738470bae0f54410d4d1177ee87cc65c4608a57c";
var secret = "s3cr3t-shared-with-the-servers";   // OMNIPLAY_TICKET_SECRET on a real server
Console.WriteLine(Tickets.Verify(fromPhp, secret, "eu-1-a3f9")?.Player);    // player-42: the server eu-1-a3f9 lets the player in
Console.WriteLine(Tickets.Verify(fromPhp, secret, "eu-1-b7c2") == null);   // True: another server refuses it

var ticket = Tickets.Mint(secret, "eu-1-a3f9", ttl: 60, claims: new Dictionary<string, object> { ["sub"] = "player-7" });
Console.WriteLine(Tickets.Verify(ticket.Token, secret, "eu-1-a3f9")?.Player);   // player-7
```

## Inside a game server

A server that omniplay/docker (or any omniplay hosting provider) starts finds its secret, id, room,
service and heartbeat URL in its environment:

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
- `TicketGate(secret, room)`, `FromEnvironment()`: `Admit(token)` gives the ticket when it holds
  and was not admitted before - one ticket, one connection - forgetting the tokens once expired.
  omniplay/mirror and omniplay/netcode let players in through it.
- `HeartbeatClient`: `Token()`, `Body()`, `SendAsync(players, health, registration)` (a
  `Registration` with the server's address registers one started by hand), `Start(state, every)`
  on a thread-pool timer for a plain .NET server; `FromEnvironment()`.
- `ServerEnvironment.Read()`: `OMNIPLAY_TICKET_SECRET` (or the provider's `secret_env`),
  `OMNIPLAY_SERVER_ID`, `OMNIPLAY_ROOM`, `OMNIPLAY_SERVICE`, `OMNIPLAY_CAPACITY`, `OMNIPLAY_PORT`,
  `OMNIPLAY_ENGINE`, `OMNIPLAY_HEARTBEAT_URL`.

The format is omniplay's `docs/tickets.md`, the heartbeat its `docs/heartbeat.md`;
`Dotnet~/Tests/vectors.json` is a copy of omniplay's `Tests/vectors.json`.

## Installing

- **.NET** (a server, Godot's C#): `dotnet add package Omniplay.Dotnet` (NuGet), or a reference to
  the project `Dotnet~/Omniplay.Dotnet.csproj`.
- **Unity**: this repository is also the UPM package `io.omniplay.dotnet` - its root `package.json`,
  the sources in `Runtime/` with the assembly `Omniplay` (no engine references). Unity ignores
  `Dotnet~/` (a folder ending in `~`), where the .NET projects, their tests and their builds live:

  ```json
  "io.omniplay.dotnet": "https://github.com/glitchr-omnisense/omniplay-dotnet.git#v1.0.0"
  ```

  `#v1.0.0` pins the release; `#1.x` follows the branch.

  In Unity, call `SendAsync()` from the main thread (a coroutine or `InvokeRepeating`): `Start()`'s
  timer reads the state on a thread of the pool, where Unity's API may not be called.

## Tests

```sh
dotnet test Dotnet~/Tests
dotnet run --project Dotnet~/Examples/FirstCall   # player-42, True, player-7
```

On 2026-11-13, .NET SDK 10.0.404 (mcr.microsoft.com/dotnet/sdk:10.0): 38 tests green - the
README's first call (shown as it is, its results), the 5 sign vectors (the JSON as PHP writes it,
the ticket from the payload and from the JSON), the 22 verify vectors (the payload key by key, or
null, as each says), the expiry's edge, minting, JSON byte for byte as PHP 8.4.27 writes a value
with slashes, accents, an emoji, controls and numbers, and heartbeats sent to a handler of the
test's own process (the body, a token that holds for `heartbeat:<server>` and not for the server's
room, the heartbeat vector, the timer's beats with the state of the moment), the environment read,
the gate admitting a ticket once and for its room only.
The library itself builds for netstandard2.1 with warnings as errors; `dotnet pack` gives
`Omniplay.Dotnet.1.0.0.nupkg` (19 kB: the library, the README, the nuspec) and its `.snupkg`.

Not verified: inside Unity (the package was not imported in an editor; the asmdef and the metas
are written by hand); against an application running omniplay's `Heartbeats`.

## Releasing

A pushed tag `vX.Y.Z` equal to the csproj's `Version` and `package.json`'s `version` runs
`.github/workflows/release.yml`: the CI on the tag, the changelog's section checked, `dotnet pack`,
the GitHub release with the `.nupkg` and `.snupkg`, then `dotnet nuget push` to nuget.org when the
repository holds the secret `NUGET_API_KEY`. Without it, by hand from the tagged commit:

```sh
dotnet pack Dotnet~/Omniplay.Dotnet.csproj -c Release -o out
dotnet nuget push "out/*.nupkg" --api-key <key> --source https://api.nuget.org/v3/index.json
```

Unity users take the tag itself (`#vX.Y.Z`).

## Licence

MIT.
