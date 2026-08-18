# BRIDGE-004 handoff

## Task

Session and lap lifecycle tracker — idempotent dedupe for normalized `RaceEvent` ticks.

## Goal

Add standalone `SessionLifecycleTracker.Observe(RaceEvent)` returning the event on first sight and `null` on duplicates keyed by `(SessionId, Type, lapNumberAttributeOrEmpty)`.

## Status

DONE

## Files changed

- `apps/windows-bridge/SimPulse.Bridge.Core/Application/SessionLifecycleTracker.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/SessionLifecycleTrackerTests.cs`
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/KNOWN_ISSUES.md`

## Decisions made

- Tracker stays standalone; no `BridgeRuntime` wiring until BRIDGE-003 emits ticks.
- Identity key uses `lapNumber` attribute when present; empty string otherwise.
- Covers `SessionStart`, `SessionEnd`, `LapStart`, `LapComplete` idempotency.
- SessionStart test uses explicit second `TimestampInstant` (no ternary).

## Tests executed

- `dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests/SimPulse.Bridge.Core.Tests.csproj --filter SessionLifecycleTrackerTests --configuration Release` — 4 passed
- `dotnet test SimPulse.sln --configuration Release` — 26 passed

## Tests passing

Yes

## Known failures

None

## Remaining work

BRIDGE-003 should call `SessionLifecycleTracker.Observe` on normalized ticks before logging/forwarding.

## Risks

None

## Suggested next action

Proceed to BRIDGE-005 WebSocket or BRIDGE-003 iRacing mmap.
