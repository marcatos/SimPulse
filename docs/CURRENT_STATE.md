# Current state

Agents must read this file before substantial work and update it after material changes.

**Date:** 2026-08-18  
**Milestone:** Phase 0 — Foundation  
**Product version:** 0.1.0-unreleased

## Current milestone

Phase 0 bootstrap is complete enough for parallel agents to start Phase 1–4 work in isolated lanes.

## Completed features

- Git repository on `main` with monorepo layout, EditorConfig, gitattributes, license, `.env.example`.
- Documentation system (`docs/*`, ADRs 0001–0009, AGENTS.md, privacy/security, backlog).
- C# domain, protocol v1 envelope/codec, analytics (HR/energy summaries, RaceReport from DriverSession, HeartRateWindows lap/event averages gated by timeline offset, non-medical wording).
- Windows Bridge worker + Core library: fixture replay adapter, first-party iRacing mmap session reader (YAML subset; fake memory in tests), gated PIN pairing (CSPRNG, 5-minute window, 5-attempt lockout) + trusted-device store (in-memory or JSON file), Windows tray pairing UX (`WinExe` + `NotifyIcon`, **Show current PIN** / **Pair new device** without restart, console fallback if tray startup fails; last PIN cleared when the window closes), MEL file logs under `%LOCALAPPDATA%\SimPulse\logs` (IO failures disable file logging without crashing the host), `SessionLifecycleTracker` wired in `BridgeRuntime` for session/lap race-event dedupe, loopback WebSocket listen/accept (`HttpListener` on `/ws/`), race-event envelopes broadcast to trusted clients (no biometrics).
- Apple Swift source scaffolding without an Xcode project (ADR 0009).
- GitHub Actions CI (INFRA-002 DONE): `.github/workflows/ci.yml` runs `dotnet test SimPulse.sln` on `windows-latest` and `ubuntu-latest`; test-result artifacts use `retention-days: 7`. PR #1 checks green 2026-08-18.
- Apple CI placeholder (INFRA-003 DONE): workflow records NOT EXECUTED until an Xcode project exists.

## Partially completed features

- Protocol: JSON types and compatibility tests exist; LAN WebSocket listen/accept exists on loopback with gated PIN pairing. Trusted clients receive `simulator.race-event` envelopes. Windows interactive Bridge registers `NotifyIconPairingUx` on an STA message loop and hides the console (`WinExe`); **Show current PIN** redisplays the last PIN only while the window is open; **Pair new device** reopens a PIN window without process restart. Tray startup failure/timeout falls back to console. Remaining KI-003 gap is TLS.
- iRacing: first-party mmap session reader (BRIDGE-003 + BUG-001 DONE). BRIDGE-008 Task 1 adds official header layout + typed var-table reads (`SessionTime`, `SessionNum`, `DriverCarIdx`, `Lap`) from synthetic buffers. Task 2 resolves vehicle/session from YAML `Drivers`/`Sessions` lists; unmatched `driverCarIdx`/`sessionNum` are `Unavailable` (no first-entry fallback). Live YAML / live 60 Hz still requires the sim + memmap (KI-002). Adapter wiring is Task 3.
- Entitlements: `CapabilityGate` only; no StoreKit or UI enforcement (KI-004).
- Swift mirrors: names exist; not compiled.

## Active work

- BRIDGE-008 Tasks 1–2 done (header/var parsers + YAML list match with unmatched lookups Unavailable). Tasks 3–4 remaining (`docs/handoffs/BRIDGE-008.md`).

## Blocked work

- All WATCH-* and IOS-* implementation: no Swift/Xcode on the Windows workstation (KI-001).
- PROTO-003 Swift codec: same.

## Known broken behavior

- `scripts/build-ios.sh`, `test-ios.sh`, `archive-ios.sh`, `build-watch.sh` exit 1 by design until an Xcode project exists.
- Bridge without `SIMPULSE_FIXTURE_PATH` waits on `SubscribeAsync` until `Local\IRSDKMemMapFileName` appears and is connected; no live session YAML until then (KI-002).

## Latest successful build

- **.NET Bridge host:** `dotnet test SimPulse.sln --configuration Release` — succeeded, 0 warnings, 0 errors (2026-08-18, Windows 10.0.26200, SDK 8.0.424). BRIDGE-008 Task 1–2 parsers are covered by synthetic-buffer and two-driver YAML tests (unmatched lookups Unavailable). BRIDGE-007 host is `net8.0-windows` `WinExe` with `UseWindowsForms`; Linux stays `net8.0` `Exe`. File logs default to `%LOCALAPPDATA%\SimPulse\logs`.
- **iOS / watchOS:** NOT EXECUTED (no Xcode / no `.xcodeproj`).

## Latest successful tests

| Suite | Platform | Result |
| --- | --- | --- |
| `dotnet test SimPulse.sln --configuration Release` | Windows 10.0.26200, SDK 8.0.424 | **114 passed**, 0 failed (Domain 6, Analytics 9, Protocol 7, Bridge.Core 92) |
| GitHub Actions `.NET` job | `windows-latest`, `ubuntu-latest` (PR #1, 2026-08-18) | **pass** (Actions runs 32127173207, 32127216936) |
| xcodebuild iOS | n/a | NOT EXECUTED |
| xcodebuild watchOS | n/a | NOT EXECUTED |

## Architecture summary

Monorepo. Hexagonal Bridge (`ISimulatorAdapter`). JSON protocol v1 (LAN, pairing required, TLS later). HealthKit is source of truth for workouts. Correlation uses explicit clock sources (ADR 0004). No cloud. No GPL iRacing wrappers (IRSDKSharper rejected).

## Immediate recommended next tasks

Windows (no Mac required):

1. Live iRacing 60 Hz variable table (beyond session YAML)
2. TLS for Bridge transport (KI-003 remaining)

Mac (unblocks Watch/iOS):

1. Generate Xcode project (ADR 0009)
2. WATCH-001 workout lifecycle
3. IOS-001 session history
