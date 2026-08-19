# Known issues

Do not hide defects because they are outside the current task.

| ID | Date | Component | Severity | Status |
| --- | --- | --- | --- | --- |
| KI-001 | 2026-08-18 | Apple apps | High (blocks Phase 1–2) | Closed |
| KI-002 | 2026-08-18 | Bridge / iRacing | Medium (live still needs sim + memmap) | Open |
| KI-003 | 2026-08-18 | Protocol | Low | Open (TLS remaining) |
| KI-004 | 2026-08-18 | Product | Medium | Open |
| KI-005 | 2026-08-18 | Android / Wear OS | Medium (blocks Phase 9) | Open |
| KI-006 | 2026-08-18 | Bridge / Security | Low | Open (Phase 0 limitation) |
| KI-007 | 2026-08-19 | iOS / HealthKit | Low | Open (by design) |

## KI-001 — Apple project not generated

- **Status:** Closed 2026-08-18. XcodeGen `SimPulse.xcodeproj` exists; iOS 26.5 + watchOS 26.5 simulators installed on `simpulse-mac`. First compile and WATCH-001 unit tests succeeded. Remaining Apple work is WATCH-002 / WATCH-003 / IOS-001, not missing toolchain.
- **Symptoms (historical):** No `.xcodeproj` on Windows; missing simulator runtimes on the Mac.
- **Related:** ADR 0009, ADR 0012, INFRA-003, INFRA-004, WATCH-001

## KI-002 — Live iRacing still requires a running sim + memmap

- **Symptoms:** Without `SIMPULSE_FIXTURE_PATH`, Bridge probes `Local\IRSDKMemMapFileName`. If the map is missing at process start (iRacing closed, memmap disabled, or non-Windows), `IsAvailableAsync` is false but `BridgeRuntime` still enters `SubscribeAsync`. The adapter polls `TryOpen` and starts the session when the mmap appears. No live YAML or variable-table telemetry until the official map is present and `irsdk_stConnected` is set.
- **Reproduction:** Run Bridge without `SIMPULSE_FIXTURE_PATH` on a PC without iRacing, or with iRacing running but `irsdkEnableMem` off.
- **Workaround:** Replay `tests/fixtures/telemetry/iracing-practice-short.json`, or start iRacing with memory telemetry enabled (`irsdkEnableMem=1`; Bridge may already be running).
- **Suspected cause:** Live bytes are read only when the official mmap is present and `irsdk_stConnected` is set. CI never requires a live session.
- **Done in adapters (BRIDGE-008, not live-verified):** player car (`DriverCarIdx`), `SessionNum`, `SessionTime`, and `sessionInfoUpdate` YAML cache. mmap Latin1 YAML string is reused when `sessionInfoUpdate` is unchanged (BUG-005). Available identity changes re-run `Parse` on the cached YAML (BUG-004). Lap tracking resets on Available `SessionNum`. LapStart/LapComplete carry `sessionNum` so tracker keys do not drop race lap 1. Latest `varBuf` by tickCount. `IRSDKDataValidEvent` wait is best-effort (missing/timeout → false, caller still reads, then poll idle). Trusted clients still get race-events only — no 60 Hz WebSocket telemetry frames.
- **Remaining:** live smoke with a real sim + mmap (`irsdkEnableMem=1`). ANALYTICS-003 `RaceReportBuilder` peak-event wiring stays out of scope.
- **Related:** ADR 0006, BRIDGE-003, BRIDGE-008, BUG-001, BUG-004

## KI-003 — Transport is still cleartext (tray pairing UX shipped)

- **Symptoms:** Loopback WebSocket (`http://127.0.0.1:8742/ws/` by default) accepts clients. Windows interactive Bridge runs as `WinExe` with `NotifyIconPairingUx` (PIN balloon, **Show current PIN** / **Pair new device** / **Exit**). If tray startup fails or times out (5s), Bridge falls back to `ConsolePairingUx` and keeps running. Non-interactive, `SIMPULSE_BRIDGE_TRAY=0`, or Linux uses `ConsolePairingUx` only. Trusted clients receive `simulator.race-event` envelopes (no biometric / telemetry-frame payloads). PIN is logged at Information when the window opens. TLS is not implemented. Default bind is loopback (`0.0.0.0` is opt-in).
- **Workaround:** On Windows, read the PIN from the tray balloon or **Show current PIN** (does not rotate the PIN; after the window closes it reports `pairing window closed`). WinExe file logs are under `%LOCALAPPDATA%\SimPulse\logs` (`SIMPULSE_LOG_DIR` / `SIMPULSE_LOG_FILE=0`) and include the coordinator Information `Pin=` line — restrict that directory. Persist trusted devices with `SIMPULSE_TRUSTED_DEVICES_PATH`. For console logs while debugging, `dotnet run --property:OutputType=Exe` (see `docs/DEVELOPMENT.md`). Keep Bridge on loopback until TLS exists.
- **Suspected cause:** BRIDGE-005 + BRIDGE-006 shipped listen/accept, PIN window, and race-event broadcast; BRIDGE-007 shipped tray UX. TLS remains Phase 0 follow-up.
- **Related:** PROTO-001, BRIDGE-005, BRIDGE-006, BRIDGE-007, ADR 0003, KI-006

## KI-005 — Android and Wear OS projects not generated

- **Symptoms:** No Gradle project under `apps/android` or `apps/wearos`; Wear AVD exists on Windows but cannot build an app.
- **Workaround:** Develop Bridge/.NET and protocol contracts; treat Phase 9 as documented only until AND-001.
- **Suspected cause:** Phase 9 deferred until the Apple vertical slice (Phases 1–5).
- **Related:** ADR 0010, AND-001, WEAROS-001

## KI-006 — Reconnect trust is DeviceId-only (Phase 0)

- **Symptoms:** After PIN pairing, reconnecting clients send `hello` with a previously trusted DeviceId and are accepted without PIN re-entry. DeviceId is client-asserted, sent in cleartext over unencrypted WebSocket; there is no per-device reconnect secret and no TLS.
- **Impact:** Any LAN peer that knows or guesses a trusted DeviceId can impersonate that device until revoke.
- **Workaround:** Revoke compromised DeviceIds; keep Bridge on loopback unless LAN pairing is intentional; treat DeviceId as a capability token, not proof of possession.
- **Suspected cause:** Phase 0 scope — PIN establishes trust once; reconnect hardening (TLS, per-device secrets) is deferred.
- **Related:** SECURITY.md, BRIDGE-006, ADR 0003, KI-003

## KI-004 — Entitlements are code-level only

- **Symptoms:** Free/Premium/Pro gates exist as functions; StoreKit is absent; UI does not enforce limits.
- **Related:** ADR 0008, IOS-010

## KI-007 — HealthKit read auth indistinguishable from empty list (IOS-003)

- **Symptoms:** HealthKit does not expose reliable read-authorization status; an empty session list after prompt cannot distinguish “user denied read” from “no Sim Racing workouts yet.” IOS-003 uses one `.needsHealthAccess` empty state for both.
- **Workaround:** Copy mentions allowing Health access and starting a Watch workout; Open Settings CTA opens app Settings (user navigates to Health manually).
- **Related:** IOS-003, `docs/superpowers/specs/2026-08-19-ios-003-healthkit-permissions-design.md`

## ANALYTICS-003 — HeartRateWindows (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `HeartRateWindows` averages workout HR in simulator-aligned lap/event windows using `workoutTime = simulatorTime + offset`. Returns `Unavailable` when offset is unknown, lap lacks `CompletedAt`, or no samples fall in the window — intentional per ADR 0004.

## ANALYTICS-002 — RaceReport (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `RaceReport` and `RaceReportBuilder.FromDriverSession` use `DataPresence` / `OptionalValue<T>` for missing fields (simulator metadata, positions, peak-HR event association). `PeakHeartRateAssociatedEvent` remains `Unavailable` when `TimelineOffset` is not available — intentional per ADR 0004, not a defect.

## BRIDGE-004 — Session lifecycle tracker (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `SessionLifecycleTracker` is wired into `BridgeRuntime` and dedupes by `(SessionId, RaceEventType, sessionNum, lapNumber)` before race-event logging and trusted-client broadcast. Live iRacing mmap session/lap ticks are produced by BRIDGE-003 / BRIDGE-008.
