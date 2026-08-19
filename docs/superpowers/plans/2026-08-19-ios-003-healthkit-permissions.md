# IOS-003 HealthKit Permissions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On first session-list load, request HealthKit access once, ship the iOS HealthKit entitlement, and show an honest empty state with an Open Settings CTA when the list is empty after the prompt.

**Architecture:** Hexagonal port `HealthAuthorization` + `HealthKitHealthAuthorization` adapter; `SessionListViewModel` calls authorize-then-list; UI uses `SessionListEmptyReason` for copy/CTA. No SwiftData. Watch remains the workout writer.

**Tech Stack:** Swift 5 / SwiftUI / HealthKit / XCTest / XcodeGen (`project.yml`) / partner Mac `simpulse-mac` for `xcodebuild`

## Global Constraints

- Worktree branch: `feat/ios-003-healthkit-permissions` from current `main`
- Plane project SPULS: create/claim IOS-003 work item before coding; comments for session notes (no secrets)
- Do not log HR, energy, or raw biometric payloads
- Do not hand-edit `SimPulse.xcodeproj` as source of truth — change `project.yml`, regenerate with XcodeGen on Mac
- Conventional commits; commit only when the user asks (or at task commit steps if user already approved shipping this plan end-to-end)
- Windows: Apple scripts remain NOT EXECUTED; record Mac results in CURRENT_STATE / handoff
- Spec: `docs/superpowers/specs/2026-08-19-ios-003-healthkit-permissions-design.md`

## File map

| Path | Role |
| --- | --- |
| `apps/ios/SimPulse/Sessions/HealthAuthorization.swift` | Port + `SessionListEmptyReason` + `MockHealthAuthorization` |
| `apps/ios/SimPulse/Sessions/HealthKitHealthAuthorization.swift` | HealthKit adapter + UserDefaults prompted flag |
| `apps/ios/SimPulse/Sessions/SessionListViewModel.swift` | Authorize → list → emptyReason |
| `apps/ios/SimPulse/SessionListView.swift` | Empty UI + Open Settings |
| `apps/ios/SimPulse/SimPulse.entitlements` | `com.apple.developer.healthkit` |
| `project.yml` | `CODE_SIGN_ENTITLEMENTS` + usage-string tweak |
| `apps/ios/SimPulseTests/HealthAuthorizationTests.swift` | ViewModel / mock auth tests |
| `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/handoffs/IOS-003.md` | Tracker docs |

---

### Task 1: Claim work + worktree

**Files:**
- Create: Plane work item (MCP/CLI); git worktree
- Modify: `docs/BACKLOG.md` (IOS-003 → `IN_PROGRESS`); `docs/handoffs/IOS-003.md` (stub)
- Test: none yet

**Interfaces:**
- Consumes: design spec above
- Produces: branch `feat/ios-003-healthkit-permissions` checked out in a worktree; Plane item In Progress

- [ ] **Step 1: List open Plane items for SPULS; create IOS-003 if missing**

Use Plane MCP `workitem` on project `5de718ee-c465-4756-bc10-92ddf8e82604`, or:

```powershell
python C:\Users\simot\Documents\Projects\opnsense-server\scripts\plane_issue.py create --project SPULS --title "IOS-003 HealthKit permissions"
```

Set state to In Progress. Comment that work starts on `feat/ios-003-healthkit-permissions`.

- [ ] **Step 2: Create worktree from main**

From primary checkout `C:\Users\simot\Documents\Projects\SimPulse` (on `main`, clean of unrelated WIP):

```powershell
git fetch origin
git pull --ff-only origin main
git worktree add .worktrees\feat-ios-003-healthkit-permissions -b feat/ios-003-healthkit-permissions origin/main
```

Move the Cursor agent root to that worktree (`move_agent_to_root`).

- [ ] **Step 3: Claim in docs**

Set IOS-003 status to `IN_PROGRESS` in `docs/BACKLOG.md`. Write `docs/handoffs/IOS-003.md` with Task/Goal/Status=`IN_PROGRESS` and empty Files changed for now.

- [ ] **Step 4: Commit claim docs only if user asked to commit; otherwise continue**

Message if committing: `docs(ios): claim IOS-003 HealthKit permissions`

---

### Task 2: Port + failing ViewModel tests (TDD)

**Files:**
- Create: `apps/ios/SimPulse/Sessions/HealthAuthorization.swift`
- Create: `apps/ios/SimPulseTests/HealthAuthorizationTests.swift`
- Modify: `apps/ios/SimPulse/Sessions/SessionListViewModel.swift` (minimal stubs only if needed to compile — prefer RED on missing symbols first)
- Test: `apps/ios/SimPulseTests/HealthAuthorizationTests.swift`

**Interfaces:**
- Consumes: `SessionRepository` / `MockSessionRepository` (existing)
- Produces:

```swift
enum SessionListEmptyReason: Equatable {
    case needsHealthAccess
    case healthUnavailable
}

protocol HealthAuthorization: Sendable {
    var hasPrompted: Bool { get }
    var isAvailable: Bool { get }
    func requestAccessIfNeeded() async throws
}

final class MockHealthAuthorization: HealthAuthorization, @unchecked Sendable {
    private(set) var requestCallCount = 0
    private var prompted: Bool
    var isAvailable: Bool
    var throwOnRequest: Error?

    init(hasPrompted: Bool = false, isAvailable: Bool = true) { ... }

    var hasPrompted: Bool { prompted }

    func requestAccessIfNeeded() async throws {
        requestCallCount += 1
        if let throwOnRequest { throw throwOnRequest }
        guard isAvailable else { prompted = true; return }
        prompted = true
    }
}
```

- [ ] **Step 1: Write failing tests**

Create `apps/ios/SimPulseTests/HealthAuthorizationTests.swift`:

```swift
import XCTest
@testable import SimPulse

@MainActor
final class HealthAuthorizationTests: XCTestCase {
    func testLoadRequestsAccessOnlyOnce() async {
        let auth = MockHealthAuthorization(hasPrompted: false)
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()
        await model.load()

        XCTAssertEqual(auth.requestCallCount, 2)
        XCTAssertTrue(auth.hasPrompted)
        // Mock increments every call; ViewModel must still call requestAccessIfNeeded each load,
        // and the mock/live adapter no-ops the HealthKit sheet when hasPrompted.
        // Strengthen: use a mock that only increments when !hasPrompted:
    }

    func testLoadRequestsHealthKitSheetOnlyWhileNotPrompted() async {
        let auth = MockHealthAuthorization(hasPrompted: false)
        auth.countOnlyUnpromptedRequests = true
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()
        await model.load()

        XCTAssertEqual(auth.requestCallCount, 1)
        XCTAssertEqual(model.emptyReason, .needsHealthAccess)
        XCTAssertTrue(model.sessions.isEmpty)
    }

    func testLoadWithSessionsClearsEmptyReason() async {
        let auth = MockHealthAuthorization(hasPrompted: true)
        let model = SessionListViewModel(
            repository: MockSessionRepository(),
            authorization: auth
        )

        await model.load()

        XCTAssertFalse(model.sessions.isEmpty)
        XCTAssertNil(model.emptyReason)
    }

    func testUnavailableSetsHealthUnavailableEmptyReason() async {
        let auth = MockHealthAuthorization(hasPrompted: false, isAvailable: false)
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()

        XCTAssertEqual(model.emptyReason, .healthUnavailable)
    }
}
```

Implement `MockHealthAuthorization` so `countOnlyUnpromptedRequests` only increments when a real prompt would run (`!prompted` before the call), matching live adapter behavior.

- [ ] **Step 2: Run tests on Mac (expect fail / compile fail)**

Sync tree to `simpulse-mac` if needed, then:

```bash
export PATH="/opt/homebrew/bin:/opt/homebrew/Cellar/xcodegen/2.44.1/bin:$PATH"
cd /Users/simonemarcato/Projects/SimPulse
xcodegen generate
bash scripts/test-ios.sh
```

Expected: compile error or failing tests for missing `HealthAuthorization` / `emptyReason` / new `SessionListViewModel` initializer.

- [ ] **Step 3: Add port + mock types**

Create `apps/ios/SimPulse/Sessions/HealthAuthorization.swift` with the enum, protocol, and `MockHealthAuthorization` as in Interfaces (include `countOnlyUnpromptedRequests` for tests).

- [ ] **Step 4: Minimal ViewModel changes so tests can link**

Update `SessionListViewModel` to take `authorization: HealthAuthorization`, publish `emptyReason`, call `requestAccessIfNeeded()` then `listSessions()`, set:

- `emptyReason = nil` if `!sessions.isEmpty`
- else if `!authorization.isAvailable` → `.healthUnavailable`
- else → `.needsHealthAccess`

Keep `errorText` for repository failures (`Could not load sessions.`). On auth throw: log ERROR (description only), mark flow as prompted if the mock/adapter already did, still load list.

Update `live()`:

```swift
static func live() -> SessionListViewModel {
    #if DEBUG
    if ProcessInfo.processInfo.arguments.contains("--simpulse-preview-sessions") {
        return SessionListViewModel(
            repository: MockSessionRepository(),
            authorization: MockHealthAuthorization(hasPrompted: true)
        )
    }
    #endif
    return SessionListViewModel(
        repository: HealthKitSessionRepository(),
        authorization: HealthKitHealthAuthorization()
    )
}
```

(`HealthKitHealthAuthorization` may be a stub that compiles until Task 3.)

- [ ] **Step 5: Re-run tests on Mac — expect PASS for Task 2 tests**

- [ ] **Step 6: Commit** (when user allows)

```text
test(ios): cover session list HealthKit auth prompting
```

---

### Task 3: HealthKit adapter + entitlements

**Files:**
- Create: `apps/ios/SimPulse/Sessions/HealthKitHealthAuthorization.swift`
- Create: `apps/ios/SimPulse/SimPulse.entitlements`
- Modify: `project.yml` (iOS target `CODE_SIGN_ENTITLEMENTS` + usage strings)
- Test: existing + Task 2 tests still pass (adapter covered indirectly; optional thin test of UserDefaults key via injectable defaults)

**Interfaces:**
- Consumes: `HealthAuthorization`
- Produces: `HealthKitHealthAuthorization` matching Watch type sets

- [ ] **Step 1: Write entitlements file**

`apps/ios/SimPulse/SimPulse.entitlements` — same shape as Watch:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>com.apple.developer.healthkit</key>
	<true/>
</dict>
</plist>
```

- [ ] **Step 2: Update `project.yml` iOS target settings**

Under `SimPulse` → `settings.base`, add:

```yaml
CODE_SIGN_ENTITLEMENTS: apps/ios/SimPulse/SimPulse.entitlements
INFOPLIST_KEY_NSHealthShareUsageDescription: SimPulse reads heart rate, energy, and past Sim Racing workouts from Health. Not a medical device.
INFOPLIST_KEY_NSHealthUpdateUsageDescription: SimPulse may save Sim Racing workout data to Health on this iPhone. Not a medical device.
```

(Replace the existing two INFOPLIST Health keys with these tighter strings.)

- [ ] **Step 3: Implement `HealthKitHealthAuthorization`**

```swift
import Foundation
import os
#if canImport(HealthKit)
import HealthKit
#endif

final class HealthKitHealthAuthorization: HealthAuthorization, @unchecked Sendable {
    static let promptedDefaultsKey = "com.marcatos.SimPulse.healthAuthPrompted"
    private let defaults: UserDefaults
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "health-auth")
    #if canImport(HealthKit)
    private let store: HKHealthStore
    #endif

    init(defaults: UserDefaults = .standard, store: HKHealthStore = HKHealthStore()) { ... }

    var hasPrompted: Bool {
        defaults.bool(forKey: Self.promptedDefaultsKey)
    }

    var isAvailable: Bool {
        #if canImport(HealthKit)
        HKHealthStore.isHealthDataAvailable()
        #else
        false
        #endif
    }

    func requestAccessIfNeeded() async throws {
        if hasPrompted { return }
        guard isAvailable else {
            defaults.set(true, forKey: Self.promptedDefaultsKey)
            log.info("healthkit unavailable; skipping authorization sheet")
            return
        }
        #if canImport(HealthKit)
        let share: Set<HKSampleType> = [
            HKQuantityType(.activeEnergyBurned),
            HKObjectType.workoutType()
        ]
        let read: Set<HKObjectType> = [
            HKQuantityType(.heartRate),
            HKQuantityType(.activeEnergyBurned),
            HKObjectType.workoutType()
        ]
        do {
            try await store.requestAuthorization(toShare: share, read: read)
            defaults.set(true, forKey: Self.promptedDefaultsKey)
            log.info("healthkit authorization prompt completed")
        } catch {
            defaults.set(true, forKey: Self.promptedDefaultsKey)
            log.error("healthkit authorization failed: \(error.localizedDescription, privacy: .public)")
            throw error
        }
        #endif
    }
}
```

Always set prompted after an attempt (success or failure) so the system sheet is not spammed.

- [ ] **Step 4: Regenerate Xcode project on Mac and build**

```bash
xcodegen generate
bash scripts/build-ios.sh
bash scripts/test-ios.sh
```

Expected: BUILD SUCCEEDED; all unit tests PASS (prior 12 + new auth tests).

- [ ] **Step 5: Commit** (when user allows)

```text
feat(ios): request HealthKit access for session list
```

---

### Task 4: Session list empty UI + Settings CTA

**Files:**
- Modify: `apps/ios/SimPulse/SessionListView.swift`
- Modify: `apps/ios/SimPulse/Sessions/SessionListViewModel.swift` (if copy helpers needed)
- Test: optional presentation helper test; manual Preview check

**Interfaces:**
- Consumes: `SessionListEmptyReason`, `SessionListViewModel.emptyReason`
- Produces: Open Settings button when `.needsHealthAccess`

- [ ] **Step 1: Update empty ContentUnavailableView**

In `SessionListView`, when `model.sessions.isEmpty` and not loading:

```swift
ContentUnavailableView {
    Label(title, systemImage: icon)
} description: {
    Text(description)
} actions: {
    if model.emptyReason == .needsHealthAccess {
        Button("Open Settings") {
            if let url = URL(string: UIApplication.openSettingsURLString) {
                UIApplication.shared.open(url)
            }
        }
    }
}
```

Copy:

| `emptyReason` | Title | Description |
| --- | --- | --- |
| `.needsHealthAccess` | No sessions yet | Allow SimPulse in Settings → Health, or start a Sim Racing workout on Apple Watch. |
| `.healthUnavailable` | Health unavailable | Health data is not available on this device. |
| `nil` but empty + `errorText` | No sessions yet | `errorText` |
| else | No sessions yet | Workouts from Apple Watch will appear here. |

Import `UIKit` only if needed for `UIApplication` (or use SwiftUI-friendly openURL environment).

Prefer:

```swift
@Environment(\.openURL) private var openURL
// ...
openURL(URL(string: UIApplication.openSettingsURLString)!)
```

- [ ] **Step 2: Keep Previews compiling** with `MockHealthAuthorization(hasPrompted: true)`.

- [ ] **Step 3: Mac smoke — build + tests still green**

- [ ] **Step 4: Commit** (when user allows)

```text
feat(ios): guide users to Settings when Health list is empty
```

---

### Task 5: Docs, Plane, verify

**Files:**
- Modify: `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/handoffs/IOS-003.md`
- Optionally: `docs/KNOWN_ISSUES.md` one line that read-auth status cannot be distinguished from empty

**Interfaces:** none

- [ ] **Step 1: Fill handoff** with Files changed, Tests executed (Mac counts), Remaining work, Risks (read status limitation).

- [ ] **Step 2: Mark IOS-003 DONE in BACKLOG only after Mac verification and before/at merge; until then keep IN_PROGRESS.** Update CURRENT_STATE recommended next tasks (IOS-002 or WATCH-003/IOS-004).

- [ ] **Step 3: Plane comment** with test results and branch name (no LAN secrets).

- [ ] **Step 4: Commit docs** (when user allows)

```text
docs(ios): record IOS-003 HealthKit permissions
```

- [ ] **Step 5: Open PR to main when user asks** — title `feat(ios): request HealthKit access for session list`

---

## Spec coverage checklist

| Spec item | Task |
| --- | --- |
| Hybrid prompt once + UserDefaults | 2–3 |
| Types aligned with Watch | 3 |
| iOS entitlements + project.yml | 3 |
| Usage string tweak | 3 |
| emptyReason needsHealthAccess / healthUnavailable | 2, 4 |
| Open Settings via openSettingsURLString | 4 |
| Preview / mock path sheet-free | 2 |
| No biometric logging | 3 |
| Docs + Plane | 1, 5 |
| Mac xcodebuild verify | 3–5 |

## Self-review notes

- No TBD left; Settings URL choice locked to `UIApplication.openSettingsURLString`.
- `requestCallCount` semantics documented via `countOnlyUnpromptedRequests` so tests match live “sheet once” behavior while ViewModel may call the port every load.
- `HealthKitSessionRepository` unchanged except remaining the list adapter after auth.
