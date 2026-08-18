Task
Whole-branch review fixes for the Windows unblocked slice (C1, C2, I1, I2, I3).

Goal
Cryptographic PIN; real pairing window + expiry + attempt lockout; TryReadPayload must not throw; wire SessionLifecycleTracker and race-event broadcast; reconnect test; honest docs.

Status
DONE

Files changed
- `packages/protocol/SimPulse.Protocol/EnvelopeCodec.cs`
- `packages/protocol/SimPulse.Protocol.Tests/EnvelopeCodecTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingPinGenerator.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingCoordinator.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/PairingCoordinatorTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/BridgeRuntime.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/BridgeCoreTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/WebSocketTransportTests.cs`
- `docs/SECURITY.md`, `docs/KNOWN_ISSUES.md`, `docs/CURRENT_STATE.md`, `docs/BACKLOG.md`

Decisions made
- Pairing PIN via `RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6")`.
- Window is a real gate: closed until `BeginPairingWindow()`; 5-minute expiry; success closes window; 5 failed attempts lock until a new window.
- Reject reasons: `pairing_window_closed`, `too_many_attempts`, `invalid_pin`.
- `TryReadPayload` catches `JsonException` / `NotSupportedException` and returns false.
- `BridgeRuntime` observes ticks through `SessionLifecycleTracker` then broadcasts `MessageTypes.RaceEvent` to trusted clients only (no telemetry frames).

Tests executed
- `dotnet test SimPulse.sln --configuration Release` — 54 passed, 0 failed (Domain 6, Analytics 6, Protocol 7, Bridge.Core 35)

Tests passing
Yes

Known failures
None expected.

Remaining work
BRIDGE-003 mmap, BRIDGE-007 tray, TLS. Do not expand into ANALYTICS-003.

Risks
New reject reason strings are a protocol-facing contract for iOS pairing later.

Suggested next action
BRIDGE-003 iRacing mmap, feeding the already-wired tracker/broadcast path.
