# Changelog

## [Unreleased]

## [1.0.0] - 2026-11-13

The first release (the 0.1.0 prepared before was never published). One version for both forms:
NuGet `Omniplay.Dotnet` 1.0.0 and UPM `io.omniplay.dotnet` 1.0.0.

### Added
- `Omniplay.Dotnet` (NuGet) and `io.omniplay.dotnet` (UPM), assembly `Omniplay`: `Tickets` (Verify,
  Sign, SignJson, Mint), `Ticket`, `TicketGate` (one ticket, one connection), `Json` and `JsonObject`
  (JSON read with its order kept, written as PHP writes it), `HeartbeatClient`, `Registration`,
  `HeartbeatAnswer`, `Health`, `ServerEnvironment`. Checked against omnisense/omniplay 1.0.0's
  vectors (5 signed, 22 verified).
- `Dotnet~/Examples/FirstCall`, the README's first call (a ticket PHP signed, verified; one minted),
  checked by the tests and run by the CI.
- The NuGet package carries the README, a symbols package (`.snupkg`), release notes and a
  copyright; the UPM manifest its documentation, changelog and licence URLs.
- CI (tests, the first call, `dotnet pack`, the UPM metas and the two versions kept equal) and a
  release workflow on a tag (GitHub release, `dotnet nuget push` when the repository holds
  `NUGET_API_KEY`).
