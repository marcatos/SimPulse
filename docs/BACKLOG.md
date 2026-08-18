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
- **Status:** DONE
- **Dependencies:** ADR 0006
- **Acceptance criteria:** Detect sim, parse session YAML subset (track, car, session type); no GPL deps; copyright notice if headers vendored.
- **Notes:** First-party `IIracingSharedMemory` + YAML subset parser. Tests use `FakeIracingSharedMemory` and synthetic `tests/fixtures/iracing/session-info-sample.yaml`. Live path opens `Local\IRSDKMemMapFileName` (no throw if missing). No IRSDKSharper. See `docs/handoffs/BRIDGE-003.md`.

### BRIDGE-004 — Session and lap lifecycle

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BRIDGE-002 or BRIDGE-003
- **Acceptance criteria:** SESSION_START/END, LAP_START/COMPLETE from normalized ticks; idempotent.
- **Notes:** `SessionLifecycleTracker` is wired in `BridgeRuntime` for log/broadcast dedupe. Live mmap session ticks come from BRIDGE-003.

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
- **Notes:** Six-digit CSPRNG PIN (not persisted). `BeginPairingWindow()` runs at Bridge host start and again from **Pair new device** via `TrayPairingPresenter` (BRIDGE-007). Reconnect trust is DeviceId-only — client-asserted, cleartext, no per-device secret, no TLS; see SECURITY.md and KI-006. `JsonFileTrustedDeviceStore` when `SIMPULSE_TRUSTED_DEVICES_PATH` is set; otherwise in-memory. PIN logged at Information when the window opens.

### BUG-001 — Pre-merge iRacing mmap review fixes

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BRIDGE-003
- **Acceptance criteria:**
  - BridgeRuntime enters SubscribeAsync when `IsAvailableAsync` is false so mmap can appear after startup
  - IRSDK session YAML decoded as Windows-1252 / Latin1
  - KI-002 / BRIDGE-003 handoff document first-DriverInfo car and sessionInfoUpdate follow-up
- **Notes:** Whole-branch Important findings for `feat/iracing-mmap-hr-windows`. Should-fix done: shared `IracingHeaderReader` offsets; Debug on repeated TryOpen. Session YAML Latin1; runtime always subscribes. Player car / `sessionInfoUpdate` / ANALYTICS-003 wiring left as KI-002 follow-ups.

### BRIDGE-007 — Tray / background UX

- **Area:** Bridge
- **Priority:** P2
- **Status:** DONE
- **Dependencies:** BRIDGE-001
- **Acceptance criteria:** User can run Bridge without a console window; pairing PIN visible; **Pair new device** calls `BeginPairingWindow()` so a new PIN window opens without process restart.
- **Notes:** Windows interactive host registers `NotifyIconPairingUx` on an STA `Application.Run` thread and builds as `WinExe` (Linux stays `net8.0` / `Exe`). `SIMPULSE_BRIDGE_TRAY=0` or non-interactive uses `ConsolePairingUx` only. **Show current PIN** redisplays the last PIN only while the window is open. Tray startup failure falls back to console. See `docs/handoffs/BRIDGE-007.md`, `docs/handoffs/BUG-002.md`, `docs/handoffs/BUG-003.md`, and `docs/DEVELOPMENT.md`.

### BRIDGE-008 — iRacing variable table

- **Area:** Bridge
- **Priority:** P1
- **Status:** DONE
- **Dependencies:** BRIDGE-003, BUG-001
- **Acceptance criteria:**
  - Latest IRSDK varBuf by tickCount
  - `DriverCarIdx`, `SessionNum`, `SessionTime`, `Lap` read when present
  - YAML re-parsed only on `sessionInfoUpdate` change
  - Re-resolve vehicle/session type from cached YAML when Available `SessionNum` or `DriverCarIdx` changes (BUG-004)
  - Lap tracking resets when Available `SessionNum` changes (no second SessionStart)
  - LapStart/LapComplete from Lap increases
  - No 60 Hz WebSocket frames
  - Tests pass without a live sim
- **Notes:** Header/var parsers + YAML list match (unmatched lookups → Unavailable). Live path reads latest varBuf; `IracingLiveSession` caches YAML on `sessionInfoUpdate`, re-resolves identity, emits lap events, and leaves `NormalizedSimulatorUpdate.Telemetry` null. Verified with synthetic buffers + `FakeIracingSharedMemory` (`dotnet test SimPulse.sln --configuration Release`, 126 passed on Windows 2026-08-18). No live-on-track / iRacing session was run. Live mmap smoke remains KI-002. See `docs/handoffs/BRIDGE-008.md`.

### BUG-005 — SessionNum in lap keys + YAML decode cache

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BRIDGE-008, BUG-004
- **Acceptance criteria:**
  - LapStart/LapComplete include `sessionNum` (and keep `lapNumber`)
  - `SessionLifecycleTracker.BuildKey` includes `sessionNum` so practice→race lap 1 is not dropped
  - Two LapStart with same SessionId and lapNumber=1 but different sessionNum both Observe() as non-null; same sessionNum duplicate is dropped
  - mmap snapshot reader caches last `(SessionInfoUpdate, yaml string)` for the open map; unchanged update reuses the cached string and still reads the latest telemetry row
  - Disconnect/Close clears the YAML cache
  - Synthetic two-snapshot test: same sessionInfoUpdate + mutated yaml bytes → SessionYaml stays the first decode
  - `dotnet test SimPulse.sln --configuration Release` passes
- **Notes:** Whole-branch Critical + Important (YAML-every-frame) for `feat/iracing-var-table`. Does not start TLS/Apple, torn-read retry, EventWaitHandle caching, or full mmap slice copy.

### BUG-004 — Re-resolve session type from cached YAML

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BRIDGE-008
- **Acceptance criteria:**
  - Re-tokenize YAML only when `SessionInfoUpdate` changes (same update + mutated YAML keeps cached vehicle)
  - When `SessionInfoUpdate` is unchanged but Available `DriverCarIdx` or `SessionNum` identity changes, re-run `Parse` on the cached YAML string with the new telemetry args
  - Two-drivers fixture, constant update, SessionNum 0 then 1 → Practice then Race; no second SessionStart
  - `dotnet test SimPulse.sln --configuration Release` passes
- **Notes:** Review finding: cache skipped `Parse` entirely when the YAML tick was unchanged, so practice→race waited on the next YAML update. Does not take over BRIDGE-008 Task 4. See `docs/handoffs/BUG-004.md`.

### BUG-003 — Pre-merge tray UX review fixes (round 2)

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BUG-002
- **Acceptance criteria:**
  - File logger write failures (IO/ACL) disable further file writes and never throw
  - Pairing PIN is logged at Information only from `PairingCoordinator.BeginPairingWindow` (`Pin=`); UX adapters do not log PIN
  - **Show current PIN** does not redisplay a consumed, expired, locked, or otherwise closed window PIN; presenter clears last PIN
  - Core tests cover the above without WinForms; `dotnet test SimPulse.sln --configuration Release` passes
- **Notes:** Whole-branch Important findings (round 2) for `feat/bridge-007-tray`. See `docs/handoffs/BUG-003.md`. File logger disables after first IO/ACL failure. Coordinator keeps `Pin=` at Information; UX `ShowPin` does not. `IPairingUx.ClearPin()` + presenter consults `IsPairingWindowOpen()`.

### BUG-002 — Pre-merge tray UX review fixes

- **Area:** Bridge
- **Priority:** P0
- **Status:** DONE
- **Dependencies:** BRIDGE-007
- **Acceptance criteria:**
  - WinExe writes rolling/simple MEL file logs under `%LOCALAPPDATA%\SimPulse\logs` (or user-profile equivalent); PIN is not logged in extra places; env-configurable
  - STA/NotifyIcon failure or 5s ready timeout falls back to console UX (or continues) and logs ERROR; process stays alive
  - Pair new device menu/presenter exceptions are caught and logged ERROR (no ThreadExceptionDialog)
  - **Show current PIN** redisplays the last PIN without `BeginPairingWindow`; last PIN stored on `ShowPin`; tooltip keeps PIN after balloon close
  - Core tests cover presenter/mode/file-log path without WinForms; `dotnet test SimPulse.sln --configuration Release` passes
- **Notes:** Whole-branch Important findings for `feat/bridge-007-tray`. MEL daily file logs; 5s tray ready timeout + console fallback; presenter/menu catch on Pair new device; **Show current PIN** via `RedisplayLastPin`. See `docs/handoffs/BUG-002.md`.

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
