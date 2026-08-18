# Windows Unblocked Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Windows-executable backlog items that do not need Xcode: RaceReport analytics model, Bridge session/lap lifecycle idempotency, WebSocket transport, and PIN pairing with a durable trusted-device store.

**Architecture:** Hexagonal Bridge stays in `SimPulse.Bridge.Core` (ports + application + adapters). Analytics stays pure in `packages/analytics` and only references `SimPulse.Domain`. Protocol wire types stay in `packages/protocol`. No HealthKit, no Android Gradle, no iRacing mmap in this plan.

**Tech Stack:** .NET 8, xUnit, `System.Net.HttpListener` WebSockets, `System.Text.Json`, Microsoft.Extensions.Logging, Conventional Commits.

## Global Constraints

- Conventional Commits: `feat|fix|refactor|test|docs|chore(<scope>): …`
- Hexagonal: domain/analytics/protocol have no Bridge or Hosting references; Bridge adapters implement ports; application orchestrates.
- `OptionalValue<T>` / `DataPresence` for missing fields — never invent zeros for measurements.
- Do not log heart-rate sample payloads, pairing PINs after accept, or secrets.
- Logging: INFO default via `SIMPULSE_LOG_LEVEL`; log start, major steps, end, durations.
- Tests: TDD (RED → GREEN); `dotnet test SimPulse.sln --configuration Release` must pass on Windows before each commit.
- Files: prefer ≤300 lines/file, ≤60 lines/method; split rather than grow blobs.
- Do not implement WATCH-*, IOS-*, AND-*, WEAROS-*, or BRIDGE-003 (iRacing mmap) in this plan.
- Update `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, and `docs/KNOWN_ISSUES.md` when a backlog ID’s acceptance criteria are met.
- Work only under the assigned worktree path; do not edit the parent checkout.
- Personal GitHub identity already set by `includeIf`; never change `git config`.

---

## File map

| Path | Responsibility |
| --- | --- |
| `packages/analytics/SimPulse.Analytics/RaceReport.cs` | RaceReport record |
| `packages/analytics/SimPulse.Analytics/RaceReportBuilder.cs` | Build report from `DriverSession` |
| `packages/analytics/SimPulse.Analytics.Tests/RaceReportBuilderTests.cs` | Analytics tests |
| `apps/windows-bridge/SimPulse.Bridge.Core/Application/SessionLifecycleTracker.cs` | Idempotent session/lap events |
| `apps/windows-bridge/SimPulse.Bridge.Core.Tests/SessionLifecycleTrackerTests.cs` | Lifecycle tests |
| `apps/windows-bridge/SimPulse.Bridge.Core/Ports/ClientTransportPorts.cs` | Transport + connection ports |
| `apps/windows-bridge/SimPulse.Bridge.Core/Application/ClientSessionHub.cs` | Fan-out to paired connections |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/HttpListenerWebSocketTransport.cs` | WebSocket listener adapter |
| `apps/windows-bridge/SimPulse.Bridge.Core.Tests/WebSocketTransportTests.cs` | Loopback WS tests |
| `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingCoordinator.cs` | PIN pairing + trust gate |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/JsonFileTrustedDeviceStore.cs` | Durable trusted devices |
| `packages/protocol/SimPulse.Protocol/Messages.cs` | Add `PairingRejectMessage` |
| `apps/windows-bridge/SimPulse.Bridge/Program.cs` / `Worker.cs` | Wire host |

---

### Task 1: ANALYTICS-002 RaceReport model

**Backlog:** ANALYTICS-002  
**Files:**
- Create: `packages/analytics/SimPulse.Analytics/RaceReport.cs`
- Create: `packages/analytics/SimPulse.Analytics/RaceReportBuilder.cs`
- Create: `packages/analytics/SimPulse.Analytics.Tests/RaceReportBuilderTests.cs`
- Modify: `docs/BACKLOG.md` (ANALYTICS-002 → DONE)
- Modify: `docs/CURRENT_STATE.md` (note RaceReport)

**Interfaces:**
- Consumes: `DriverSession`, `WorkoutSession`, `SimulatorSession`, `HeartRateMetrics`, `EnergyMetrics`, `OptionalValue<T>`, `DataPresence`, `RaceEventType`
- Produces: `RaceReport` record; `RaceReportBuilder.FromDriverSession(DriverSession session)`

- [ ] **Step 1: Write the failing tests**

```csharp
using SimPulse.Domain;

namespace SimPulse.Analytics.Tests;

public sealed class RaceReportBuilderTests
{
    [Fact]
    public void Workout_only_session_marks_simulator_fields_unavailable()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        WorkoutSession workout = new(
            SessionId.New(),
            new TimestampInstant(start, ClockSource.WorkoutSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(20), ClockSource.WorkoutSession)),
            [
                new HeartRateSample(new TimestampInstant(start, ClockSource.WorkoutSession), 100),
                new HeartRateSample(new TimestampInstant(start.AddMinutes(10), ClockSource.WorkoutSession), 140)
            ],
            [new EnergySample(new TimestampInstant(start.AddMinutes(20), ClockSource.WorkoutSession), 22.0)],
            OptionalValue<int>.Unavailable(),
            OptionalValue<int>.Unavailable(),
            OptionalValue<double>.Unavailable());

        DriverSession session = new(
            workout.Id,
            workout,
            OptionalValue<SimulatorSession>.Unavailable(),
            OptionalValue<TimeSpan>.Unavailable());

        RaceReport report = RaceReportBuilder.FromDriverSession(session);

        Assert.Equal(DataPresence.Unavailable, report.SimulatorDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, report.TrackDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, report.VehicleDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, report.PeakHeartRateAssociatedEvent.Presence);
        Assert.True(report.AverageHeartRateBpm.TryGet(out double avg));
        Assert.Equal(120, avg, 0);
        Assert.True(report.MaximumHeartRateBpm.TryGet(out int max));
        Assert.Equal(140, max);
        Assert.True(report.ActiveKilocalories.TryGet(out double kcal));
        Assert.Equal(22.0, kcal);
        Assert.True(report.Duration.TryGet(out TimeSpan duration));
        Assert.Equal(TimeSpan.FromMinutes(20), duration);
        Assert.DoesNotContain("stress", MeasurementWording.HeartRateChangePercent(100, 140), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Without_timeline_offset_does_not_invent_associated_race_event()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        SessionId id = SessionId.New();
        WorkoutSession workout = new(
            id,
            new TimestampInstant(start, ClockSource.WorkoutSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(5), ClockSource.WorkoutSession)),
            [new HeartRateSample(new TimestampInstant(start.AddMinutes(2), ClockSource.WorkoutSession), 150)],
            Array.Empty<EnergySample>(),
            OptionalValue<int>.Unavailable(),
            OptionalValue<int>.Unavailable(),
            OptionalValue<double>.Unavailable());

        SimulatorSession sim = new(
            id,
            new Simulator(SimulatorIds.IRacing, "iRacing"),
            OptionalValue<Track>.Available(new Track("okayama", "Okayama", OptionalValue<string>.Unavailable())),
            OptionalValue<Vehicle>.Available(new Vehicle("mx5", "MX-5", OptionalValue<string>.Unavailable())),
            OptionalValue<SimulatorSessionType>.Available(SimulatorSessionType.Practice),
            new TimestampInstant(start, ClockSource.SimulatorSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(5), ClockSource.SimulatorSession)),
            [
                new Lap(id, 1, new TimestampInstant(start, ClockSource.SimulatorSession),
                    OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(2), ClockSource.SimulatorSession)),
                    OptionalValue<TimeSpan>.Available(TimeSpan.FromMinutes(2)),
                    OptionalValue<int>.Available(3))
            ],
            [RaceEvent.Create(id, RaceEventType.LapComplete, new TimestampInstant(start.AddMinutes(2), ClockSource.SimulatorSession))]);

        DriverSession session = new(
            id,
            workout,
            OptionalValue<SimulatorSession>.Available(sim),
            OptionalValue<TimeSpan>.Unavailable());

        RaceReport report = RaceReportBuilder.FromDriverSession(session);

        Assert.True(report.SimulatorDisplayName.TryGet(out string? name));
        Assert.Equal("iRacing", name);
        Assert.True(report.LapCount.TryGet(out int laps));
        Assert.Equal(1, laps);
        Assert.True(report.BestLapTime.TryGet(out TimeSpan best));
        Assert.Equal(TimeSpan.FromMinutes(2), best);
        Assert.Equal(DataPresence.Unavailable, report.PeakHeartRateAssociatedEvent.Presence);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```powershell
dotnet test packages/analytics/SimPulse.Analytics.Tests/SimPulse.Analytics.Tests.csproj --filter RaceReportBuilderTests --configuration Release
```

Expected: compile error / missing `RaceReport` / `RaceReportBuilder`.

- [ ] **Step 3: Minimal implementation**

`RaceReport.cs`:

```csharp
using SimPulse.Domain;

namespace SimPulse.Analytics;

public sealed record RaceReport(
    SessionId SessionId,
    OptionalValue<string> SimulatorDisplayName,
    OptionalValue<string> TrackDisplayName,
    OptionalValue<string> VehicleDisplayName,
    OptionalValue<SimulatorSessionType> SessionType,
    OptionalValue<TimeSpan> Duration,
    OptionalValue<int> LapCount,
    OptionalValue<int> StartPosition,
    OptionalValue<int> FinishPosition,
    OptionalValue<TimeSpan> BestLapTime,
    OptionalValue<double> AverageHeartRateBpm,
    OptionalValue<int> MaximumHeartRateBpm,
    OptionalValue<double> ActiveKilocalories,
    OptionalValue<DateTimeOffset> PeakHeartRateAtUtc,
    OptionalValue<RaceEventType> PeakHeartRateAssociatedEvent,
    IReadOnlyList<HeartRateSample> HeartRateTimeline,
    IReadOnlyList<Lap> Laps);
```

`RaceReportBuilder.cs`: implement `FromDriverSession` using `HeartRateMetrics` / `EnergyMetrics`. Duration from workout start/end when both available. Simulator fields from `SimulatorSession` when present. Best lap = minimum available `Lap.LapTime`. Start/finish position from first/last lap with available position. **If `TimelineOffset` is not Available, `PeakHeartRateAssociatedEvent` must be Unavailable** (do not match by wall clock). HeartRateTimeline = workout samples. Laps = simulator laps or empty.

- [ ] **Step 4: Run tests — expect PASS**

```powershell
dotnet test SimPulse.sln --configuration Release
```

- [ ] **Step 5: Docs + commit**

Mark ANALYTICS-002 DONE. Commit:

```text
feat(analytics): add RaceReport model from DriverSession
```

---

### Task 2: BRIDGE-004 Session and lap lifecycle tracker

**Backlog:** BRIDGE-004  
**Files:**
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Application/SessionLifecycleTracker.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/SessionLifecycleTrackerTests.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Application/BridgeRuntime.cs` (optional: feed tracker for logging dedupe — only if tests require; otherwise keep tracker standalone and call from tests + document for BRIDGE-003)
- Modify: `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`

**Interfaces:**
- Consumes: `SessionId`, `RaceEvent`, `RaceEventType`, `TimestampInstant`
- Produces: `SessionLifecycleTracker.Observe(RaceEvent candidate) -> RaceEvent?` returning null when duplicate; identity key = `(SessionId, Type, lapNumberAttributeOrEmpty)`

- [ ] **Step 1: Failing tests**

```csharp
using SimPulse.Bridge.Core.Application;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Tests;

public sealed class SessionLifecycleTrackerTests
{
    [Fact]
    public void Emits_session_start_once()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent first = RaceEvent.Create(id, RaceEventType.SessionStart, t);
        RaceEvent second = RaceEvent.Create(id, RaceEventType.SessionStart, t.Value.AddSeconds(1) is var _ 
            ? new TimestampInstant(t.Value.AddSeconds(1), ClockSource.SimulatorSession) 
            : t);

        Assert.NotNull(tracker.Observe(first));
        Assert.Null(tracker.Observe(second));
    }

    [Fact]
    public void Emits_distinct_lap_completes_and_dedupes_same_lap()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent lap1 = RaceEvent.Create(id, RaceEventType.LapComplete, t, new Dictionary<string, string> { ["lapNumber"] = "1" });
        RaceEvent lap1Again = RaceEvent.Create(id, RaceEventType.LapComplete, t, new Dictionary<string, string> { ["lapNumber"] = "1" });
        RaceEvent lap2 = RaceEvent.Create(id, RaceEventType.LapComplete, t, new Dictionary<string, string> { ["lapNumber"] = "2" });

        Assert.NotNull(tracker.Observe(lap1));
        Assert.Null(tracker.Observe(lap1Again));
        Assert.NotNull(tracker.Observe(lap2));
    }

    [Fact]
    public void Session_end_is_idempotent()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent end = RaceEvent.Create(id, RaceEventType.SessionEnd, t);
        Assert.NotNull(tracker.Observe(end));
        Assert.Null(tracker.Observe(end));
    }
}
```

Fix the awkward ternary in the first test when implementing — use a clear `TimestampInstant` for the second event.

- [ ] **Step 2: Run — expect FAIL** (type missing)

- [ ] **Step 3: Implement `SessionLifecycleTracker`**

```csharp
namespace SimPulse.Bridge.Core.Application;

public sealed class SessionLifecycleTracker
{
    private readonly HashSet<string> _emitted = new(StringComparer.Ordinal);

    public RaceEvent? Observe(RaceEvent candidate)
    {
        string key = BuildKey(candidate);
        if (!_emitted.Add(key))
        {
            return null;
        }

        return candidate;
    }

    private static string BuildKey(RaceEvent e)
    {
        e.Attributes.TryGetValue("lapNumber", out string? lap);
        return $"{e.SimulatorSessionId}:{e.Type}:{lap ?? ""}";
    }
}
```

- [ ] **Step 4: Full solution tests PASS**

- [ ] **Step 5: Mark BRIDGE-004 DONE when acceptance is met (idempotent SESSION_START/END + LAP_START/COMPLETE). Commit:**

```text
feat(bridge): add idempotent session lifecycle tracker
```

---

### Task 3: BRIDGE-005 WebSocket server

**Backlog:** BRIDGE-005  
**Files:**
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Ports/ClientTransportPorts.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Application/ClientSessionHub.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/HttpListenerWebSocketTransport.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/WebSocketTransportTests.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge/Program.cs`, `Worker.cs` to start transport alongside runtime
- Modify: docs (BACKLOG BRIDGE-005, CURRENT_STATE, KNOWN_ISSUES KI-003 partially)

**Interfaces:**
- Consumes: `EnvelopeCodec`, `MessageTypes`, `IClock`, host/port from env `SIMPULSE_BRIDGE_HOST` (default `127.0.0.1`), `SIMPULSE_BRIDGE_PORT` (default `8742`)
- Produces:
  - `IClientConnection` with `string? DeviceId { get; set; }`, `bool IsTrusted { get; set; }`, `Task SendAsync(string json, CancellationToken ct)`
  - `IClientSessionHub` with `Task BroadcastToTrustedAsync(string json, CancellationToken ct)`, connection registration
  - `IBridgeTransport` with `Task RunAsync(Func<IClientConnection, CancellationToken, Task> onConnected, CancellationToken ct)`
  - `HttpListenerWebSocketTransport` implementing `IBridgeTransport`

**Behavior:**
- Accept WebSocket upgrades on `http://{host}:{port}/ws/`
- On connect, run a read loop: deserialize envelope; **unknown `type` → log + ignore** (do not disconnect); known types handed to a callback `Func<IClientConnection, MessageEnvelope, CancellationToken, Task>` injected later by Task 4 — for Task 3, a no-op/echo-hello handler is enough **except** Hello must be parseable.
- Reconnect: second connection is a new `IClientConnection` (no sticky session required beyond device trust in Task 4).
- **Do not send simulator telemetry to untrusted connections** (hub method name makes this explicit even before Task 4 wires trust).
- No biometrics on the wire.
- Structured logs for listen start, accept, close, duration; never log full payloads.

- [ ] **Step 1: Failing integration test** using `ClientWebSocket` against ephemeral port:

```csharp
[Fact]
public async Task Accepts_websocket_and_ignores_unknown_type()
{
    // Start HttpListenerWebSocketTransport on 127.0.0.1:0 (bind ephemeral — if HttpListener needs explicit port, pick a free port via TcpListener)
    // Connect ClientWebSocket to ws://127.0.0.1:{port}/ws/
    // Send EnvelopeCodec.Serialize("not.a.real.type", new { }, DateTimeOffset.UtcNow)
    // Then send HelloMessage; assert connection still open (receive optional ack or just State == Open after 200ms)
}
```

Also test `ClientSessionHub.BroadcastToTrustedAsync` only delivers to connections with `IsTrusted == true` (unit test with fake connections — no sockets required).

- [ ] **Step 2: RED**

- [ ] **Step 3: Implement ports, hub, HttpListener adapter, wire Worker to `Task.WhenAny(runtime, transport)` or parallel tasks**

Default bind **127.0.0.1** for safety (SECURITY.md). Document that `0.0.0.0` is opt-in via env.

- [ ] **Step 4: Full suite PASS**

- [ ] **Step 5: Commit**

```text
feat(bridge): accept WebSocket clients on LAN loopback
```

Note: Pairing gate is Task 4. Task 3 may leave all connections untrusted so BroadcastToTrusted is a no-op until pairing exists — that is intentional.

---

### Task 4: BRIDGE-006 Pairing and trusted devices

**Backlog:** BRIDGE-006  
**Files:**
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingCoordinator.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingPinGenerator.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/JsonFileTrustedDeviceStore.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/PairingCoordinatorTests.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/JsonFileTrustedDeviceStoreTests.cs`
- Modify: `packages/protocol/SimPulse.Protocol/Messages.cs` — add `PairingRejectMessage(string DeviceId, string Reason)`
- Modify: transport callback wiring + `Program.cs` to use `JsonFileTrustedDeviceStore` when `SIMPULSE_TRUSTED_DEVICES_PATH` is set (else in-memory)
- Modify: docs; close KI-003 if transport+pairing satisfy “no transport” issue (update to remaining UX gaps if any)

**Interfaces:**
- Consumes: `ITrustedDeviceStore`, `IClock`, `PairingRequestMessage`, `PairingAcceptMessage`, `PairingRejectMessage`, `HelloMessage`
- Produces: `PairingCoordinator.HandleAsync(IClientConnection connection, MessageEnvelope envelope, CancellationToken ct)`
- PIN: six digits, generated at coordinator construction / `BeginPairingWindow()`, **not persisted**; compare with `PairingRequestMessage.Pin`; on success `TrustAsync` + set `connection.IsTrusted = true` + send accept; on failure send reject; wrong PIN does not trust.
- Already-trusted `DeviceId` on Hello → set `IsTrusted = true` without PIN.
- Revoke: `ITrustedDeviceStore.RevokeAsync` → subsequent `IsTrustedAsync` false; unpaired get **no** broadcast telemetry.
- File store: JSON array of `{ deviceId, trustedAtUtc, revoked }`; atomic write; path from env; never commit the file.

- [ ] **Step 1: Failing tests** — wrong PIN rejected; correct PIN trusts; revoke blocks; file store round-trip; BroadcastToTrusted skips untrusted after revoke.

- [ ] **Step 2: RED**

- [ ] **Step 3: Implement + wire**

Log pairing window start with PIN at Information **once** when window opens (required until tray UX exists). Do not re-log PIN on every request. Do not log PIN on reject/accept lines.

- [ ] **Step 4: Full suite PASS**

- [ ] **Step 5: Mark BRIDGE-005 and BRIDGE-006 DONE if both ACs met; update KI-003; commit:**

```text
feat(bridge): add PIN pairing and file trusted-device store
```

If Task 3 left BRIDGE-005 as BACKLOG pending pairing, mark both DONE here.

---

## Out of scope (do not implement)

- BRIDGE-003 iRacing mmap
- BRIDGE-007 tray UX
- Apple / Android apps
- TLS
- mDNS discovery

## Self-review checklist

1. Spec coverage: ANALYTICS-002, BRIDGE-004, BRIDGE-005, BRIDGE-006 each have a task.
2. No placeholders / TBD steps.
3. Types consistent: `RaceReport`, `SessionLifecycleTracker.Observe`, `IBridgeTransport`, `PairingCoordinator.HandleAsync`.
