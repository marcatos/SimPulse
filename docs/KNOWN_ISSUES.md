# Known issues

Do not hide defects because they are outside the current task.

| ID | Date | Component | Severity | Status |
| --- | --- | --- | --- | --- |
| KI-001 | 2026-08-18 | Apple apps | High (blocks Phase 1–2) | Open |
| KI-002 | 2026-08-18 | Bridge / iRacing | High (blocks Phase 3 live) | Open |
| KI-003 | 2026-08-18 | Protocol | Medium | Open |
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

## KI-003 — Transport is in-process only

- **Symptoms:** No LAN WebSocket server, no pairing UI.
- **Workaround:** Protocol unit tests round-trip JSON envelopes.
- **Related:** PROTO-001, BRIDGE-005, ADR 0003

## KI-004 — Entitlements are code-level only

- **Symptoms:** Free/Premium/Pro gates exist as functions; StoreKit is absent; UI does not enforce limits.
- **Related:** ADR 0008, IOS-010

## ANALYTICS-002 — RaceReport (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `RaceReport` and `RaceReportBuilder.FromDriverSession` use `DataPresence` / `OptionalValue<T>` for missing fields (simulator metadata, positions, peak-HR event association). `PeakHeartRateAssociatedEvent` remains `Unavailable` when `TimelineOffset` is not available — intentional per ADR 0004, not a defect.

## BRIDGE-004 — Session lifecycle tracker (2026-08-18)

- **Status:** No known defects introduced.
- **Note:** `SessionLifecycleTracker` dedupes by `(SessionId, RaceEventType, lapNumber attribute or empty)`. Standalone until BRIDGE-003 wires normalized ticks through `BridgeRuntime`.
