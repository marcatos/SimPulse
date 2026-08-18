# Current state

Agents must read this file before substantial work and update it after material changes.

**Date:** 2026-08-18  
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
- WATCH-001: `WorkoutSessionController` start/end; companion unreachability does not stop recording; HealthKit Watch adapter (`HKWorkoutActivityType.other`, metadata Sim Racing); Start/End on `WorkoutView`.
- GitHub Actions CI (INFRA-002 DONE). Apple CI placeholder (INFRA-003 DONE).

## Partially completed features

- Protocol: loopback WebSocket + PIN pairing. TLS remaining (KI-003).
- iRacing live path (KI-002): adapters tested with synthetic buffers only.
- Entitlements: `CapabilityGate` only (KI-004).
- Watch UI: Start/End + metric placeholders exist; glance/Always On polish is WATCH-002. HealthKit persist/sync to iPhone is WATCH-003.

## Active work

- None. WATCH-001 is DONE on branch `feat/watch-001-workout-lifecycle` (not merged).

## Blocked work

- PROTO-003 Swift codec and remaining IOS-* still need Mac verification per item; toolchain itself is no longer the blocker (KI-001 closed).

## Known broken behavior

- `scripts/build-ios.sh` / `test-ios.sh` / `build-watch.sh` record **NOT EXECUTED** on Windows (no Xcode).
- `scripts/archive-ios.sh` still exits 1 until a Development Team is configured.
- Bridge without `SIMPULSE_FIXTURE_PATH` waits for live iRacing mmap (KI-002).

## Latest successful build

- **.NET Bridge host:** `dotnet test SimPulse.sln --configuration Release` — 129 passed (2026-08-18, Windows). No live iRacing.
- **iOS / watchOS (simpulse-mac, Xcode 26.6):** `xcodebuild test` scheme SimPulse iPhone 17 — **TEST SUCCEEDED** (4 WorkoutSessionController tests). `build-watch.sh` and `build-ios.sh` — **BUILD SUCCEEDED**. CODE_SIGNING_ALLOWED=NO. Live HealthKit on a physical Watch was not run.

## Latest successful tests

| Suite | Platform | Result |
| --- | --- | --- |
| `dotnet test SimPulse.sln --configuration Release` | Windows 10.0.26200, SDK 8.0.424 | **129 passed** |
| GitHub Actions `.NET` job | `windows-latest`, `ubuntu-latest` (PR #1) | **pass** |
| `xcodebuild test` SimPulse | simpulse-mac, iPhone 17 simulator | **4 passed**, 0 failed (2026-08-18) |
| xcodebuild iOS | simpulse-mac, iPhone 17 / generic iOS Simulator | **BUILD SUCCEEDED** |
| xcodebuild watchOS | simpulse-mac, watchOS Simulator | **BUILD SUCCEEDED** |

## Architecture summary

Monorepo. Hexagonal Bridge (`ISimulatorAdapter`). JSON protocol v1 (LAN, pairing required, TLS later). HealthKit is source of truth for workouts. Correlation uses explicit clock sources (ADR 0004). No cloud. No GPL iRacing wrappers (IRSDKSharper rejected).

## Immediate recommended next tasks

1. WATCH-002 glanceable live UI polish
2. WATCH-003 persist/sync summary to iPhone
3. IOS-001 session history
4. Windows: KI-002 live mmap smoke; KI-003 TLS
