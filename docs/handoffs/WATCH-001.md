Task
WATCH-001 — Workout session lifecycle

Goal
Start and end a Sim Racing workout on watchOS through HealthKit, keep recording if the iPhone is unreachable, and unit-test the use case against WorkoutDataSource.

Status
DONE (not merged)

Files changed
- apps/ios/SimPulse/Workout/WorkoutSessionController.swift
- apps/ios/SimPulse/Workout/WorkoutDataSource.swift
- apps/ios/SimPulseTests/WorkoutSessionControllerTests.swift
- apps/watchos/SimPulseWatch/Adapters/HealthKitWatchWorkoutDataSource.swift
- apps/watchos/SimPulseWatch/Application/WorkoutViewModel.swift
- apps/watchos/SimPulseWatch/WorkoutView.swift
- apps/watchos/SimPulseWatch/SimPulseWatchApp.swift
- apps/watchos/SimPulseWatch/WatchWorkoutSession.swift
- apps/watchos/SimPulseWatch/SimPulseWatch.entitlements
- project.yml, SimPulse.xcodeproj, ADR 0012, Apple scripts

Decisions made
- HKWorkoutActivityType.other + metadata Sim Racing (no honest sport type).
- Controller shared Swift; XCTest on iOS Simulator (iPhone 17), not a watchOS test host.
- companionReachabilityDidChange logs and does not call stop().
- Do not log HR or energy values (os.Logger durations only).

Tests executed
- RED: 4 XCTest failures on stub controller (simpulse-mac).
- GREEN: `./scripts/test-ios.sh` — TEST SUCCEEDED, 4 passed.
- `./scripts/build-watch.sh` and `./scripts/build-ios.sh` — BUILD SUCCEEDED.

Tests passing
Yes, on simpulse-mac. Windows Apple scripts remain NOT EXECUTED.

Known failures
Live HealthKit authorization / physical Watch not run.

Remaining work
WATCH-002 glance polish. WATCH-003 HealthKit persist + iPhone sync. Merge this branch.

Risks
Simulator HealthKit is not a substitute for a real Watch workout.

Suggested next action
Commit/PR feat/watch-001-workout-lifecycle, then WATCH-002.
