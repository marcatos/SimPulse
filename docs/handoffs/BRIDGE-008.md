Task
BRIDGE-008 — iRacing variable table (Tasks 1–4)

Goal
Read the live IRSDK variable table (latest triple-buffer) so the Bridge picks the player car, current session type, simulator session time, and lap events, and re-parses YAML only when `sessionInfoUpdate` changes — without broadcasting 60 Hz WebSocket frames.

Status
DONE

Files changed
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs` — header / varBuf / varHeader sizes + copyright
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingHeaderReader.cs` — full header including `sessionInfoUpdate`, `numVars`, latest `varBuf`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingVarTableReader.cs` — var headers + typed named reads
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionInfoParser.cs` — Drivers by `CarIdx`; Sessions by `SessionNum`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingMemorySnapshotReader.cs` — mmap bytes → YAML + telemetry
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingLiveSession.cs` — YAML cache, identity re-resolve, lap deltas
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionMapper.cs` — attach `Laps`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/WindowsIracingSharedMemory.cs` — snapshot + `IRSDKDataValidEvent` wait
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/IRacingAdapter.cs` — wait/read/apply loop only
- `apps/windows-bridge/SimPulse.Bridge.Core/Ports/IracingPorts.cs` — snapshot + telemetry values + `WaitForUpdate`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/*` — synthetic mmap + adapter tests (`FakeIracingSharedMemory`)
- `tests/fixtures/iracing/session-info-two-drivers.yaml`
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/KNOWN_ISSUES.md` (KI-002), `docs/THIRD_PARTY.md`, this handoff

Decisions made
- Latest buffer = `varBuf[i]` with the highest `tickCount` among `numBuf` (clamped 1..4). Out-of-span offsets rejected.
- Missing var names → `Unknown`, no throw. Unmatched YAML `driverCarIdx` / `sessionNum` → Unavailable (no first-entry fallback).
- 2-arg `IracingMemorySnapshot(yaml, Connected)` stays compiling (`SessionInfoUpdate=0`, telemetry Unknown).
- YAML is re-tokenized only when `SessionInfoUpdate` changes. Same update + mutated YAML text keeps the cached vehicle.
- When `SessionInfoUpdate` is unchanged but Available `DriverCarIdx` or `SessionNum` identity changes, `Parse` runs on the cached YAML string (BUG-004).
- Available `SessionNum` change clears observed lap / snapshot laps (practice→race LapStart); Unknown does not reset; no second SessionStart.
- SessionStart/SessionEnd stay `ClockSource.Utc`. Lap timestamps use `ClockSource.SimulatorSession` when `SessionTime` is Available: `sessionStartUtc + TimeSpan.FromSeconds(sessionTime)`.
- Lap increase: first observed Lap ≥ 1 → LapStart only; later increase → LapComplete previous (≥ 1) then LapStart. Attributes `lapNumber`. Attached to snapshot `Laps`.
- Windows waits on `Local\IRSDKDataValidEvent`; missing/timeout → false; caller still reads, then poll idle. Fake `WaitForUpdate` returns true immediately.
- `NormalizedSimulatorUpdate.Telemetry` stays null (no WebSocket telemetry frames). Trusted clients still receive race-events only.
- Apply logic lives in `IracingLiveSession` so `IRacingAdapter` stays a subscribe loop.
- No live iRacing process was used to verify on-track behavior.

Tests executed
- Focused parser / snapshot / adapter telemetry tests (RED then GREEN during Tasks 1–3)
- `dotnet test SimPulse.sln --configuration Release` (Task 4 docs pass, 2026-08-18, Windows 10.0.26200, SDK 8.0.424)

Tests passing
126 passed, 0 failed (Domain 6, Analytics 9, Protocol 7, Bridge.Core 104). Apple/Xcode NOT EXECUTED. No live sim.

Known failures
None in unit tests. Live mmap path is untested on a running iRacing session (KI-002).

Remaining work
- Live smoke with iRacing + `irsdkEnableMem=1` (KI-002)
- ANALYTICS-003 `RaceReportBuilder` wiring remains out of scope

Risks
- `DataValidEvent` wait is best-effort; missing event falls back to poll interval.
- CI uses Fake + synthetic buffers only.

Suggested next action
Live iRacing smoke (`irsdkEnableMem=1`) or TLS (KI-003).
