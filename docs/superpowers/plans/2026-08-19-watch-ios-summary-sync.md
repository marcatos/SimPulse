# WATCH-003 + IOS-004 WatchConnectivity Summary Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After Watch `finishWorkout()`, enqueue a summary-only message, deliver via `transferUserInfo` (queued when iPhone absent), and idempotently ingest on iPhone by `sessionId` with session-list refresh.

**Architecture:** Shared Codable wire DTO; Watch file outbox + WC sender; iOS WC receiver + ingest port; HealthKit remains SoT for samples. No SwiftData in this slice.

**Tech Stack:** Swift / WatchConnectivity / HealthKit / XCTest / XcodeGen / simpulse-mac

## Global Constraints

- Branch: `feat/watch-ios-sync-summary` worktree from `main`
- Plane: claim **WATCH-003** and **IOS-004** In Progress; paired slice
- Payload: summary only — **no HR sample arrays** on the wire
- Do not log bpm or kilocalorie **values** (sessionId, counts, durations, errors OK)
- Shared sources under `apps/ios/SimPulse/Sync/` compiled into **both** SimPulse (iOS) and SimPulseWatch via `project.yml`
- Commit regenerated `SimPulse.xcodeproj` after XcodeGen on Mac
- Spec: `docs/superpowers/specs/2026-08-19-watch-ios-summary-sync-design.md`
- Conventional commits; executing this plan authorizes per-task commits

## File map

| Path | Role |
| --- | --- |
| `apps/ios/SimPulse/Sync/WatchWorkoutSummaryMessage.swift` | Codable DTO + userInfo key + encode/decode helpers |
| `apps/ios/SimPulse/Sync/WorkoutSummaryOutbox.swift` | Port + `FileWorkoutSummaryOutbox` |
| `apps/ios/SimPulse/Sync/WorkoutSummaryIngest.swift` | Port + `UserDefaultsWorkoutSummaryIngest` |
| `apps/watchos/SimPulseWatch/Adapters/WatchConnectivitySummarySender.swift` | WCSession sender + flush |
| `apps/ios/SimPulse/Sync/WatchConnectivitySummaryReceiver.swift` | WCSession receiver |
| `apps/watchos/.../HealthKitWatchWorkoutDataSource.swift` | Capture HKWorkout → enqueue |
| `apps/ios/SimPulse/SimPulseApp.swift` | Activate WC receiver at launch |
| `apps/ios/SimPulse/Sessions/SessionListViewModel.swift` | Refresh on ingest notification |
| `project.yml` | Watch target also sources `apps/ios/SimPulse/Sync` |
| `apps/ios/SimPulseTests/WatchWorkoutSummarySyncTests.swift` | Encode/outbox/ingest tests |
| Docs + handoffs | BACKLOG, CURRENT_STATE, WATCH-003.md, IOS-004.md |

---

### Task 1: Claim + worktree + design commit

**Files:** Plane; worktree; `docs/BACKLOG.md`; `docs/handoffs/WATCH-003.md`; `docs/handoffs/IOS-004.md`; design spec

**Interfaces:** Produces branch `feat/watch-ios-sync-summary`

- [ ] **Step 1:** Plane — set WATCH-003 (`b9b07c7c-…` lookup) and IOS-004 (`83228d43-…` lookup) to In Progress; comment paired branch

- [ ] **Step 2:** Worktree

```powershell
cd C:\Users\simot\Documents\Projects\SimPulse
git pull --ff-only origin main
git worktree add .worktrees\feat-watch-ios-sync-summary -b feat/watch-ios-sync-summary origin/main
```

Copy design spec into worktree if untracked on main.

- [ ] **Step 3:** BACKLOG — both IDs `IN_PROGRESS`; fix circular dep notes (WATCH-003 deps = WATCH-001 only for this slice; IOS-004 deps = WATCH-003 contract / paired)

- [ ] **Step 4:** Handoff stubs for both IDs

- [ ] **Step 5:** Commit `docs(sync): claim WATCH-003 and IOS-004 summary sync`

---

### Task 2: Shared message + outbox + ingest (TDD, no WC yet)

**Files:**
- Create Sync DTOs/ports/file+defaults adapters
- Modify `project.yml` Watch sources to include `apps/ios/SimPulse/Sync`
- Create `WatchWorkoutSummarySyncTests.swift`

**Interfaces:**

```swift
enum WatchWorkoutSummaryWire {
    static let schemaVersion = 1
    static let userInfoKey = "com.marcatos.SimPulse.workoutSummary"
}

struct WatchWorkoutSummaryMessage: Codable, Equatable, Sendable {
    var schemaVersion: Int
    var sessionId: String
    var startedAt: Date
    var endedAt: Date
    var durationSeconds: TimeInterval
    var averageHeartRateBpm: Int?
    var maximumHeartRateBpm: Int?
    var activeKilocalories: Double?
}

extension WatchWorkoutSummaryMessage {
    func makeUserInfo() throws -> [String: Any] // JSON Data under userInfoKey, or plist-safe dict
    static func fromUserInfo(_ userInfo: [String: Any]) throws -> WatchWorkoutSummaryMessage?
    // returns nil for unknown schemaVersion (after decode attempt) — or throw SchemaUnsupported
}

protocol WorkoutSummaryOutbox: Sendable {
    func enqueue(_ message: WatchWorkoutSummaryMessage) throws
    var pendingCount: Int { get }
    func pendingMessages() throws -> [WatchWorkoutSummaryMessage]
    func remove(sessionId: String) throws
}

final class FileWorkoutSummaryOutbox: WorkoutSummaryOutbox { /* Application Support/SimPulse/outbox/*.json */ }

protocol WorkoutSummaryIngest: Sendable {
    /// Returns true if newly merged, false if duplicate.
    func merge(_ message: WatchWorkoutSummaryMessage) throws -> Bool
}

final class UserDefaultsWorkoutSummaryIngest: WorkoutSummaryIngest {
    static let seenKey = "com.marcatos.SimPulse.seenWorkoutSummaryIds"
    // stores [String] of sessionIds; Notification.Name.simpulseWorkoutSummaryMerged posted on new merge
}

extension Notification.Name {
    static let simpulseWorkoutSummaryMerged = Notification.Name("com.marcatos.SimPulse.workoutSummaryMerged")
}
```

- [ ] **Step 1: Failing tests**

```swift
func testMessageRoundTripPreservesFields() throws { ... }
func testUnknownSchemaVersionReturnsNil() throws { ... }
func testOutboxEnqueueIsIdempotentBySessionId() throws { ... }
func testIngestDuplicateReturnsFalse() throws { ... }
func testIngestNewPostsNotification() throws { ... }
```

Use `FileManager` temporary directory for outbox tests; suite `UserDefaults` for ingest.

- [ ] **Step 2:** Implement until Mac `test-ios.sh` green (new tests + prior suite)

- [ ] **Step 3:** Commit `test(sync): add workout summary wire and outbox ingest contracts`

---

### Task 3: Watch finish hook + WC sender (WATCH-003)

**Files:**
- `WatchConnectivitySummarySender.swift` (watchOS)
- Modify `HealthKitWatchWorkoutDataSource.stop`
- Wire sender from Watch app entry / `WorkoutViewModel.live()`

**Interfaces:**

```swift
final class WatchConnectivitySummarySender: NSObject, WCSessionDelegate {
    init(outbox: WorkoutSummaryOutbox, session: WCSession = .default)
    func start() // activate session
    func enqueueAndTransfer(_ message: WatchWorkoutSummaryMessage) throws
    func flushIfPossible() // transfer all pending
}
```

- [ ] **Step 1:** Capture workout on finish:

```swift
let finished = try await workoutBuilder.finishWorkout()
// build WatchWorkoutSummaryMessage from finished workout + lastSnapshot metrics if stats missing
try outboxSender.enqueueAndTransfer(message)
```

Avg/max: prefer `lastSnapshot.averageHeartRateBpm` / `maximumHeartRateBpm` (Int) available in-memory; kcal from `finished?.totalEnergyBurned` or snapshot. `sessionId` = `finished.uuid.uuidString`. If `finished` is nil, log ERROR and skip enqueue (HealthKit save failed).

- [ ] **Step 2:** On `sessionReachabilityDidChange` / activation, `flushIfPossible`. After successful `transferUserInfo`, `outbox.remove(sessionId:)` — note: transferUserInfo queues with the system; remove when transfer is handed off (Apple queues it) to avoid infinite re-send; keep file until transferUserInfo succeeds without throw. Document that system may still deliver later.

- [ ] **Step 3:** Inject outbox/sender into `HealthKitWatchWorkoutDataSource` via initializer (default live wiring) so tests can use mock outbox without WC.

- [ ] **Step 4:** Mac `build-watch.sh` + iOS tests still green; commit pbxproj

- [ ] **Step 5:** Commit `feat(watch): queue and transfer workout summaries to iPhone`

---

### Task 4: iOS WC receiver + list refresh (IOS-004)

**Files:**
- `WatchConnectivitySummaryReceiver.swift`
- `SimPulseApp.swift`
- `SessionListViewModel.swift` (observe notification → `load()`)

**Interfaces:**

```swift
final class WatchConnectivitySummaryReceiver: NSObject, WCSessionDelegate {
    init(ingest: WorkoutSummaryIngest, session: WCSession = .default)
    func start()
    // didReceiveUserInfo → decode → merge
}
```

- [ ] **Step 1:** Implement receiver; ignore non-matching keys; unknown schema → WARN log, drop

- [ ] **Step 2:** `SimPulseApp` creates/starts receiver (store as `@StateObject` or unmanaged singleton held by App)

```swift
@StateObject private var summaryReceiver = WatchConnectivitySummaryReceiver.live()
// .onAppear { summaryReceiver.start() }
```

- [ ] **Step 3:** `SessionListViewModel` listens for `.simpulseWorkoutSummaryMerged` and calls `load()` (MainActor)

- [ ] **Step 4:** Unit test ingest path already covered; optional decode-from-userInfo test

- [ ] **Step 5:** Mac `test-ios.sh` + `build-ios.sh` + `build-watch.sh`; regenerate Xcode project

- [ ] **Step 6:** Commit `feat(ios): ingest WatchConnectivity workout summaries`

---

### Task 5: Docs + Plane

- [ ] Fill `docs/handoffs/WATCH-003.md` and `docs/handoffs/IOS-004.md`
- [ ] BACKLOG both → DONE (or IN_PROGRESS until merge — prefer DONE after Mac verify, Plane Done at merge)
- [ ] CURRENT_STATE: sync shipped on branch; next Bridge KI-002/KI-003 or PROTO-003
- [ ] Plane comments with Mac results
- [ ] Commit `docs(sync): record WATCH-003 and IOS-004 summary sync`
- [ ] PR when user asks — title `feat(sync): WatchConnectivity workout summary queue and ingest`

---

## Spec coverage

| Spec item | Task |
| --- | --- |
| Shared summary DTO schema v1 | 2 |
| File outbox + idempotent enqueue | 2–3 |
| transferUserInfo + flush | 3 |
| Capture HKWorkout UUID on finish | 3 |
| iOS ingest by sessionId | 2, 4 |
| List refresh | 4 |
| No HR series / no biometric logs | all |
| Docs / BACKLOG circular fix | 1, 5 |

## Self-review notes

- Removing outbox entries when `transferUserInfo` accepts the dictionary is correct for “handed to system queue”; do not wait for iPhone ACK (WC has no delivery receipt for transferUserInfo).
- Real paired-device WC remains device/Mac follow-up; unit tests use file/UserDefaults only.
