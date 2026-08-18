# BRIDGE-003 handoff

## Task

BRIDGE-003 — iRacing mmap reader (Task 3).

## Goal

Replace the `IRacingAdapter` stub with a first-party mmap session reader: detect iRacing, parse a YAML session-info subset (track, car, session type), emit SessionStart / SessionSnapshot / SessionEnd. No GPL. CI stays green without a live sim.

## Status

DONE

## Files changed

- `apps/windows-bridge/SimPulse.Bridge.Core/Ports/IracingPorts.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingHeaderReader.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionInfoParser.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionMapper.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/WindowsIracingSharedMemory.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/IRacingAdapter.cs`
- `apps/windows-bridge/SimPulse.Bridge/Program.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Application/BridgeRuntime.cs` (unavailable log text)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/FakeIracingSharedMemory.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingSessionInfoParserTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IRacingAdapterTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingHeaderReaderTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/WindowsIracingSharedMemoryTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/BridgeCoreTests.cs`
- `tests/fixtures/iracing/session-info-sample.yaml`
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/KNOWN_ISSUES.md`, `docs/THIRD_PARTY.md`

## Decisions made

- Port `IIracingSharedMemory` exposes `SessionYaml` + `Connected` (no raw buffer required for this task).
- Tests inject `FakeIracingSharedMemory`; host wires `WindowsIracingSharedMemory`.
- Missing mmap or non-Windows → `TryOpen` false, no throw.
- Synthetic YAML fixture (original), not a scraped copyrighted pack.
- IRSDK constants file retains the iRacing BSD-style copyright notice.
- Adapter emits SessionStart + SessionSnapshot when connected with YAML; SessionEnd when connection is lost after a session started; then clears stream state, re-`TryOpen`s, and keeps polling so Subscribe/Worker stay up.
- Timestamps from `IClock.UtcNow` use `ClockSource.Utc` until IRSDK `SessionTime` is used.
- `SessionType` is the first YAML `SessionInfo.Sessions[].SessionType` until `SessionNum` telemetry exists.
- 60 Hz telemetry variable table is out of scope.

## Tests executed

- Focused iRacing tests (parser, adapter, header, Windows opener)
- `dotnet test SimPulse.sln --configuration Release` (recorded in CURRENT_STATE after the run)

## Tests passing

Yes (see CURRENT_STATE)

## Known failures

None on .NET. Apple suites NOT EXECUTED.

## Remaining work

Live 60 Hz IRSDK variable table; confirm on a PC with iRacing + `irsdkEnableMem=1`. KI-002 remains open for that live dependency.

## Risks

Named mmap `Local\IRSDKMemMapFileName` must stay aligned with official IRSDK. Session-info YAML subset parser is indentation-tolerant but not a full YAML implementation. `SessionType` stays first-in-YAML until `SessionNum` is read from telemetry.

## Suggested next action

BRIDGE-007 tray / pairing PIN UX, or a live smoke test with iRacing running.
