Task
BRIDGE-008 — iRacing variable table (Task 1: header + variable table reader)

Goal
Parse official IRSDK header layout fields and typed SessionTime / SessionNum / DriverCarIdx / Lap values from synthetic mmap bytes. No live iRacing. No GPL. No WebSocket telemetry frames.

Status
IN_PROGRESS (Task 1 complete; Tasks 2–4 remaining)

Files changed
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingHeaderReader.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingVarTableReader.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingVarTableReaderTests.cs` (create)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/IracingHeaderReaderTests.cs`
- `docs/BACKLOG.md`
- `docs/CURRENT_STATE.md`
- `docs/handoffs/BRIDGE-008.md`

Decisions made
- Follow the plan table for official `irsdk_header` / `irsdk_varHeader` offsets.
- Keep `HeaderMinSize = 24` so existing YAML-only `TryReadHeader` tests pass.
- `HeaderLayoutMinSize = 112` for var-capable headers (`IRSDK_MAX_BUFS` * 16-byte `varBuf` after the 48-byte prefix).
- Latest buffer is the `varBuf[i]` with the highest `tickCount` among `numBuf` clamped 1..4.
- Reject layout/var-header reads when any offset/length is outside the span.

Tests executed
- `dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests --filter IracingVarTableReaderTests --configuration Release` (RED: types missing; GREEN after implement)
- `dotnet test SimPulse.sln --configuration Release`

Tests passing
107 passed, 0 failed (Domain 6, Analytics 9, Protocol 7, Bridge.Core 85). Apple/Xcode NOT EXECUTED.

Known failures
None.

Remaining work
- Task 2: YAML player car + SessionNum
- Task 3: Snapshot telemetry + adapter
- Task 4: Docs + KI-002

Risks
- Live sim still required for KI-002 end-to-end; CI uses synthetic buffers only.

Suggested next action
Task 2: resolve car and session type from YAML lists using DriverCarIdx / SessionNum.
