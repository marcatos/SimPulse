# Known issues

Do not hide defects because they are outside the current task.

| ID | Date | Component | Severity | Status |
| --- | --- | --- | --- | --- |
| KI-001 | 2026-08-18 | Apple apps | High (blocks Phase 1–2) | Open |
| KI-002 | 2026-08-18 | Bridge / iRacing | High (blocks Phase 3 live) | Open |
| KI-003 | 2026-08-18 | Protocol | Low | Open (UX remaining) |
| KI-004 | 2026-08-18 | Product | Medium | Open |

## KI-001 — Apple project not generated

- **Symptoms:** No `.xcodeproj` / `.xcworkspace`. Swift sources cannot be built here.
- **Reproduction:** Run `scripts/build-ios.sh` or look for Xcode on the Windows bootstrap machine.
- **Workaround:** Develop domain/analytics/protocol on .NET; treat Swift as a specification.
- **Suspected cause:** No macOS/Xcode in the current environment (by design for Phase 0).
- **Related:** ADR 0009, INFRA-003, WATCH-001, IOS-001

## KI-002 — Live iRacing adapter not implemented

- **Symptoms:** Bridge fixture adapter works; live sim detection always reports unavailable.
- **Reproduction:** Run Bridge without `SIMPULSE_FIXTURE_PATH` on a PC without iRacing, or with iRacing running.
- **Workaround:** Replay `tests/fixtures/telemetry/iracing-practice-short.json`.
- **Suspected cause:** Phase 0 intentionally ships a stub `IRacingAdapter`.
- **Related:** ADR 0006, BRIDGE-003

## KI-003 — Transport pairing UX still console-only

- **Symptoms:** Loopback WebSocket (`http://127.0.0.1:8742/ws/` by default) accepts clients. Pairing requires an open window (`BeginPairingWindow`): 6-digit CSPRNG PIN, 5-minute expiry, success closes the window, 5 failed attempts lock until a new window. Trusted clients receive `simulator.race-event` envelopes (no biometric / telemetry-frame payloads). PIN is logged at Information each time a window opens; there is no tray UI. TLS is not implemented. Default bind is loopback (`0.0.0.0` is opt-in).
- **Workaround:** Read the current window PIN from Bridge console logs. Persist trusted devices with `SIMPULSE_TRUSTED_DEVICES_PATH`.
- **Suspected cause:** BRIDGE-005 + BRIDGE-006 shipped listen/accept, PIN window, and race-event broadcast; tray UX is BRIDGE-007; TLS is a later security step (ADR 0003).
- **Related:** PROTO-001, BRIDGE-005, BRIDGE-006, BRIDGE-007, ADR 0003

## KI-004 — Entitlements are code-level only

- **Symptoms:** Free/Premium/Pro gates exist as functions; StoreKit is absent; UI does not enforce limits.
- **Related:** ADR 0008, IOS-010

## ANALYTICS-002 — RaceReport (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `RaceReport` and `RaceReportBuilder.FromDriverSession` use `DataPresence` / `OptionalValue<T>` for missing fields (simulator metadata, positions, peak-HR event association). `PeakHeartRateAssociatedEvent` remains `Unavailable` when `TimelineOffset` is not available — intentional per ADR 0004, not a defect.

## BRIDGE-004 — Session lifecycle tracker (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `SessionLifecycleTracker` is wired into `BridgeRuntime` and dedupes by `(SessionId, RaceEventType, lapNumber attribute or empty)` before race-event logging and trusted-client broadcast. Live iRacing mmap ticks remain BRIDGE-003.
