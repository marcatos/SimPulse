# Engineering backlog

Canonical until an external tracker exists.

Statuses: `BACKLOG` | `READY` | `IN_PROGRESS` | `BLOCKED` | `DONE`  
Priorities: `P0` | `P1` | `P2` | `P3`

Do not mark DONE unless acceptance criteria are met on a real platform.

## Phase 0

### INFRA-001 — Repository bootstrap

- **Area:** Infra
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** none
- **Acceptance criteria:**
  - Git repo with documented layout
  - Docs system present
  - .NET solution tests run on Windows
  - Apple builds recorded as not executed
  - CURRENT_STATE is truthful
- **Notes:** Phase 0 bootstrap. 20 .NET tests passed on Windows 2026-08-18.

### INFRA-002 — CI for .NET

- **Area:** Infra
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** INFRA-001
- **Acceptance criteria:** GitHub Actions runs `dotnet test` on Windows and Ubuntu; artifacts (if any) use `retention-days: 7`.
- **Notes:** `.github/workflows/ci.yml` matrix runs `dotnet test SimPulse.sln` on `windows-latest` and `ubuntu-latest`; test-result artifacts use `retention-days: 7`. PR #1 checks green 2026-08-18 (Actions runs 32127173207, 32127216936).

### INFRA-003 — Apple CI placeholder

- **Area:** Infra
- **Priority:** P2
- **Status:** DONE
- **Dependencies:** INFRA-001, ADR 0009
- **Acceptance criteria:** Workflow documents that xcodebuild is skipped until a project exists; does not fake success.

### DOCS-001 — Documentation system

- **Area:** Docs
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** none
- **Acceptance criteria:** Files listed in the bootstrap spec exist and an agent can recover project state from them alone.

## Protocol / domain

### PROTO-001 — Freeze protocol v1 schema and fixtures

- **Area:** Protocol
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** none
- **Acceptance criteria:** JSON Schema + C# envelope tests for unknown fields, unknown types, version rejection; fixtures in `tests/fixtures/protocol`.
- **Notes:** Blocks IOS-006 and BRIDGE-005. Extra message fixtures can still be added with BRIDGE-005.

### PROTO-002 — Time-sync message contract

- **Area:** Protocol
- **Priority:** P1
- **Status:** DONE
- **Dependencies:** PROTO-001
- **Acceptance criteria:** Request/response types + offset calculation tests with skewed clocks.

### PROTO-003 — Swift protocol codec

- **Area:** Protocol
- **Priority:** P1
- **Status:** BLOCKED
- **Dependencies:** PROTO-001, macOS/Xcode
- **Acceptance criteria:** Swift round-trip of the same fixtures as C#.
- **Notes:** KI-001

### ANALYTICS-001 — HR and energy summary functions

- **Area:** Analytics
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** none
- **Acceptance criteria:** Average, max, percentiles, calories/session, calories/hour from biometric fixtures; no medical labels.

### ANALYTICS-002 — RaceReport model (no UI)

- **Area:** Analytics
- **Priority:** P1
- **Status:** DONE
- **Dependencies:** ANALYTICS-001
- **Acceptance criteria:** Structured report with `DataPresence` for missing fields.

### ANALYTICS-003 — HR by lap and event windows

- **Area:** Analytics
- **Priority:** P1
- **Status:** DONE
- **Dependencies:** ANALYTICS-001, ADR 0004
- **Acceptance criteria:** Functions take a correlated timeline; refuse to join when offset unknown.

## Watch

### WATCH-001 — Workout session lifecycle

- **Area:** Watch
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** Xcode project (ADR 0009)
- **Acceptance criteria:** Start/end Sim Racing workout via HealthKit; survives iPhone disconnect; unit tests against `WorkoutDataSource`.

### WATCH-002 — Glanceable live UI

- **Area:** Watch
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** WATCH-001
- **Acceptance criteria:** Elapsed, current/avg/max HR, active calories, state; large numbers; Always On considered.

### WATCH-003 — Persist and sync summary to iPhone

- **Area:** Watch
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** WATCH-001, IOS-004
- **Acceptance criteria:** Workout saved in HealthKit; summary queued if iPhone absent; delivered later.

## iOS

### IOS-001 — Session store and history UI

- **Area:** iOS
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** Xcode, ADR 0002
- **Acceptance criteria:** List of local sessions from mock + HealthKit ports.

### IOS-002 — Session detail and charts

- **Area:** iOS
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** IOS-001, ANALYTICS-001
- **Acceptance criteria:** Duration, HR avg/max, calories, HR over time from stored samples.

### IOS-003 — HealthKit permissions

- **Area:** iOS
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** Xcode
- **Acceptance criteria:** Minimum read/write types; usage strings; denial is a documented empty state.

### IOS-004 — WatchConnectivity ingest

- **Area:** iOS
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** WATCH-003
- **Acceptance criteria:** Idempotent merge by session ID.

### IOS-005 — Bridge pairing client

- **Area:** iOS
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** PROTO-001, BRIDGE-006, Xcode
- **Acceptance criteria:** Manual IP/PIN pairing; persist trusted Bridge identity; revoke.

### IOS-006 — Receive simulator session and correlate

- **Area:** iOS
- **Priority:** P0
- **Status:** BLOCKED
- **Dependencies:** IOS-005, BRIDGE-004, PROTO-002
- **Acceptance criteria:** DriverSession with workout + optional simulator; honest missing data.

### IOS-007 — Race report screen

- **Area:** iOS
- **Priority:** P1
- **Status:** BACKLOG
- **Dependencies:** IOS-006, ANALYTICS-002
- **Acceptance criteria:** Renders RaceReport model only.

### IOS-010 — Entitlement gates in UI

- **Area:** iOS
- **Priority:** P2
- **Status:** BACKLOG
- **Dependencies:** ADR 0008
- **Acceptance criteria:** History cap and Bridge features consult `CapabilityGate`; no StoreKit required.

## Bridge

### BRIDGE-001 — Host lifecycle and logging

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** none
- **Acceptance criteria:** Worker starts/stops, structured logs, configurable log level, cancellation.

### BRIDGE-002 — Fixture adapter

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** none
- **Acceptance criteria:** Replay JSON fixture to normalized events; tests without iRacing.

### BRIDGE-003 — iRacing mmap reader

- **Area:** Bridge
- **Priority:** P0
- **Status:** BACKLOG
- **Dependencies:** ADR 0006
- **Acceptance criteria:** Detect sim, parse session YAML subset (track, car, session type); no GPL deps; copyright notice if headers vendored.

### BRIDGE-004 — Session and lap lifecycle

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BRIDGE-002 or BRIDGE-003
- **Acceptance criteria:** SESSION_START/END, LAP_START/COMPLETE from normalized ticks; idempotent.
- **Notes:** `SessionLifecycleTracker` is wired in `BridgeRuntime` for log/broadcast dedupe. Live mmap ticks are still BRIDGE-003.

### BRIDGE-005 — WebSocket server

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** PROTO-001
- **Acceptance criteria:** Accepts paired clients; reconnect; ignores unknown messages; no biometrics outbound.
- **Notes:** Loopback `HttpListener` transport (`127.0.0.1:8742/ws/` by default; `0.0.0.0` is opt-in). Pairing gate is BRIDGE-006. Reconnect after a client close is covered by `Accepts_second_client_after_first_closes`.

### BRIDGE-006 — Pairing and trusted devices

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BRIDGE-005, SECURITY.md
- **Acceptance criteria:** PIN pairing, persist device id, revoke, unpaired clients get no telemetry.
- **Notes:** Six-digit CSPRNG PIN (not persisted). `BeginPairingWindow()` is invoked once at Bridge host start today; after success, expiry, or 5-attempt lockout the window stays closed until **process restart** (or future tray action in BRIDGE-007). Reconnect trust is DeviceId-only — client-asserted, cleartext, no per-device secret, no TLS; see SECURITY.md and KI-006. `JsonFileTrustedDeviceStore` when `SIMPULSE_TRUSTED_DEVICES_PATH` is set; otherwise in-memory. PIN logged at Information when the window opens.

### BRIDGE-007 — Tray / background UX

- **Area:** Bridge
- **Priority:** P2
- **Status:** BACKLOG
- **Dependencies:** BRIDGE-001
- **Acceptance criteria:** User can run Bridge without a console window; pairing PIN visible; **Pair new device** calls `BeginPairingWindow()` so a new PIN window opens without process restart (today restart is required after the initial window closes — see KI-003).

## Dependency edges (do not parallelize these pairs)

```text
PROTO-001  →  BRIDGE-005, IOS-005, PROTO-003
PROTO-002  →  IOS-006
WATCH-001  →  WATCH-002, WATCH-003
WATCH-003  →  IOS-004
IOS-001    →  IOS-002
IOS-005    →  IOS-006
BRIDGE-003 or BRIDGE-002 → BRIDGE-004
BRIDGE-004 → IOS-006
Xcode      →  all WATCH-* and IOS-* except design/docs
```
