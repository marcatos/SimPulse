# BRIDGE-005 handoff

## Task

BRIDGE-005 — WebSocket server (Task 3: listen/accept only).

## Goal

Accept WebSocket clients on `http://{host}:{port}/ws/` (default `127.0.0.1:8742`), ignore unknown envelope types without disconnecting, and keep `BroadcastToTrustedAsync` from sending to untrusted connections. Pairing is Task 4 — all connections stay untrusted.

## Status

DONE (pairing gate completed in BRIDGE-006)

## Files changed

- `apps/windows-bridge/SimPulse.Bridge.Core/Ports/ClientTransportPorts.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/ClientSessionHub.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/HttpListenerWebSocketTransport.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/WebSocketClientConnection.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/WebSocketMessagePump.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/WebSocketTransportTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/LinkedSiblingTasksTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/LinkedSiblingTasks.cs`
- `apps/windows-bridge/SimPulse.Bridge/Program.cs`, `Worker.cs`
- `.env.example` (default host `127.0.0.1`; `0.0.0.0` opt-in)
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/KNOWN_ISSUES.md`

## Decisions made

- Default bind `127.0.0.1`; `0.0.0.0` / `*` / `+` maps to `http://+:{port}/ws/`.
- Tests pick an ephemeral port with `TcpListener` then pass it into the transport.
- Pairing does not exist yet — connections remain untrusted so broadcast is a no-op until Task 4.
- Worker runs runtime and transport via `LinkedSiblingTasks` (WhenAny + linked CTS cancel + await both). A listen fault does not wait on idle runtime.
- Accepted sockets are closed (`CloseOutputAsync`) then disposed, including `SemaphoreSlim`.
- Do not mark BRIDGE-005 DONE (pairing AC remains).

## Tests executed

- `dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests/SimPulse.Bridge.Core.Tests.csproj --configuration Release` — 14 passed
- `dotnet test SimPulse.sln --configuration Release` — 31 passed

## Tests passing

Yes

## Known failures

None

## Remaining work

Task 4: pairing PIN, persist device id, set `IsTrusted`, reject unpaired telemetry.

## Risks

HttpListener URL ACL on some Windows machines; loopback prefixes usually work without reservation.

## Suggested next action

Implement BRIDGE-006 pairing and wire trust onto `IClientConnection`.
