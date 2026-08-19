Task
WATCH-003 — Persist and sync summary to iPhone

Goal
After a Sim Racing workout ends on the Watch: persist in HealthKit, enqueue a summary-only payload when the iPhone is unreachable, and deliver via WatchConnectivity when possible.

Status
DONE (on branch `feat/watch-ios-sync-summary`; pending PR/merge)

Files changed
- `apps/ios/SimPulse/Sync/WatchWorkoutSummaryMessage.swift` — shared wire DTO + userInfo encode/decode
- `apps/ios/SimPulse/Sync/WorkoutSummaryOutbox.swift` — port + `FileWorkoutSummaryOutbox` (idempotent by sessionId)
- `apps/watchos/SimPulseWatch/Adapters/WatchConnectivitySummarySender.swift` — WCSession activate, enqueue+transfer, flush on reachability
- `apps/watchos/SimPulseWatch/Adapters/HealthKitWatchWorkoutDataSource.swift` — capture `HKWorkout` on finish, build summary, enqueue via sender
- `apps/watchos/SimPulseWatch/Application/WorkoutViewModel.swift` — wires outbox + sender in live path
- `project.yml` — SimPulseWatch sources include `apps/ios/SimPulse/Sync`
- `SimPulse.xcodeproj/project.pbxproj` — regenerated on Mac (Tasks 3–4 verify)
- `docs/superpowers/specs/2026-08-19-watch-ios-summary-sync-design.md`
- `docs/superpowers/plans/2026-08-19-watch-ios-summary-sync.md`
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/handoffs/WATCH-003.md`

Decisions made
- Paired delivery with IOS-004; circular BACKLOG deps resolved as one slice.
- Summary-only payload via `transferUserInfo`; idempotency key = `HKWorkout.uuid` (sessionId).
- Outbox entry removed after successful WC handoff; system delivers asynchronously.
- Logs: session IDs and counts only; no bpm/kcal values.
- Screenshots: **N/A** — no Watch UI chrome delta (sync-only; see `.cursor/rules/apple-screenshots.mdc`).

Tests executed
- Mac (simpulse-mac, Xcode 26.6, iPhone 17 simulator): `xcodegen generate && bash scripts/test-ios.sh` — **31 passed**, 0 failed (2026-08-19, Tasks 3–4 verify).
- Mac build: `bash scripts/build-watch.sh` — **BUILD SUCCEEDED**; `bash scripts/build-ios.sh` — **BUILD SUCCEEDED**.
- Windows: NOT EXECUTED (expected).

Tests passing
Yes — 31/31 on iPhone 17 simulator (includes 5 `WatchWorkoutSummarySyncTests` for wire/outbox contracts shared with iOS):
- HealthAuthorizationTests: 5
- SessionDetailTests: 7
- SessionDetailViewModelTests: 2
- SessionRepositoryTests: 4
- WatchWorkoutSummarySyncTests: 5
- WorkoutGlancePresentationTests: 4
- WorkoutSessionControllerTests: 4

Known failures
None.

Remaining work
- Open PR to `main` (`feat(sync): WatchConnectivity workout summary queue and ingest`).
- Merge branch; optional end-to-end WC delivery smoke on paired physical devices.

Risks
- WatchConnectivity delivery timing when iPhone app is backgrounded.
- Live WC path not exercised on simulator (expected).

Suggested next action
Open PR and merge `feat/watch-ios-sync-summary`; then Bridge KI-002 live mmap smoke or KI-003 TLS, or PROTO-003 Swift codec.
