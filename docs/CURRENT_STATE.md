# Current state

Agents must read this file before substantial work and update it after material changes.

**Date:** 2026-08-19  
**Milestone:** Phase 0 — Foundation  
**Product version:** 0.1.0-unreleased

## Current milestone

Phase 0 bootstrap is complete enough for parallel agents to start Phase 1–4 work in isolated lanes. Apple toolchain on the partner Mac is unblocked.

## Completed features

- Git repository on `main` with monorepo layout, EditorConfig, gitattributes, license, `.env.example`.
- Documentation system (`docs/*`, ADRs 0001–0009 + 0012, AGENTS.md, privacy/security, backlog). Canonical operational status lives on Plane Pages.
- C# domain, protocol v1 envelope/codec, analytics (HR/energy summaries, RaceReport from DriverSession, HeartRateWindows lap/event averages gated by timeline offset, non-medical wording).
- Windows Bridge worker + Core library: fixture replay adapter, first-party iRacing mmap session reader, gated PIN pairing, tray WinExe UX, MEL file logs, session/lap race-event broadcast on loopback WebSocket.
- INFRA-004: XcodeGen `project.yml` → `SimPulse.xcodeproj` (iOS + embedded Watch).
- WATCH-001: `WorkoutSessionController` start/end; companion unreachability does not stop recording; HealthKit Watch adapter (`HKWorkoutActivityType.other`, metadata Sim Racing). Merged PR #5.
- WATCH-002: glanceable Watch UI — large HR, elapsed, Idle/Recording; Always On hides Start/End. Merged [PR #6](https://github.com/marcatos/SimPulse/pull/6). Simulator screenshots in `docs/screenshots/watchos/`.
- DOCS-002: public GitHub README (product story, honest status, Watch screenshots). Merged [PR #7](https://github.com/marcatos/SimPulse/pull/7).
- IOS-001: session list from `SessionRepository` (Mock + HealthKit); ADR 0002 Accepted (SwiftData deferred). Merged [PR #8](https://github.com/marcatos/SimPulse/pull/8).
- IOS-003: HealthKit permissions — hybrid auth UX, iOS entitlement, Settings empty state. Merged [PR #9](https://github.com/marcatos/SimPulse/pull/9).
- IOS-002: session detail + Swift Charts HR line. Merged [PR #10](https://github.com/marcatos/SimPulse/pull/10).
- GitHub Actions CI (INFRA-002 DONE). Apple CI placeholder (INFRA-003 DONE).

## Partially completed features

- **WATCH-003 + IOS-004:** WatchConnectivity workout summary sync (file outbox, WC transfer/ingest, idempotent merge, session list refresh) on branch `feat/watch-ios-sync-summary` (Mac **31 tests passed**; pending PR/merge). Screenshots **N/A** — sync-only; no user-visible UI chrome delta per `.cursor/rules/apple-screenshots.mdc`.
- Protocol: loopback WebSocket + PIN pairing. TLS remaining (KI-003).
- iRacing live path (KI-002): adapters tested with synthetic buffers only.
- Entitlements: `CapabilityGate` only (KI-004).

## Active work

- `feat/watch-ios-sync-summary` — WATCH-003 + IOS-004 implementation complete; awaiting PR/merge.

## Blocked work

- PROTO-003 Swift codec; remaining IOS-* blocked only on upstream deps (not toolchain — KI-001 closed).

## Known broken behavior

- `scripts/build-ios.sh` / `test-ios.sh` / `build-watch.sh` record **NOT EXECUTED** on Windows (no Xcode).
- `scripts/archive-ios.sh` still exits 1 until a Development Team is configured.
- Bridge without `SIMPULSE_FIXTURE_PATH` waits for live iRacing mmap (KI-002).

## Latest successful build

- **.NET Bridge host:** `dotnet test SimPulse.sln --configuration Release` — 129 passed (2026-08-18, Windows). No live iRacing.
- **iOS / watchOS (simpulse-mac, Xcode 26.6):** `xcodebuild test` scheme SimPulse iPhone 17 — **TEST SUCCEEDED** (31 tests). `build-ios.sh` + `build-watch.sh` — **BUILD SUCCEEDED**. CODE_SIGNING_ALLOWED=NO.

## Latest successful tests

| Suite | Platform | Result |
| --- | --- | --- |
| `dotnet test SimPulse.sln --configuration Release` | Windows 10.0.26200, SDK 8.0.424 | **129 passed** |
| GitHub Actions `.NET` job | `windows-latest`, `ubuntu-latest` (PR #1) | **pass** |
| `xcodebuild test` SimPulse | simpulse-mac, iPhone 17 simulator | **31 passed**, 0 failed (2026-08-19, WATCH-003/IOS-004) |
| xcodebuild iOS | simpulse-mac, iPhone 17 / generic iOS Simulator | **BUILD SUCCEEDED** |
| xcodebuild watchOS | simpulse-mac, watchOS Simulator | **BUILD SUCCEEDED** |

## Architecture summary

Monorepo. Hexagonal Bridge (`ISimulatorAdapter`). JSON protocol v1 (LAN, pairing required, TLS later). HealthKit is source of truth for workouts. Correlation uses explicit clock sources (ADR 0004). No cloud. No GPL iRacing wrappers (IRSDKSharper rejected).

## Immediate recommended next tasks

1. PR/merge `feat/watch-ios-sync-summary` (WATCH-003 + IOS-004)
2. Windows: KI-002 live mmap smoke; KI-003 TLS
3. PROTO-003 Swift protocol codec (when Bridge/iOS pairing lane is ready)

## Screenshot policy

Apple UI changes require refreshed simulator screenshots before marking UI work DONE (`.cursor/rules/apple-screenshots.mdc`). WATCH-003/IOS-004 sync work did not alter visible UI — screenshots not required for this slice.
