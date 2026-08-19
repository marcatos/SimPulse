# Current state

Agents must read this file before substantial work and update it after material changes.

**Date:** 2026-08-19  
**Milestone:** Phase 0 — Foundation  
**Product version:** 0.1.0-unreleased

## Current milestone

Phase 0 bootstrap is complete enough for parallel agents to start Phase 1–4 work in isolated lanes. Apple toolchain on the partner Mac is unblocked.

## Completed features

- Git repository on `main` with monorepo layout, EditorConfig, gitattributes, license, `.env.example`.
- Documentation system (`docs/*`, ADRs 0001–0009 + 0012–0013, AGENTS.md, privacy/security, backlog). Canonical operational status lives on Plane Pages.
- C# domain, protocol v1 envelope/codec, analytics (HR/energy summaries, RaceReport from DriverSession, HeartRateWindows lap/event averages gated by timeline offset, non-medical wording).
- Windows Bridge worker + Core library: fixture replay adapter, first-party iRacing mmap session reader (KI-002 live replay smoke 2026-08-19), gated PIN pairing, tray WinExe UX, MEL file logs, and session/lap race-event broadcast. KI-003 adds Kestrel TLS by default at `wss://127.0.0.1:8742/ws/`, a persisted self-signed certificate with logged SHA-256 pin, and loopback-only explicit cleartext opt-out. Merged [PR #12](https://github.com/marcatos/SimPulse/pull/12).
- INFRA-004: XcodeGen `project.yml` → `SimPulse.xcodeproj` (iOS + embedded Watch).
- WATCH-001: `WorkoutSessionController` start/end; companion unreachability does not stop recording; HealthKit Watch adapter (`HKWorkoutActivityType.other`, metadata Sim Racing). Merged PR #5.
- WATCH-002: glanceable Watch UI — large HR, elapsed, Idle/Recording; Always On hides Start/End. Merged [PR #6](https://github.com/marcatos/SimPulse/pull/6). Simulator screenshots in `docs/screenshots/watchos/`.
- DOCS-002: public GitHub README (product story, honest status, Watch screenshots). Merged [PR #7](https://github.com/marcatos/SimPulse/pull/7).
- IOS-001: session list from `SessionRepository` (Mock + HealthKit); ADR 0002 Accepted (SwiftData deferred). Merged [PR #8](https://github.com/marcatos/SimPulse/pull/8).
- IOS-003: HealthKit permissions — hybrid auth UX, iOS entitlement, Settings empty state. Merged [PR #9](https://github.com/marcatos/SimPulse/pull/9).
- IOS-002: session detail + Swift Charts HR line. Merged [PR #10](https://github.com/marcatos/SimPulse/pull/10).
- WATCH-003 + IOS-004: WatchConnectivity workout summary sync. Merged [PR #11](https://github.com/marcatos/SimPulse/pull/11).
- GitHub Actions CI (INFRA-002 DONE). Apple CI placeholder (INFRA-003 DONE).

## Partially completed features

- WatchConnectivity paired-device E2E (KI-008).
- Protocol: Bridge TLS + PIN pairing shipped; IOS-005 client-side certificate pin enforcement remains.
- Entitlements: `CapabilityGate` only (KI-004).

## Active work

- None claimed. Next: IOS-005 Bridge client pin, or PROTO-003 Swift codec.

## Blocked work

- PROTO-003 Swift codec; remaining IOS-* blocked only on upstream deps (not toolchain — KI-001 closed).

## Known broken behavior

- `scripts/build-ios.sh` / `test-ios.sh` / `build-watch.sh` record **NOT EXECUTED** on Windows (no Xcode).
- `scripts/archive-ios.sh` still exits 1 until a Development Team is configured.
- Bridge without `SIMPULSE_FIXTURE_PATH` waits for live iRacing mmap when the sim is closed (expected).

## Latest successful build

- **.NET Bridge host:** `dotnet test SimPulse.sln --configuration Release` — 149 passed (2026-08-19, Windows, KI-003). Live iRacing replay smoke (KI-002) produced SessionStart + lap 1→2 events.
- **iOS / watchOS (simpulse-mac, Xcode 26.6):** `xcodebuild test` scheme SimPulse iPhone 17 — **TEST SUCCEEDED** (31 tests). `build-ios.sh` + `build-watch.sh` — **BUILD SUCCEEDED**. CODE_SIGNING_ALLOWED=NO.

## Latest successful tests

| Suite | Platform | Result |
| --- | --- | --- |
| `dotnet test SimPulse.sln --configuration Release` | Windows 10.0.26200, SDK 8.0.424 | **149 passed**, 0 failed (2026-08-19, KI-003) |
| KI-002 live mmap smoke (iRacing replay) | Windows, `iRacingSim64DX11` | **PASS** mmap + SessionStart + LapComplete/LapStart (2026-08-19) |
| GitHub Actions `.NET` job | `windows-latest`, `ubuntu-latest` (PR #1) | **pass** |
| `xcodebuild test` SimPulse | simpulse-mac, iPhone 17 simulator | **31 passed**, 0 failed (2026-08-19, WATCH-003/IOS-004) |
| xcodebuild iOS | simpulse-mac, iPhone 17 / generic iOS Simulator | **BUILD SUCCEEDED** |
| xcodebuild watchOS | simpulse-mac, watchOS Simulator | **BUILD SUCCEEDED** |

## Architecture summary

Monorepo. Hexagonal Bridge (`ISimulatorAdapter`, `IBridgeCertificateSource`). JSON protocol v1 over TLS by default (LAN, pairing required, client pin contract in ADR 0013). HealthKit is source of truth for workouts. Correlation uses explicit clock sources (ADR 0004). No cloud. No GPL iRacing wrappers (IRSDKSharper rejected).

## Immediate recommended next tasks

1. IOS-005 Bridge client pairing + certificate pin enforcement
2. PROTO-003 Swift protocol codec
3. KI-008 paired Watch/iPhone WatchConnectivity E2E when a Mac is available

## Screenshot policy

Apple UI changes require refreshed simulator screenshots before marking UI work DONE (`.cursor/rules/apple-screenshots.mdc`). WATCH-003/IOS-004 sync work did not alter visible UI — screenshots not required for this slice.
