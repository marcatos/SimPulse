# Known issues

Do not hide defects because they are outside the current task.

| ID | Date | Component | Severity | Status |
| --- | --- | --- | --- | --- |
| KI-001 | 2026-08-18 | Apple apps | High (blocks Phase 1–2) | Closed |
| KI-002 | 2026-08-18 | Bridge / iRacing | Medium (live still needs sim + memmap) | Closed 2026-08-19 (live replay smoke) |
| KI-003 | 2026-08-18 | Protocol | Low | Mitigated 2026-08-19 (Bridge TLS shipped; IOS-005 pin pending) |
| KI-004 | 2026-08-18 | Product | Medium | Open |
| KI-005 | 2026-08-18 | Android / Wear OS | Medium (blocks Phase 9) | Open |
| KI-006 | 2026-08-18 | Bridge / Security | Low | Mitigated 2026-08-19 (Bridge token shipped; IOS-005 pending) |
| KI-007 | 2026-08-19 | iOS / HealthKit | Low | Open (by design) |
| KI-008 | 2026-08-19 | watchOS / iOS / WatchConnectivity | Medium | Open |

## KI-001 — Apple project not generated

- **Status:** Closed 2026-08-18. XcodeGen `SimPulse.xcodeproj` exists; iOS 26.5 + watchOS 26.5 simulators installed on `simpulse-mac`. First compile and WATCH-001 unit tests succeeded. Remaining Apple work is WATCH-002 / WATCH-003 / IOS-001, not missing toolchain.
- **Symptoms (historical):** No `.xcodeproj` on Windows; missing simulator runtimes on the Mac.
- **Related:** ADR 0009, ADR 0012, INFRA-003, INFRA-004, WATCH-001

## KI-002 — Live iRacing mmap smoke

- **Status:** Closed 2026-08-19. Live replay on this PC (`iRacingSim64DX11`, `irsdkEnableMem=1` in `Documents\iRacing\app.ini`) opened `Local\IRSDKMemMapFileName` with `irsdk_stConnected=1`. Bridge (no `SIMPULSE_FIXTURE_PATH`) logged mmap open, `Available=True`, `iRacing session started` (YAML length 43694), `SessionStart`, then `LapStart` lap 1 / `LapComplete` lap 1 / `LapStart` lap 2 with `SessionNum=2`. Trusted-client broadcast had Recipients=0 (no paired phone). Repeat locally with `pwsh -File scripts/smoke-iracing-mmap.ps1`.
- **Still true when the sim is closed:** Without `SIMPULSE_FIXTURE_PATH`, Bridge probes the official mmap, `IsAvailableAsync` is false until the map appears, and `BridgeRuntime` still enters `SubscribeAsync` so it can attach later. Fixture replay remains `tests/fixtures/telemetry/iracing-practice-short.json`. CI never requires a live session.
- **Out of scope:** ANALYTICS-003 `RaceReportBuilder` peak-event wiring; 60 Hz WebSocket telemetry frames (race-events only).
- **Related:** ADR 0006, BRIDGE-003, BRIDGE-008, BUG-001, BUG-004, `docs/handoffs/KI-002.md`

## KI-003 — Bridge TLS shipped; client pin enforcement pending

- **Status:** Mitigated 2026-08-19. Bridge now defaults to Kestrel TLS at `wss://127.0.0.1:8742/ws/`, persists or loads a self-signed PFX, and logs the lowercase certificate-DER SHA-256 fingerprint for pinning. Cleartext requires explicit `SIMPULSE_BRIDGE_TLS=0`, `false`, or `off` and is refused outside loopback.
- **Remaining:** IOS-005 must pin `TlsCertSha256` and reject certificate mismatches. Until that client exists, the Bridge-side pin contract is covered by .NET transport tests rather than an end-to-end iPhone flow.
- **Operational note:** Read the fingerprint from Information logs. Keep the generated PFX and any configured password outside source control. Per-device reconnect proof is specified by ADR 0014.
- **Related:** PROTO-001, BRIDGE-005, BRIDGE-006, BRIDGE-007, ADR 0003, ADR 0013, ADR 0014, KI-006, IOS-005

## KI-005 — Android and Wear OS projects not generated

- **Symptoms:** No Gradle project under `apps/android` or `apps/wearos`; Wear AVD exists on Windows but cannot build an app.
- **Workaround:** Develop Bridge/.NET and protocol contracts; treat Phase 9 as documented only until AND-001.
- **Suspected cause:** Phase 9 deferred until the Apple vertical slice (Phases 1–5).
- **Related:** ADR 0010, AND-001, WEAROS-001

## KI-006 — Per-device reconnect token

- **Status:** Mitigated 2026-08-19 on the Bridge. Successful PIN pairing now returns a random 32-byte `pairing.accept.reconnectToken`; reconnect authorization requires the same token on `hello` and compares its raw-byte SHA-256 against the stored hash in fixed time. DeviceId alone, missing/wrong tokens, legacy rows without a hash, and revoked devices are untrusted.
- **Storage:** The Bridge stores only `reconnectTokenSha256` and does not log token material. Legacy devices must pair again. The Bridge store is not Keychain and never needs the plaintext token after `pairing.accept`.
- **Remaining:** IOS-005 must persist `pairing.accept.reconnectToken` in Keychain (not UserDefaults), send it on every hello, and pin the Bridge TLS certificate. Until that client exists, the end-to-end iPhone reconnect path is not verified.
- **Related:** SECURITY.md, BRIDGE-006, IOS-005, ADR 0003, ADR 0013, ADR 0014, KI-003

## KI-004 — Entitlements are code-level only

- **Symptoms:** Free/Premium/Pro gates exist as functions; StoreKit is absent; UI does not enforce limits.
- **Related:** ADR 0008, IOS-010

## KI-007 — HealthKit read auth indistinguishable from empty list (IOS-003)

- **Symptoms:** HealthKit does not expose reliable read-authorization status; an empty session list after prompt cannot distinguish “user denied read” from “no Sim Racing workouts yet.” IOS-003 uses one `.needsHealthAccess` empty state for both.
- **Workaround:** Copy mentions allowing Health access and starting a Watch workout; Open Settings CTA opens app Settings (user navigates to Health manually).
- **Related:** IOS-003, `docs/superpowers/specs/2026-08-19-ios-003-healthkit-permissions-design.md`

## KI-008 — WatchConnectivity summary sync not E2E verified (WATCH-003 / IOS-004)

- **Symptoms:** Watch → iPhone workout summary delivery via `WCSession.transferUserInfo` has not been exercised end-to-end on paired simulators or physical devices. Unit tests cover the DTO wire format, file outbox, and iOS ingest dedupe only.
- **Workaround:** Treat summary sync as implemented-but-unverified until a paired Watch/iPhone manual smoke pass is recorded.
- **Related:** WATCH-003, IOS-004, ADR 0012

## ANALYTICS-003 — HeartRateWindows (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `HeartRateWindows` averages workout HR in simulator-aligned lap/event windows using `workoutTime = simulatorTime + offset`. Returns `Unavailable` when offset is unknown, lap lacks `CompletedAt`, or no samples fall in the window — intentional per ADR 0004.

## ANALYTICS-002 — RaceReport (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `RaceReport` and `RaceReportBuilder.FromDriverSession` use `DataPresence` / `OptionalValue<T>` for missing fields (simulator metadata, positions, peak-HR event association). `PeakHeartRateAssociatedEvent` remains `Unavailable` when `TimelineOffset` is not available — intentional per ADR 0004, not a defect.

## BRIDGE-004 — Session lifecycle tracker (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `SessionLifecycleTracker` is wired into `BridgeRuntime` and dedupes by `(SessionId, RaceEventType, sessionNum, lapNumber)` before race-event logging and trusted-client broadcast. Live iRacing mmap session/lap ticks are produced by BRIDGE-003 / BRIDGE-008.
