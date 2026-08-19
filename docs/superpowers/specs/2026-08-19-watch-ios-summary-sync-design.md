# WATCH-003 + IOS-004 — WatchConnectivity workout summary sync

**Date:** 2026-08-19  
**Status:** Approved  
**Backlog:** WATCH-003 (Watch persist + queue), IOS-004 (iPhone ingest)  
**Depends on:** WATCH-001 (DONE), IOS-001/003 (DONE). Circular BACKLOG deps resolved: implement as one paired slice; edge order Watch outbox → iOS ingest.

## Goal

After a Sim Racing workout finishes on the Watch, persist it in HealthKit (already done), enqueue a **summary-only** payload if the iPhone is unreachable, deliver it via WatchConnectivity when possible, and merge idempotently on iPhone by session ID (refresh session list / acknowledge ingest).

## Non-goals

- Sending HR sample series over WatchConnectivity (user chose summary-only)
- SwiftData schema (ADR 0002 still deferred; Watch outbox = file queue; iPhone merge does not require SwiftData for this slice)
- Bridge / LAN protocol changes
- Complication transfers, Interactive messaging as the primary delivery path
- Logging biometric values (bpm, kcal amounts in logs — counts and session IDs only)

## Context

- Watch already calls `finishWorkout()` but discards the returned `HKWorkout`.
- iPhone history already reads Sim Racing workouts from HealthKit (Apple sync). WC adds reliable app-level delivery + immediate refresh when the companion returns.
- No `WCSession` code exists yet.
- PRIVACY_ARCHITECTURE allows Watch→iPhone summary via WatchConnectivity on the paired device.

## Chosen decisions

- **Payload:** summary only (option A).
- **Architecture:** hexagonal ports + file outbox on Watch + WC adapters (approach 1).
- **Delivery:** `transferUserInfo` (queued by the system when unreachable).
- **Idempotency key:** `sessionId` = `HKWorkout.uuid.uuidString` (same id as HealthKit list).

## Wire DTO

```swift
struct WatchWorkoutSummaryMessage: Codable, Equatable, Sendable {
    var schemaVersion: Int // = 1
    var sessionId: String
    var startedAt: Date
    var endedAt: Date
    var durationSeconds: TimeInterval
    var averageHeartRateBpm: Int?
    var maximumHeartRateBpm: Int?
    var activeKilocalories: Double?
}
```

Encode as property-list compatible dictionary / JSON Data inside `transferUserInfo` under a stable key, e.g. `com.marcatos.SimPulse.workoutSummary`.

Unknown `schemaVersion` on iOS: log WARN, ignore (do not crash).

## Watch (WATCH-003)

### Ports

- `WorkoutSummaryOutbox`: `enqueue(_ message:)`, `pendingCount`, `flushIfPossible()`
- Optional: `CompanionConnectivity` for reachability observation (or fold into WC adapter)

### Adapters

- **FileOutbox:** Application Support directory, one JSON file per `sessionId` (overwrite = idempotent enqueue).
- **WatchConnectivitySummarySender:** activates `WCSession`, on `enqueue` also attempts `transferUserInfo`; on reachability / session activation, `flushIfPossible` re-sends pending files.
- **HealthKit finish hook:** capture `HKWorkout` from `finishWorkout()`, build message from workout stats (avg/max from builder statistics or workout statistics; kcal from totalEnergyBurned), then `enqueue`.

### Behavior

1. End workout → HealthKit save (existing) → build summary → enqueue.
2. If session reachable → `transferUserInfo`; on successful handoff to the system queue (no WC delivery ACK), remove from outbox.
3. If unreachable → leave on disk; flush when reachable again.
4. Companion disconnect during recording still does not stop the workout (WATCH-001).

## iPhone (IOS-004)

### Ports

- `WorkoutSummaryIngest`: `merge(_ message:) async throws` — idempotent by `sessionId`

### Adapters

- **WatchConnectivitySummaryReceiver:** `WCSessionDelegate` receives userInfo transfers; decode message; call ingest.
- **Ingest implementation (v1):**  
  - Record `sessionId` in a small durable set (UserDefaults or file) so duplicates are no-ops.  
  - Trigger session list reload (notification / callback / shared `SessionListViewModel` refresh).  
  - Do **not** invent HealthKit rows; HealthKit remains SoT for samples. Merge success = “seen + UI refresh.”

### Behavior

- Duplicate `sessionId` → no-op success.
- Malformed payload → log ERROR (no biometric fields), drop.
- App launch activates WCSession early (e.g. in `SimPulseApp`) so transfers are received.

## Testing

- Unit tests: encode/decode round-trip; outbox enqueue overwrite; ingest duplicate; ignore unknown schema.
- Mock WC session for Watch/iOS adapters where feasible; real WC remains Mac/device follow-up.
- Mac: regenerate Xcode project if new files; watchOS + iOS tests/build.
- Windows: Apple scripts NOT EXECUTED.

## Docs / tracker

- One design/plan covering both IDs; handoffs `WATCH-003.md` and `IOS-004.md` (or single handoff referencing both).
- Fix BACKLOG circular dependency notes: WATCH-003 no longer blocked on IOS-004 as prerequisite; paired delivery.
- Update CURRENT_STATE; Plane issues for both.
- No new ADR unless SwiftData is introduced (it is not in this slice).

## Acceptance mapping

| ID | Criterion | How met |
| --- | --- | --- |
| WATCH-003 | Workout saved in HealthKit | Existing `finishWorkout()` + capture UUID |
| WATCH-003 | Summary queued if iPhone absent | File outbox + transferUserInfo |
| WATCH-003 | Delivered later | Flush on reachability |
| IOS-004 | Idempotent merge by session ID | Ingest set + no-op duplicates |

## Implementation order (summary)

1. Claim Plane WATCH-003 + IOS-004; branch `feat/watch-ios-sync-summary`
2. Shared Codable message + tests
3. Watch outbox + finish hook + WC sender
4. iOS WC receiver + ingest + list refresh hook
5. Mac verify both targets, docs, PR
