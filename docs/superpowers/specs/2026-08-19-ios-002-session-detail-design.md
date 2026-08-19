# IOS-002 — Session detail and charts

**Date:** 2026-08-19  
**Status:** Approved for planning (pending user review of this file)  
**Backlog:** IOS-002  
**Depends on:** IOS-001 (merged), ANALYTICS-001 (DONE), IOS-003 (merged)

## Goal

From the session list, open a session detail screen that shows duration, heart-rate average/max, active calories, and an HR-over-time chart built with Swift Charts from stored HealthKit (or mock) samples.

## Non-goals

- Race report UI (IOS-007) or lap/event windows (ANALYTICS-003)
- Swift port of the full C# analytics package
- SwiftData persistence (ADR 0002 — HealthKit remains source of truth)
- WatchConnectivity sync (WATCH-003 / IOS-004)
- Distinguishing HealthKit read denial from “no HR samples” beyond an honest empty chart/metrics
- Logging HR bpm values or other biometric payloads
- Medical wording or zones

## Context

- List exists via `SessionRepository.listSessions()` → `SessionListView`. Rows are not tappable yet.
- `HealthKitSessionRepository` maps workouts with metadata `com.marcatos.SimPulse.activity = "Sim Racing"` but leaves avg/max HR `nil`.
- Watch persists workouts with `HKLiveWorkoutBuilder` (activity type `.other`, same metadata); HR samples are associated HealthKit quantity samples for the workout interval.
- iOS deployment target is 17.0 — Swift Charts is available.
- Chart choice: **Swift Charts** (user-approved option A).
- Architecture choice: **extend `SessionRepository`** with `sessionDetail(id:)` (user-approved approach 1).

## Architecture

```text
SessionListView
  → NavigationLink → SessionDetailView
       → SessionDetailViewModel
            → SessionRepository.sessionDetail(id:)
                 → MockSessionRepository | HealthKitSessionRepository
```

### Domain DTOs

```swift
struct HeartRatePoint: Equatable, Sendable, Identifiable {
    var id: Date { timestamp }
    let timestamp: Date
    let beatsPerMinute: Double
}

struct SessionDetail: Equatable, Sendable, Identifiable {
    let id: String
    let startedAt: Date
    let duration: TimeInterval
    let averageHeartRateBpm: Int?
    let maximumHeartRateBpm: Int?
    let activeKilocalories: Double?
    let heartRatePoints: [HeartRatePoint]
    let source: SessionSource
}
```

### Port

Extend `SessionRepository`:

```swift
func sessionDetail(id: String) async throws -> SessionDetail?
```

- Returns `nil` if the id is unknown / not a Sim Racing workout.
- Throws only for unexpected failures the UI should surface as a load error; “no HR samples” is a successful detail with empty `heartRatePoints` and nil metrics as appropriate.

### HealthKit adapter

1. Resolve workout by UUID (`id` from list = `workout.uuid.uuidString`).
2. Confirm Sim Racing metadata (same key/value as list); else `nil`.
3. Query `HKQuantityType(.heartRate)` samples with predicate for the workout’s start…end interval (and/or workout association if straightforward).
4. Map to `[HeartRatePoint]` sorted by timestamp ascending.
5. Metrics:
   - `averageHeartRateBpm` / `maximumHeartRateBpm` from the sample set (simple mean / max). If no samples → `nil`.
   - `activeKilocalories` from `workout.totalEnergyBurned` (same as list).
6. Do not invent HR from unreliable workout metadata when samples are empty.

Optional improvement in the same task (if cheap): when listing, leave HR nil (status quo) — detail is the place that loads samples. Do not block IOS-002 on enriching list-row HR.

### Mock adapter

`MockSessionRepository` implements `sessionDetail` for known mock ids with a short synthetic HR series so Previews and tests need no HealthKit.

### Application / UI

- `SessionDetailViewModel`: load by id; publish detail / loading / errorText.
- `SessionDetailView`: header (start date, duration, AVG / MAX / KCAL) + Swift Charts `LineMark` (x: time, y: bpm). Empty chart: `ContentUnavailableView` or inline “No heart rate samples for this session.”
- `SessionListView`: `NavigationLink(value:)` or equivalent to push detail by session id; keep auth/list behavior from IOS-003.

Presentation helpers (pure, testable) for duration and metric strings — reuse or extend `SessionListRowPresentation` patterns; do not log bpm values.

### Testing

- Unit tests: mock detail with samples → metrics and point count; unknown id → nil; empty samples → nil avg/max + empty points.
- Presentation formatting tests for detail header strings.
- Mac: `xcodegen generate` if new files; `scripts/test-ios.sh` + `build-ios.sh`; commit regenerated `project.pbxproj`.
- Windows: Apple scripts NOT EXECUTED.

### Docs / tracker

- Plane IOS-002 → In Progress before coding; Done after merge.
- Update `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/handoffs/IOS-002.md`.
- No new ADR unless storage model changes (it should not).

## Acceptance mapping

| Criterion | How met |
| --- | --- |
| Duration | From workout / summary |
| HR avg/max | Computed from stored HR samples (or nil if none) |
| Calories | From workout energy |
| HR over time | Swift Charts from `heartRatePoints` |

## Implementation order (summary)

1. Claim Plane + worktree `feat/ios-002-session-detail`
2. DTOs + port method + mock + failing tests
3. HealthKit `sessionDetail`
4. ViewModel + DetailView + NavigationLink + Charts
5. Mac verify, regenerate Xcode project, docs, PR
