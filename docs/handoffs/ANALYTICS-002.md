# ANALYTICS-002 handoff

## Task

RaceReport model — structured analytics report from `DriverSession` with `DataPresence` for missing fields.

## Goal

Add `RaceReport` record and `RaceReportBuilder.FromDriverSession(DriverSession)` using existing `HeartRateMetrics` / `EnergyMetrics`.

## Status

DONE

## Files changed

- `packages/analytics/SimPulse.Analytics/RaceReport.cs`
- `packages/analytics/SimPulse.Analytics/RaceReportBuilder.cs`
- `packages/analytics/SimPulse.Analytics.Tests/RaceReportBuilderTests.cs`
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`

## Decisions made

- Duration from workout start/end when both available.
- Simulator fields from `SimulatorSession` when present; unavailable when workout-only.
- Best lap = minimum available `Lap.LapTime`.
- Start/finish position from first/last lap with available position.
- `PeakHeartRateAssociatedEvent` unavailable when `TimelineOffset` is not Available (ADR 0004).
- Positive offset-based event correlation deferred to ANALYTICS-003.
- HeartRateTimeline = workout samples; Laps = simulator laps or empty.

## Tests executed

- `dotnet test packages/analytics/SimPulse.Analytics.Tests/SimPulse.Analytics.Tests.csproj --filter RaceReportBuilderTests --configuration Release` — 2 passed
- `dotnet test SimPulse.sln --configuration Release` — 22 passed

## Tests passing

Yes

## Known failures

None

## Remaining work

ANALYTICS-003 for offset-based peak-HR event correlation.

## Risks

None

## Suggested next action

Proceed to ANALYTICS-003 or next Windows unblocked slice task.
