# IOS-002 Session Detail and Charts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add session detail with duration, HR avg/max, calories, and a Swift Charts HR-over-time line from stored samples via `SessionRepository.sessionDetail(id:)`.

**Architecture:** Extend the existing hexagonal `SessionRepository` port with `sessionDetail`. HealthKit adapter loads the workout by UUID, confirms Sim Racing metadata, queries HR quantity samples in the workout interval, and computes avg/max from samples. UI: `NavigationLink` from list → `SessionDetailView` + Swift Charts.

**Tech Stack:** Swift 5 / SwiftUI / Swift Charts / HealthKit / XCTest / XcodeGen / partner Mac `simpulse-mac`

## Global Constraints

- Branch/worktree: `feat/ios-002-session-detail` from current `main`
- Plane SPULS work item IOS-002 — claim In Progress before coding; comments for notes (no secrets)
- Do not log HR bpm values or biometric payloads (counts/durations/errors only)
- Do not hand-edit `project.pbxproj` as source of truth — change sources + `project.yml` if needed, `xcodegen generate` on Mac, **commit regenerated project**
- iOS 17+; Swift Charts OK
- Avg/max HR types: `Int?` (match `SessionSummary`); sample points use `Double` bpm from HealthKit
- Spec: `docs/superpowers/specs/2026-08-19-ios-002-session-detail-design.md`
- Conventional commits; executing this plan authorizes per-task commits

## File map

| Path | Role |
| --- | --- |
| `apps/ios/SimPulse/Sessions/SessionDetail.swift` | `HeartRatePoint` + `SessionDetail` DTOs |
| `apps/ios/SimPulse/Sessions/SessionRepository.swift` | Add `sessionDetail`; mock implementation |
| `apps/ios/SimPulse/Sessions/HealthKitSessionRepository.swift` | HealthKit detail + HR query |
| `apps/ios/SimPulse/Sessions/SessionDetailPresentation.swift` | Header string formatting (testable) |
| `apps/ios/SimPulse/Sessions/SessionDetailViewModel.swift` | Load detail by id |
| `apps/ios/SimPulse/SessionDetailView.swift` | Metrics + Chart UI |
| `apps/ios/SimPulse/SessionListView.swift` | NavigationLink to detail |
| `apps/ios/SimPulseTests/SessionDetailTests.swift` | Repository + presentation tests |
| `SimPulse.xcodeproj/project.pbxproj` | Regenerated on Mac |
| `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/handoffs/IOS-002.md` | Tracker |

---

### Task 1: Claim work + worktree + design commit

**Files:**
- Create: worktree; `docs/handoffs/IOS-002.md` stub
- Modify: `docs/BACKLOG.md` (IOS-002 → `IN_PROGRESS`)
- Include: `docs/superpowers/specs/2026-08-19-ios-002-session-detail-design.md` (may be untracked on main)

**Interfaces:**
- Consumes: approved design spec
- Produces: branch `feat/ios-002-session-detail` in `.worktrees/feat-ios-002-session-detail`

- [ ] **Step 1: Plane** — set IOS-002 (`4eb5de47-…` look up via workitem list if needed) to In Progress; comment branch name

- [ ] **Step 2: Worktree**

```powershell
cd C:\Users\simot\Documents\Projects\SimPulse
git fetch origin
git pull --ff-only origin main
git worktree add .worktrees\feat-ios-002-session-detail -b feat/ios-002-session-detail origin/main
```

Move agent root to the worktree. Copy or ensure design spec is present.

- [ ] **Step 3: Docs claim** — BACKLOG `IN_PROGRESS`; handoff stub

- [ ] **Step 4: Commit**

```text
docs(ios): claim IOS-002 session detail and charts
```

Include design spec + BACKLOG + handoff.

---

### Task 2: DTOs + port + mock + failing tests (TDD)

**Files:**
- Create: `apps/ios/SimPulse/Sessions/SessionDetail.swift`
- Modify: `apps/ios/SimPulse/Sessions/SessionRepository.swift`
- Create: `apps/ios/SimPulseTests/SessionDetailTests.swift`
- Stub: `HealthKitSessionRepository.sessionDetail` returning `nil` until Task 3 (so protocol compiles)

**Interfaces:**
- Produces:

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

protocol SessionRepository: Sendable {
    func listSessions() async throws -> [SessionSummary]
    func sessionDetail(id: String) async throws -> SessionDetail?
}
```

- [ ] **Step 1: Write failing tests** in `SessionDetailTests.swift`:

```swift
import XCTest
@testable import SimPulse

final class SessionDetailTests: XCTestCase {
    func testMockDetailReturnsSortedPointsAndMetrics() async throws {
        let repo = MockSessionRepository()
        let detail = try await repo.sessionDetail(id: "mock-1")
        XCTAssertNotNil(detail)
        XCTAssertEqual(detail?.id, "mock-1")
        XCTAssertEqual(detail?.averageHeartRateBpm, 132)
        XCTAssertEqual(detail?.maximumHeartRateBpm, 161)
        XCTAssertFalse(detail!.heartRatePoints.isEmpty)
        let times = detail!.heartRatePoints.map(\.timestamp)
        XCTAssertEqual(times, times.sorted())
    }

    func testMockDetailUnknownIdReturnsNil() async throws {
        let repo = MockSessionRepository()
        let detail = try await repo.sessionDetail(id: "missing")
        XCTAssertNil(detail)
    }

    func testMockDetailEmptySamplesHasNilHeartRateMetrics() async throws {
        // Construct a MockSessionRepository that can return a detail with empty points
        // OR add MockSessionRepository(details:) / special id "mock-empty-hr"
        // Prefer: extend mock with optional details override in init for tests.
    }
}
```

Implement the empty-samples case by adding to mock:

```swift
// In MockSessionRepository — optional custom details dictionary keyed by id;
// default details derived from sampleSessions with synthetic HR series.
```

Synthetic series for `mock-1`: e.g. 5 points over the session with bpm values whose rounded mean ≈ 132 and max 161 (or set avg/max explicitly on the detail and use points only for chart — **prefer compute avg/max from points in a shared pure helper** so mock and HealthKit share logic):

```swift
enum HeartRateMetricsCalculator {
    static func averageBpm(_ points: [HeartRatePoint]) -> Int? { ... }
    static func maximumBpm(_ points: [HeartRatePoint]) -> Int? { ... }
}
```

Put calculator in `SessionDetail.swift` or small `HeartRateMetricsCalculator.swift`. Tests:

```swift
func testMetricsCalculatorAverageAndMax() {
    let points = [
        HeartRatePoint(timestamp: Date(timeIntervalSince1970: 1), beatsPerMinute: 100),
        HeartRatePoint(timestamp: Date(timeIntervalSince1970: 2), beatsPerMinute: 140),
    ]
    XCTAssertEqual(HeartRateMetricsCalculator.averageBpm(points), 120)
    XCTAssertEqual(HeartRateMetricsCalculator.maximumBpm(points), 140)
    XCTAssertNil(HeartRateMetricsCalculator.averageBpm([]))
}
```

- [ ] **Step 2: Mac or local compile — expect fail** (missing types / method)

On Mac after sync:

```bash
export PATH="/opt/homebrew/bin:/opt/homebrew/Cellar/xcodegen/2.44.1/bin:$PATH"
cd /Users/simonemarcato/Projects/SimPulse
# sync worktree files first
xcodegen generate
bash scripts/test-ios.sh
```

Expected: compile failure.

- [ ] **Step 3: Implement DTOs + calculator + protocol + MockSessionRepository.sessionDetail + HealthKit stub `return nil`**

Mock `sessionDetail`:

- Find summary by id in `sessions`; if missing return nil
- Build synthetic points (at least 3) spanning `startedAt .. startedAt+duration`
- Set avg/max via calculator from those points (adjust sample bpm so they match listed 132/161 as closely as practical, **or** keep list summary metrics and chart points independent — prefer calculator from points and update mock summary HR to match calculated values for consistency)

- [ ] **Step 4: Tests green on Mac**

- [ ] **Step 5: Commit**

```text
test(ios): add session detail repository contract tests
```

---

### Task 3: HealthKit `sessionDetail`

**Files:**
- Modify: `apps/ios/SimPulse/Sessions/HealthKitSessionRepository.swift`
- Test: optional — cannot hit real HK in unit tests; keep stub behavior covered by protocol via mock only. Add pure mapping test if you extract `mapHeartRateSamples` as package-visible/static test helper.

**Interfaces:**
- Consumes: `SessionDetail`, `HeartRateMetricsCalculator`
- Produces: real HealthKit implementation of `sessionDetail`

- [ ] **Step 1: Implement**

```swift
func sessionDetail(id: String) async throws -> SessionDetail? {
    guard HKHealthStore.isHealthDataAvailable() else { return nil }
    guard let uuid = UUID(uuidString: id) else { return nil }
    guard let workout = try await fetchWorkout(uuid: uuid) else { return nil }
    guard isSimRacing(workout) else { return nil }
    let samples = try await fetchHeartRateSamples(from: workout.startDate, to: workout.endDate)
    let points = samples.map { ... }.sorted { $0.timestamp < $1.timestamp }
    let energy = workout.totalEnergyBurned?.doubleValue(for: .kilocalorie())
    return SessionDetail(
        id: workout.uuid.uuidString,
        startedAt: workout.startDate,
        duration: workout.duration,
        averageHeartRateBpm: HeartRateMetricsCalculator.averageBpm(points),
        maximumHeartRateBpm: HeartRateMetricsCalculator.maximumBpm(points),
        activeKilocalories: energy,
        heartRatePoints: points,
        source: .healthKit
    )
}
```

Fetch workout: `HKSampleQuery` with `HKQuery.predicateForObject(with: uuid)` on workout type, limit 1.

HR query: `HKQuantityType(.heartRate)`, predicate `HKQuery.predicateForSamples(withStart:end:options:)` strict start/end, unit `.count()/min()` or `HKUnit.count().unitDivided(by: .minute())`.

On query errors: log ERROR (description only), return nil or rethrow — prefer **return nil** for missing workout; **throw** only if you want UI error — match list pattern: log and return nil for soft failures, throw only unexpected. Spec: unknown → nil; empty samples → successful detail with nil metrics.

Log: `listed N hr samples for session` with count only — never bpm values.

- [ ] **Step 2: Mac build + full test suite still green**

- [ ] **Step 3: Commit**

```text
feat(ios): load session heart rate samples from HealthKit
```

---

### Task 4: Detail ViewModel + SwiftUI + Charts + navigation

**Files:**
- Create: `SessionDetailPresentation.swift`, `SessionDetailViewModel.swift`, `SessionDetailView.swift`
- Modify: `SessionListView.swift`
- Test: presentation formatting in `SessionDetailTests.swift`

**Interfaces:**
- Consumes: `SessionRepository.sessionDetail`
- Produces: navigable detail UI

- [ ] **Step 1: Presentation + tests**

```swift
struct SessionDetailPresentation: Equatable {
    var titleText: String
    var durationText: String
    var averageHeartRateText: String
    var maximumHeartRateText: String
    var caloriesText: String
    var hasHeartRateChart: Bool

    static func from(_ detail: SessionDetail) -> SessionDetailPresentation { ... }
}
```

Reuse duration/bpm/kcal formatting logic — extract shared private helpers to a small `SessionFormatting` enum used by list row + detail, **or** duplicate the three formatters into detail presentation (YAGNI: duplicate is OK for ≤15 lines; prefer extract if touching both).

- [ ] **Step 2: ViewModel**

```swift
@MainActor
final class SessionDetailViewModel: ObservableObject {
    @Published private(set) var detail: SessionDetail?
    @Published private(set) var isLoading = false
    @Published private(set) var errorText: String?

    private let sessionId: String
    private let repository: SessionRepository

    init(sessionId: String, repository: SessionRepository) { ... }

    func load() async {
        isLoading = true
        errorText = nil
        do {
            detail = try await repository.sessionDetail(id: sessionId)
            if detail == nil {
                errorText = "Session not found."
            }
        } catch {
            detail = nil
            errorText = "Could not load session."
            // log error description only
        }
        isLoading = false
    }
}
```

Pass the **same repository instance** from the list (inject via `SessionListViewModel` exposing `repository` is leaky). Prefer:

```swift
// SessionListView
NavigationLink(value: session.id) { row }
.navigationDestination(for: String.self) { id in
    SessionDetailView(model: SessionDetailViewModel(sessionId: id, repository: model.repository))
}
```

Add `var repository: SessionRepository { repository }` as internal/read-only on list VM, or pass repository into `SessionListView` alongside model. Cleanest: store repository on list VM as `let repository` already private — add `fileprivate`/`internal` accessor:

```swift
var sessionsRepository: SessionRepository { repository }
```

- [ ] **Step 3: SessionDetailView**

```swift
import Charts
import SwiftUI

struct SessionDetailView: View {
    @ObservedObject var model: SessionDetailViewModel

    var body: some View {
        Group {
            if model.isLoading && model.detail == nil {
                ProgressView("Loading session…")
            } else if let detail = model.detail {
                let presentation = SessionDetailPresentation.from(detail)
                ScrollView {
                    // metrics header AVG MAX KCAL + duration
                    if presentation.hasHeartRateChart {
                        Chart(detail.heartRatePoints) { point in
                            LineMark(
                                x: .value("Time", point.timestamp),
                                y: .value("BPM", point.beatsPerMinute)
                            )
                        }
                        .frame(height: 220)
                        .accessibilityLabel("Heart rate over time")
                    } else {
                        ContentUnavailableView(
                            "No heart rate samples",
                            systemImage: "heart.slash",
                            description: Text("This session has no heart rate data in Health.")
                        )
                    }
                }
            } else {
                ContentUnavailableView(
                    "Session unavailable",
                    systemImage: "flag.checkered",
                    description: Text(model.errorText ?? "Session not found.")
                )
            }
        }
        .navigationTitle("Session")
        .navigationBarTitleDisplayMode(.inline)
        .task { await model.load() }
    }
}
```

Do not display raw bpm lists in logs. Chart y-axis can show bpm visually (UI is fine; logging is not).

- [ ] **Step 4: Wire NavigationLink in SessionListView** as above; Previews updated.

- [ ] **Step 5: Mac** — `xcodegen generate`, `test-ios.sh`, `build-ios.sh`; **commit pbxproj**

```text
feat(ios): show session detail with heart rate chart
```

(If pbxproj is large, same commit is fine; or split `chore(ios): regenerate Xcode project for IOS-002`.)

---

### Task 5: Docs + Plane

**Files:** handoff, BACKLOG DONE, CURRENT_STATE, optional KNOWN_ISSUES note if empty HR is confusing

- [ ] **Step 1: Fill `docs/handoffs/IOS-002.md`**

- [ ] **Step 2: BACKLOG IOS-002 → DONE**; CURRENT_STATE next = WATCH-003/IOS-004 or Bridge KI

- [ ] **Step 3: Plane comment** with Mac test counts; keep In Progress until merge

- [ ] **Step 4: Commit**

```text
docs(ios): record IOS-002 session detail and charts
```

- [ ] **Step 5: PR when user asks** — title `feat(ios): show session detail with heart rate chart`

---

## Spec coverage

| Spec item | Task |
| --- | --- |
| DTOs + port `sessionDetail` | 2 |
| Mock + calculator | 2 |
| HealthKit workout + HR query | 3 |
| Swift Charts detail UI + navigation | 4 |
| No biometric logging | 3–4 |
| Mac verify + committed XcodeGen | 4–5 |
| Docs / Plane | 1, 5 |

## Self-review notes

- Spec used `Double?` for avg/max; plan standardizes on `Int?` to match `SessionSummary` (rounded from sample mean/max).
- Empty HR samples → successful detail, nil metrics, empty chart state — not an error.
- Share types already requested in IOS-003 include heartRate read.
