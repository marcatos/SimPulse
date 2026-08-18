Task
BRIDGE-008 — iRacing variable table (Task 3: snapshot telemetry + adapter)

Goal
Read player car, session type, simulator session time, and lap events from IRSDK telemetry; re-parse YAML only when `SessionInfoUpdate` changes. No 60 Hz WebSocket frames.

Status
IN_PROGRESS (Tasks 1–3 complete; Task 4 docs + KI-002 remaining)

Files changed
- `apps/windows-bridge/SimPulse.Bridge.Core/Ports/IracingPorts.cs` — `IracingTelemetryValues`, 4-field snapshot + 2-arg ctor, `WaitForUpdate`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingMemorySnapshotReader.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingLiveSession.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/WindowsIracingSharedMemory.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionMapper.cs` — attach `Laps`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/IRacingAdapter.cs` — wait/read loop only
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/FakeIracingSharedMemory.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IRacingAdapterTelemetryTests.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingMemorySnapshotReaderTests.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/WindowsIracingSharedMemoryTests.cs`
- `docs/handoffs/BRIDGE-008.md`

Decisions made
- 2-arg `IracingMemorySnapshot(yaml, Connected)` stays compiling (`SessionInfoUpdate=0`, telemetry Unknown). Named `Connected:` requires PascalCase ctor params.
- Missing var names → `Unknown`, no throw.
- Windows waits on `Local\IRSDKDataValidEvent`; missing/timeout → false, caller still reads, then poll idle.
- Fake `WaitForUpdate` returns true immediately so `pollInterval: Zero` tests stay fast.
- YAML re-parsed only when `SessionInfoUpdate` changes; same update + different yaml text keeps cached vehicle.
- Player car / session type from `Parse(yaml, DriverCarIdx, SessionNum)` when those optionals are Available.
- SessionStart/SessionEnd stay `ClockSource.Utc`. Lap timestamps use `ClockSource.SimulatorSession` when SessionTime Available: `sessionStartUtc + TimeSpan.FromSeconds(sessionTime)`.
- Lap increase: first observed Lap → LapStart only; later increase → LapComplete previous (≥1) then LapStart. Attributes `lapNumber`. Attached to snapshot `Laps`.
- `NormalizedSimulatorUpdate.Telemetry` stays null (no WebSocket telemetry frames).
- Apply logic lives in `IracingLiveSession` so `IRacingAdapter.cs` stays ≤300 lines.
- Available `SessionNum` change clears `_observedLap`/`_laps` (practice→race LapStart); Unknown does not reset; no second SessionStart.

Tests executed
- `dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests --filter IRacingAdapterTelemetryTests|IracingMemorySnapshotReaderTests --configuration Release` (RED: types missing; GREEN after implement)
- `dotnet test SimPulse.sln --configuration Release`

Tests passing
123 passed, 0 failed (Domain 6, Analytics 9, Protocol 7, Bridge.Core 101). Apple/Xcode NOT EXECUTED.

Known failures
None.

Remaining work
- Task 4: Docs + KI-002 (BACKLOG DONE, CURRENT_STATE, KNOWN_ISSUES, THIRD_PARTY, handoff ACs)

Risks
- Live sim still required for KI-002 end-to-end; CI uses Fake + synthetic buffers.
- `DataValidEvent` wait is best-effort; missing event falls back to poll interval.

Suggested next action
Task 4: mark BRIDGE-008 done in docs; update KI-002 for player car / SessionNum / SessionTime / sessionInfoUpdate / lap events.
