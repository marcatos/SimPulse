# BRIDGE-006 handoff

## Task

BRIDGE-006 — Pairing and trusted devices (Task 4).

## Goal

PIN pairing (six digits, not persisted), persist trusted device IDs, revoke, and keep unpaired clients from receiving telemetry. Already-trusted Hello sets `IsTrusted` without a PIN.

## Status

DONE

## Files changed

- `packages/protocol/SimPulse.Protocol/Messages.cs` — `PairingRejectMessage`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingCoordinator.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingPinGenerator.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/JsonFileTrustedDeviceStore.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Ports/Ports.cs` — `IPairingPinGenerator`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/PairingCoordinatorTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/JsonFileTrustedDeviceStoreTests.cs`
- `packages/protocol/SimPulse.Protocol.Tests/EnvelopeCodecTests.cs`
- `apps/windows-bridge/SimPulse.Bridge/Program.cs`, `Worker.cs`
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/KNOWN_ISSUES.md`

## Decisions made

- Inject `IPairingPinGenerator` so tests use a fixed PIN.
- Generate PIN at coordinator construction; log it at Information once in `BeginPairingWindow`.
- Never log PIN on accept/reject.
- Already-trusted `DeviceId` on Hello → `IsTrusted=true` without PIN.
- Wrong PIN → `PairingReject` (`invalid_pin`), not trusted.
- `JsonFileTrustedDeviceStore` when `SIMPULSE_TRUSTED_DEVICES_PATH` is set; else in-memory.
- Revoke untrusts live connections so `BroadcastToTrustedAsync` skips them.
- Transport unchanged except inbound callback → `PairingCoordinator.HandleAsync`.

## Tests executed

- Focused pairing/file tests — 10 passed
- PairingReject protocol test — 1 passed
- `dotnet test SimPulse.sln --configuration Release` — 42 passed, 0 failed (Domain 6, Analytics 6, Protocol 6, Bridge.Core 24)

## Tests passing

Yes

## Known failures

None

## Remaining work

BRIDGE-007 tray PIN UX; TLS (ADR 0003). Do not implement mmap/Apple apps here.

## Risks

HttpListener URL ACL on non-loopback binds. File store path must stay outside source control (`trusted-devices.json` is gitignored).

## Suggested next action

BRIDGE-003 iRacing mmap, or BRIDGE-007 tray UX.
