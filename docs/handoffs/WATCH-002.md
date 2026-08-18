Task
WATCH-002 — Glanceable live UI

Goal
Make the Watch workout screen glanceable: large current HR, elapsed, avg/max/kcal, Idle/Recording. Always On keeps core metrics and hides Start/End.

Status
DONE (not merged)

Files changed
- apps/ios/SimPulse/Workout/WorkoutGlancePresentation.swift
- apps/ios/SimPulseTests/WorkoutGlancePresentationTests.swift
- apps/watchos/SimPulseWatch/WorkoutView.swift
- apps/watchos/SimPulseWatch/Application/WorkoutPreviewLaunch.swift
- apps/watchos/SimPulseWatch/SimPulseWatchApp.swift
- SimPulse.xcodeproj (regenerated)
- docs/screenshots/watchos/ (idle, recording, Always On)
- docs/BACKLOG.md, docs/CURRENT_STATE.md, docs/handoffs/WATCH-002.md

Decisions made
- Pure presentation mapping tested on iOS Simulator.
- Always On (`isLuminanceReduced`) hides Start/End, errors, and the avg/max/kcal row; keeps state, large HR, elapsed.
- Do not log HR or energy values.

Tests executed
- RED: 4 glance tests failed on stub (empty strings).
- GREEN: 8 passed (4 glance + 4 WATCH-001). Watch BUILD SUCCEEDED.

Tests passing
Yes, on simpulse-mac iPhone 17 simulator.

Known failures
Physical Watch Always On not run.

Remaining work
Merge feat/watch-002-glance-ui. Next: WATCH-003 (blocked on IOS-004) or IOS-001.

Risks
Simulator cannot fully exercise Always On luminance.

Suggested next action
Commit/PR, then IOS-001 or WATCH-003 after IOS-004.
