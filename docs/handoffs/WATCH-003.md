Task
WATCH-003 — Persist and sync summary to iPhone

Goal
After a Sim Racing workout ends on the Watch: persist in HealthKit, enqueue a summary-only payload when the iPhone is unreachable, and deliver via WatchConnectivity when possible.

Status
IN_PROGRESS — branch `feat/watch-ios-sync-summary` (paired with IOS-004)

Files changed
- (pending implementation)

Decisions made
- Paired delivery with IOS-004; circular BACKLOG deps resolved as one slice.
- Summary-only payload via `transferUserInfo`; idempotency key = `HKWorkout.uuid`.
- See `docs/superpowers/specs/2026-08-19-watch-ios-summary-sync-design.md`.

Tests executed
- (pending)

Tests passing
- (pending)

Known failures
None yet.

Remaining work
- `WorkoutSummaryOutbox` port + file adapter
- Enqueue on `finishWorkout()`; flush when companion reachable
- WatchConnectivity send adapter

Risks
- WatchConnectivity delivery timing when iPhone app is backgrounded.

Suggested next action
Implement Watch outbox + WC send per design spec (Task 2+).
