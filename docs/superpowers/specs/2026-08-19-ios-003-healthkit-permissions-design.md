# IOS-003 — HealthKit permissions (iOS)

**Date:** 2026-08-19  
**Status:** Approved for planning (pending user review of this file)  
**Backlog:** IOS-003  
**Depends on:** IOS-001 (merged), Xcode / partner Mac

## Goal

Make the iPhone session list usable on device: request HealthKit access with a clear hybrid UX, ship the iOS HealthKit entitlement and minimum types, and document denial / empty as an honest empty state.

## Non-goals

- Session detail / charts (IOS-002)
- WatchConnectivity sync (WATCH-003 / IOS-004)
- Writing workouts from the iPhone (Watch remains the writer for Sim Racing workouts)
- Distinguishing “user denied read” vs “no Sim Racing workouts” with certainty (HealthKit does not expose reliable read authorization status)
- Logging heart rate, energy, or other biometric payloads

## Context

- IOS-001 lists sessions via `SessionRepository` / `HealthKitSessionRepository`. Live list returns empty when HealthKit is unavailable, unauthorized, or has no matching workouts.
- iOS already has `NSHealthShareUsageDescription` / `NSHealthUpdateUsageDescription` in `project.yml`.
- watchOS already requests auth and has `com.apple.developer.healthkit` entitlements; iOS app target does **not** yet point at an entitlements file.
- Watch share/read types: share `activeEnergyBurned` + `workoutType`; read `heartRate` + `activeEnergyBurned` + `workoutType`.

## Chosen UX (hybrid “C”)

1. On first session-list `load()`, request HealthKit authorization once (system sheet).
2. Persist a local “already prompted” flag (UserDefaults) so we do not call `requestAuthorization` on every refresh after the first attempt.
3. After the prompt (or if already prompted), query the session repository as today.
4. Empty states:
   - **Loading:** existing ProgressView.
   - **Empty after authorize attempt:** `.needsHealthAccess` copy + “Open Settings” (denial and “no workouts yet” share this UI; copy mentions both allowing Health access and starting a Watch workout).
   - **Health unavailable:** `.healthUnavailable` copy, no Settings CTA required.
   - **Has sessions:** unchanged list UI.

Pull-to-refresh reloads the list; it does **not** re-show the system authorization sheet if already prompted (user must change access in Settings).

## Architecture

Hexagonal, matching IOS-001:

```text
SessionListView
  → SessionListViewModel
       → HealthAuthorization (port)  → HealthKitHealthAuthorization (adapter)
       → SessionRepository (port)    → HealthKitSessionRepository (existing)
```

### Port: `HealthAuthorization`

Responsibilities:

- `var hasPrompted: Bool` (backed by UserDefaults in the live adapter; injectable for tests)
- `func requestAccessIfNeeded() async throws` — no-op if already prompted; otherwise call HealthKit `requestAuthorization` then set prompted

Do **not** claim “authorized” for read types; only “prompted”.

### Adapter: `HealthKitHealthAuthorization`

- Guard `HKHealthStore.isHealthDataAvailable()`; if unavailable, mark prompted (or skip sheet) and let the list stay empty with a short unavailable message.
- Types **aligned with Watch** (minimum for list + future detail):
  - **toShare:** `workoutType`, `activeEnergyBurned`
  - **toRead:** `workoutType`, `activeEnergyBurned`, `heartRate`
- Errors from `requestAuthorization` are logged without biometric values; ViewModel falls through to list load and empty-state copy.

### ViewModel changes

`SessionListViewModel.load()`:

1. Set loading.
2. Call `requestAccessIfNeeded()`.
3. Call `repository.listSessions()`.
4. Publish sessions / empty-state kind after the authorize attempt:
   - `emptyReason`: `nil` when sessions non-empty; `.healthUnavailable` when HealthKit is not available; `.needsHealthAccess` when prompted (or unavailable handled above) and the list is empty (covers denial, restriction, and truly no Sim Racing workouts — same UI by design)
5. Clear loading.

Inject `HealthAuthorization` in `init` alongside the repository. `live()` wires HealthKit adapters; DEBUG `--simpulse-preview-sessions` keeps mock repository and a no-op / always-prompted auth stub so previews stay sheet-free.

### UI

- Extend `SessionListView` empty `ContentUnavailableView` using `emptyReason` / `errorText`.
- When `emptyReason == .needsHealthAccess`: primary button “Open Settings” that opens `UIApplication.openSettingsURLString` (app’s Settings page). Copy tells the user to enable Health access for SimPulse. No Health-app deep link (fragile / undocumented).
- No new onboarding screen.

### Entitlements / project

- Add `apps/ios/SimPulse/SimPulse.entitlements` with `com.apple.developer.healthkit = true`.
- Set `CODE_SIGN_ENTITLEMENTS` on the iOS target in `project.yml`.
- Regenerate `SimPulse.xcodeproj` on the Mac via XcodeGen (do not hand-edit pbxproj as the source of truth).
- Optionally tighten iOS usage strings to mention listing past Sim Racing workouts; keep “Not a medical device.”

## Error handling

| Case | Behavior |
| --- | --- |
| Health data unavailable | Empty + unavailable copy; no system sheet |
| `requestAuthorization` throws | Log ERROR (description only); still attempt list; empty → Settings guidance if prompted |
| Query fails | Existing: empty list + “Could not load sessions.” |
| Denied / restricted reads | Empty list (indistinguishable from no workouts); Settings guidance after prompt |

## Testing

Unit tests (iPhone simulator on partner Mac), no real Health sheet required:

- Mock `HealthAuthorization`: first load calls request once; second load does not.
- After prompted + empty repository → ViewModel exposes `.needsHealthAccess`.
- Mock repository with sessions → list populated; empty reason not Settings.
- Preview / `--simpulse-preview-sessions` path does not require HealthKit.

Record `xcodebuild test` + `build-ios.sh` results; Windows remains NOT EXECUTED for Apple scripts.

## Docs / tracker

- Update `docs/BACKLOG.md` (IOS-003 IN_PROGRESS → DONE when merged), `docs/CURRENT_STATE.md`, `docs/handoffs/IOS-003.md`.
- Plane work item for IOS-003 before coding; session notes as comments.
- Do not put secrets or LAN IPs in public docs.

## Acceptance mapping

| Criterion | How met |
| --- | --- |
| Minimum read/write types | Same sets as Watch, requested via adapter |
| Usage strings | Present in `project.yml`; tweak if needed |
| Denial is documented empty state | Prompted + empty → Settings copy + CTA; KNOWN_ISSUES / handoff notes HealthKit read-status limitation |

## Implementation order (summary)

1. Plane issue + branch/worktree `feat/ios-003-healthkit-permissions`
2. Entitlements + `project.yml`
3. Port + HealthKit adapter + UserDefaults prompted flag
4. ViewModel + SessionListView empty/CTA
5. Tests
6. Mac verify, docs, PR
