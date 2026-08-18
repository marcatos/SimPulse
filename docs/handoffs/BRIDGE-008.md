Task
BRIDGE-008 — iRacing variable table (Task 2: YAML player car + SessionNum)

Goal
Resolve vehicle and session type from `Drivers:` / `Sessions:` list entries when `driverCarIdx` / `sessionNum` are supplied. Keep first-entry Parse behavior when those args are unset.

Status
IN_PROGRESS (Tasks 1–2 complete; Tasks 3–4 remaining)

Files changed
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingHeaderReader.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingVarTableReader.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingVarTableReaderTests.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingHeaderReaderTests.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionInfoParser.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingSessionInfoParserTests.cs`
- `tests/fixtures/iracing/session-info-two-drivers.yaml` (create)
- `docs/BACKLOG.md`
- `docs/CURRENT_STATE.md`
- `docs/handoffs/BRIDGE-008.md`

Decisions made
- Follow the plan table for official `irsdk_header` / `irsdk_varHeader` offsets.
- Keep `HeaderMinSize = 24` so existing YAML-only `TryReadHeader` tests pass.
- `HeaderLayoutMinSize = 112` for var-capable headers (`IRSDK_MAX_BUFS` * 16-byte `varBuf` after the 48-byte prefix).
- Latest buffer is the `varBuf[i]` with the highest `tickCount` among `numBuf` clamped 1..4.
- Reject layout/var-header reads when any offset/length is outside the span.
- `Parse(yaml, driverCarIdx, sessionNum)` optional args; default path still first `DriverInfo`/`SessionInfo` keys.
- List matching uses `CarIdx` / `SessionNum` ordinal string compare with invariant `ToString`.
- Parser stayed under 300 lines; list parsing was not extracted.

Tests executed
- `dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests --filter IracingVarTableReaderTests --configuration Release` (RED: types missing; GREEN after implement)
- `dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests --filter IracingSessionInfoParserTests --configuration Release` (RED: first-car `othercar`; GREEN after list match)
- `dotnet test SimPulse.sln --configuration Release`

Tests passing
109 passed, 0 failed (Domain 6, Analytics 9, Protocol 7, Bridge.Core 87). Apple/Xcode NOT EXECUTED.

Known failures
None.

Remaining work
- Task 3: Snapshot telemetry + adapter
- Task 4: Docs + KI-002

Risks
- Live sim still required for KI-002 end-to-end; CI uses synthetic buffers only.
- Unmatched `driverCarIdx` / `sessionNum` keep first-entry fallback (not Unavailable).

Suggested next action
Task 3: snapshot telemetry + adapter (player car, session time, laps, YAML cache).
