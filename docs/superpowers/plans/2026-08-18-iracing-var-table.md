# BRIDGE-008 iRacing variable table Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read the live IRSDK ~60 Hz variable table (latest triple-buffer) so the Bridge picks the **player car**, the **current session type**, **simulator session time**, and **lap events**, and re-parses YAML only when `sessionInfoUpdate` changes — without broadcasting 60 Hz frames on the WebSocket.

**Architecture:** Hexagonal. Bytes stay in adapters: expand `IracingHeaderReader` + new `IracingVarTableReader` over `ReadOnlySpan<byte>`. `WindowsIracingSharedMemory` maps mmap → `IracingMemorySnapshot` (YAML + parsed telemetry values). `IRacingAdapter` consumes the snapshot only. Domain stays IRSDK-free. Tests use synthetic buffers and `FakeIracingSharedMemory`; CI never needs a live sim.

**Tech Stack:** .NET 8, xUnit, `System.IO.MemoryMappedFiles`, `EventWaitHandle` on Windows, first-party IRSDK constants with iRacing BSD copyright, Conventional Commits.

## Global Constraints

- Conventional Commits: `feat|fix|refactor|test|docs|chore(<scope>): …`
- Hexagonal: Core/domain have no `System.Windows.Forms`; IRSDK types stay in Bridge adapters; no GPL (no IRSDKSharper).
- Do **not** add `simulator.telemetry-frame` (or similar) WebSocket payloads. Trusted clients still get race-events only.
- Ordinary CI must never require a live iRacing session.
- Logging: INFO default; start/steps/end/durations; never log raw telemetry buffers or full YAML; never log PIN.
- Files ≤300 lines; methods ≤60; split rather than grow (`IRacingAdapter.cs` is already ~228 lines — extract telemetry/lap application).
- Update BACKLOG (new BRIDGE-008), CURRENT_STATE, KNOWN_ISSUES KI-002 when ACs met.
- Never change `git config`. Do not implement TLS, tray, Apple apps, or ANALYTICS-003 RaceReportBuilder wiring.
- Default bind remains loopback.

---

## File map

| Path | Responsibility |
| --- | --- |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs` | Header / varBuf / varHeader sizes + copyright |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingHeaderReader.cs` | Full header including `sessionInfoUpdate`, `numVars`, latest `varBuf` |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingVarTableReader.cs` | Parse var headers; read named typed values from a buffer row |
| `apps/windows-bridge/SimPulse.Bridge.Core/Ports/IracingPorts.cs` | Snapshot + telemetry values + `WaitForUpdate` |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionInfoParser.cs` | Drivers by `CarIdx`; Sessions by `SessionNum` |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionMapper.cs` | Map resolved session info + laps |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingLiveSession.cs` | YAML cache, player car, session type, lap deltas → updates |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/IRacingAdapter.cs` | Open/wait/end loop only |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/WindowsIracingSharedMemory.cs` | Read full snapshot; wait on `IRSDKDataValidEvent` |
| `apps/windows-bridge/SimPulse.Bridge.Core.Tests/*` | Synthetic mmap bytes + adapter tests |
| `tests/fixtures/iracing/session-info-two-drivers.yaml` | Two cars + two session types |
| docs BACKLOG / CURRENT_STATE / KI-002 / handoff BRIDGE-008.md |

---

### Task 1: Header + variable table reader

**Backlog:** BRIDGE-008 (partial — byte parsers, testable without mmap)

**Files:**
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingHeaderReader.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingVarTableReader.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingVarTableReaderTests.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingHeaderReaderTests.cs`

**Interfaces:**
- Consumes: existing `TryReadHeader` 24-byte subset
- Produces:

```csharp
public readonly record struct IracingHeaderLayout(
    int Status,
    bool Connected,
    int SessionInfoUpdate,
    int SessionInfoLen,
    int SessionInfoOffset,
    int NumVars,
    int VarHeaderOffset,
    int NumBuf,
    int BufLen,
    int LatestTickCount,
    int LatestBufOffset);

public readonly record struct IracingVarHeader(string Name, int Type, int Offset, int Count);

public static class IracingVarTableReader
{
    public static bool TryReadVarHeaders(ReadOnlySpan<byte> buffer, in IracingHeaderLayout header, out IracingVarHeader[] headers);
    public static bool TryReadInt(ReadOnlySpan<byte> row, IracingVarHeader header, out int value);
    public static bool TryReadDouble(ReadOnlySpan<byte> row, IracingVarHeader header, out double value);
    public static bool TryFind(IReadOnlyList<IracingVarHeader> headers, string name, out IracingVarHeader header);
}
```

Official layout (vendor as constants, keep iRacing copyright notice):

| Field | Offset |
| --- | --- |
| `ver` | 0 |
| `status` | 4 |
| `tickRate` | 8 |
| `sessionInfoUpdate` | 12 |
| `sessionInfoLen` | 16 |
| `sessionInfoOffset` | 20 |
| `numVars` | 24 |
| `varHeaderOffset` | 28 |
| `numBuf` | 32 |
| `bufLen` | 36 |
| pad | 40, 44 |
| `varBuf[0]` | 48 (`tickCount` +0, `bufOffset` +4, 16-byte stride) |
| `IRSDK_MAX_BUFS` | 4 |
| varHeader size | 144 (`type` 0, `offset` 4, `count` 8, name at 16 size 32, desc 64, unit 32) |
| `irsdk_int` | 2 |
| `irsdk_double` | 5 |

Latest buffer = `varBuf[i]` with the highest `tickCount` among `numBuf` (clamp 1..4). Reject if any offset/length is out of span.

- [ ] **Step 1: Failing tests**

Build a synthetic mmap in tests: header `numVars=4`, `numBuf=2`, `bufLen=32`, var headers for `SessionTime` (double), `SessionNum` (int), `DriverCarIdx` (int), `Lap` (int). Put values in buffer 1 (higher tickCount). Assert `LatestBufOffset` points at buffer 1 and typed reads match.

Also: `sessionInfoUpdate` round-trip; connected bit still works; truncated buffer → false.

- [ ] **Step 2: RED** — `dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests --filter IracingVarTableReaderTests --configuration Release` fails (types missing).

- [ ] **Step 3: Implement constants + `TryReadLayout` + var table reader.** Keep `TryReadHeader` working for existing tests (delegate to layout or keep 24-byte path). `HeaderMinSize` for a var-capable header is 112 bytes; YAML-only path may still succeed with 24 bytes (no vars).

- [ ] **Step 4: GREEN** — focused tests pass; full `dotnet test SimPulse.sln --configuration Release` passes.

- [ ] **Step 5: Commit**

```text
feat(bridge): parse IRSDK variable table from mmap bytes
```

---

### Task 2: YAML player car + SessionNum

**Backlog:** BRIDGE-008 (partial)

**Files:**
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionInfoParser.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingSessionInfoParserTests.cs`
- Create: `tests/fixtures/iracing/session-info-two-drivers.yaml`

**Interfaces:**
- Consumes: existing `IracingSessionInfo`
- Produces:

```csharp
public sealed record IracingSessionInfo(
    OptionalValue<string> TrackId,
    OptionalValue<string> TrackDisplayName,
    OptionalValue<string> VehicleId,
    OptionalValue<string> VehicleDisplayName,
    OptionalValue<string> SessionType);

public static class IracingSessionInfoParser
{
    public static IracingSessionInfo Parse(string yaml, int? driverCarIdx = null, int? sessionNum = null);
}
```

When `driverCarIdx` is set, take `CarPath` / `CarScreenName` from the `Drivers:` list entry whose `CarIdx` matches (not the first `DriverInfo` keys). When unset, keep current first-entry behavior.

When `sessionNum` is set, take `SessionType` from the `Sessions:` entry whose `SessionNum` matches. When unset, keep first `SessionType`.

Fixture: two drivers (CarIdx 0 spectator-like car `othercar`, CarIdx 3 `mazda mx-5 cup`) and two sessions (0 Practice, 1 Race). Assert Parse(yaml, driverCarIdx: 3, sessionNum: 1) → MX-5 + Race.

Existing single-driver fixture tests must still pass with default args.

If the parser file would exceed ~300 lines, extract list parsing to `IracingSessionYamlLists.cs`.

- [ ] **Step 1: Failing tests** (two-driver fixture + Parse overload).
- [ ] **Step 2: RED**
- [ ] **Step 3: Implement list-aware parse.**
- [ ] **Step 4: GREEN** — `dotnet test SimPulse.sln --configuration Release`
- [ ] **Step 5: Commit**

```text
feat(bridge): resolve iRacing car and session from YAML lists
```

---

### Task 3: Snapshot telemetry + adapter (player car, session time, laps, YAML cache)

**Backlog:** BRIDGE-008 (behavior)

**Files:**
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Ports/IracingPorts.cs`
- Modify: `FakeIracingSharedMemory.cs`, `WindowsIracingSharedMemory.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingLiveSession.cs`
- Modify: `IRacingAdapter.cs` (keep as subscribe loop; delegate apply to `IracingLiveSession`)
- Modify: `IRacingAdapterTests.cs`
- Modify: mapper if laps must be attached to `SimulatorSession`

**Interfaces:**
- Consumes: Task 1 readers, Task 2 Parse
- Produces:

```csharp
public readonly record struct IracingTelemetryValues(
    OptionalValue<double> SessionTime,
    OptionalValue<int> SessionNum,
    OptionalValue<int> DriverCarIdx,
    OptionalValue<int> Lap);

public readonly record struct IracingMemorySnapshot(
    string? SessionYaml,
    bool Connected,
    int SessionInfoUpdate,
    IracingTelemetryValues Telemetry);
```

Keep a 2-arg snapshot constructor or optional params so existing `new IracingMemorySnapshot(yaml, connected)` still compiles (`SessionInfoUpdate=0`, telemetry unknown).

`WindowsIracingSharedMemory.TryReadSnapshot`: read layout; YAML as today; if var headers exist, fill telemetry from latest row (`SessionTime`, `SessionNum`, `DriverCarIdx`, `Lap`). Missing names → `Unknown`, not throw.

`IIracingSharedMemory.WaitForUpdate(TimeSpan timeout, CancellationToken cancellationToken)`: Windows waits on `Local\IRSDKDataValidEvent` (auto-reset / existing event). Missing event or timeout → `false` (caller still reads). Fake: return `true` immediately (or `Task.Yield` equivalent sync true) so tests with `pollInterval: Zero` stay fast.

`IRacingAdapter` loop: `WaitForUpdate` then `TryReadSnapshot` instead of unconditional `Task.Delay(100ms)` when wait is used. If wait returns false, still attempt a read (lost wakeup), then idle with existing poll interval as fallback.

`IracingLiveSession`:
- On first connected YAML: SessionStart (timestamp `ClockSource.Utc` via `IClock` — capture time). Cache `sessionInfoUpdate` + parsed info.
- Re-parse YAML only when `SessionInfoUpdate` changes (or first time).
- Vehicle/session type from `Parse(yaml, telemetry.DriverCarIdx, telemetry.SessionNum)` when those optionals are Available.
- `CapturedAt` stays `ClockSource.Utc` (when Bridge sampled).
- If `SessionTime` Available: lap / snapshot event timestamps use `ClockSource.SimulatorSession` with `Value = sessionStartUtc + TimeSpan.FromSeconds(sessionTime)` (sessionStartUtc = first SessionStart `Value`). SessionStart/SessionEnd stay Utc.
- Lap: when `Lap` Available and increases, emit `LapComplete` for previous number (if previous ≥ 1) then `LapStart` for new number, attributes `lapNumber`. Ignore 0 or negative. First observed Lap emits `LapStart` only (no complete). Attach laps to snapshot `Laps`.
- Do not emit a new SessionStart when only YAML updates.
- Connection lost after a live session: SessionEnd as today; reset cache.

Tests:
1. Two-driver YAML + telemetry DriverCarIdx=3 → vehicle MX-5, not first car.
2. SessionNum=1 → Race.
3. Same YAML, changing `SessionInfoUpdate` re-parses; unchanged update does not require YAML text change to keep cached vehicle (prove by feeding updated yaml with a different car but **same** SessionInfoUpdate — vehicle must stay the cached one).
4. Lap 1 then Lap 2 → LapStart 1, LapComplete 1, LapStart 2.
5. Existing start-after-mmap and SessionEnd tests still pass. SessionStart `ClockSource.Utc` unchanged.

- [ ] **Step 1: Failing tests**
- [ ] **Step 2: RED**
- [ ] **Step 3: Implement snapshot, Windows parse, live session, adapter loop.**
- [ ] **Step 4: GREEN** — `dotnet test SimPulse.sln --configuration Release`
- [ ] **Step 5: Commit**

```text
feat(bridge): use iRacing telemetry for car, session, laps
```

---

### Task 4: Docs + KI-002

**Backlog:** BRIDGE-008 DONE

**Files:**
- Modify: `docs/BACKLOG.md` — add BRIDGE-008 DONE with ACs
- Modify: `docs/CURRENT_STATE.md` — variable table in completed/partial; CI note can mention PR #3 green
- Modify: `docs/KNOWN_ISSUES.md` KI-002 — player car / SessionNum / SessionTime / sessionInfoUpdate done; live still needs sim+mmap; 60 Hz wait is best-effort (`DataValidEvent`); no WS telemetry frames
- Modify: `docs/THIRD_PARTY.md` — constants now include var table offsets
- Create: `docs/handoffs/BRIDGE-008.md`

ACs to record:
- Latest IRSDK varBuf by tickCount
- `DriverCarIdx`, `SessionNum`, `SessionTime`, `Lap` read when present
- YAML re-parsed only on `sessionInfoUpdate` change
- LapStart/LapComplete from Lap increases
- No 60 Hz WebSocket frames
- Tests pass on Windows + Ubuntu without a live sim

- [ ] **Step 1: Update docs to match shipped behavior** (do not claim live-on-track verification unless you ran iRacing).
- [ ] **Step 2: Commit**

```text
docs(bridge): mark BRIDGE-008 iRacing variable table done
```

---

## Out of scope

- TLS / per-device reconnect secret (KI-003, KI-006)
- ANALYTICS-003 `RaceReportBuilder` peak-event wiring
- Broadcasting telemetry samples
- Speed/RPM/position vars
- Pit / incident / flag vars
- `.ibt` file replay
