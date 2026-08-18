Task
WATCH-002 — Glanceable live UI

Goal
Make the Watch workout screen glanceable: large current HR, elapsed, avg/max/kcal, Idle/Recording. Always On keeps core metrics and hides Start/End.

Status
IN_PROGRESS

Files changed
- apps/ios/SimPulse/Workout/WorkoutGlancePresentation.swift (planned)
- apps/ios/SimPulseTests/WorkoutGlancePresentationTests.swift (planned)
- apps/watchos/SimPulseWatch/WorkoutView.swift (planned)

Decisions made
- Presentation mapping is a pure struct tested on iOS Simulator (same as WATCH-001).
- Always On uses SwiftUI isLuminanceReduced; controls and errors hide when reduced.
- Do not log HR or energy values.

Tests executed
None yet.

Tests passing
n/a

Known failures
n/a

Remaining work
RED presentation tests, GREEN view, Mac xcodebuild.

Risks
Always On cannot be verified on Windows; simulator luminance reduction is limited.

Suggested next action
Write failing WorkoutGlancePresentation tests.
