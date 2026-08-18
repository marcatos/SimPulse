# ANALYTICS-003 Handoff

## Task

HR by lap and event windows — average BPM in simulator-aligned windows gated by timeline offset.

## Goal

Add `HeartRateWindows` with lap, event, and generic simulator window averages. Refuse correlation when `TimelineOffset` is unknown (ADR 0004).

## Status

DONE

## Files changed

- `packages/analytics/SimPulse.Analytics/HeartRateWindows.cs` (create)
- `packages/analytics/SimPulse.Analytics.Tests/HeartRateWindowsTests.cs` (create)
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/KNOWN_ISSUES.md`

## Decisions made

- Offset convention: `workoutTime = simulatorTime + offset`; map workout samples via `simulatorTime = workoutTime - offset`.
- Inclusive window bounds on simulator timeline.
- Lap average requires `CompletedAt`; empty filtered samples → `Unavailable` via `HeartRateMetrics.AverageBpm`.

## Tests executed

- `dotnet test packages/analytics/SimPulse.Analytics.Tests/SimPulse.Analytics.Tests.csproj --filter HeartRateWindowsTests --configuration Release`
- `dotnet test SimPulse.sln --configuration Release`

## Tests passing

- HeartRateWindowsTests: 3 passed
- Full suite: 57 passed, 0 failed

## Known failures

- None

## Remaining work

- None for ANALYTICS-003. RaceReportBuilder could wire `HeartRateWindows` for peak-HR event correlation in a follow-up.

## Risks

- None identified.

## Suggested next action

Proceed to BRIDGE-003 iRacing mmap or wire `HeartRateWindows` into `RaceReportBuilder` peak-HR association.
