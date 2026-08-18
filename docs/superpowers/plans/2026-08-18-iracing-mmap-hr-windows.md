# iRacing mmap + HR windows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close INFRA-002, implement ANALYTICS-003 (HR by lap/event windows that refuse joins without timeline offset), and implement BRIDGE-003 (first-party iRacing mmap reader: detect sim + parse session YAML subset) without GPL dependencies.

**Architecture:** Analytics stays pure (Domain only). Bridge hexagonal: `IIracingSharedMemory` port + Windows mmap adapter + YAML session-info parser inside the iRacing adapter; domain stays free of IRSDK types. CI verification is docs/status only (workflow already green).

**Tech Stack:** .NET 8, xUnit, `System.IO.MemoryMappedFiles`, first-party IRSDK constants with iRacing BSD copyright notice, Conventional Commits.

## Global Constraints

- Conventional Commits: `feat|fix|refactor|test|docs|chore|ci(<scope>): …`
- Hexagonal: domain/analytics/protocol have no Bridge references; IRSDK types stay in Bridge adapters.
- `OptionalValue<T>` / `DataPresence` — never invent measurements; refuse correlation when `TimelineOffset` unknown (ADR 0004).
- No GPL (no IRSDKSharper). Vendor only constants/layout needed; include iRacing copyright notice per ADR 0006 / THIRD_PARTY.md.
- Ordinary CI must never require a live iRacing session (TESTING.md).
- Logging: INFO default; start/steps/end/durations; never log raw telemetry buffers or biometrics.
- Files ≤300 lines; methods ≤60; split rather than grow.
- Update BACKLOG, CURRENT_STATE, KNOWN_ISSUES when ACs are met.
- Never change `git config`. Work only in this worktree.
- Do not implement WATCH-*, IOS-*, BRIDGE-007 tray, or Phase 8 sims.

---

## File map

| Path | Responsibility |
| --- | --- |
| `docs/BACKLOG.md`, `CURRENT_STATE.md`, `KNOWN_ISSUES.md` | INFRA-002 / ANALYTICS-003 / BRIDGE-003 / KI-002 status |
| `packages/analytics/SimPulse.Analytics/HeartRateWindows.cs` | HR averages in lap/event windows |
| `packages/analytics/SimPulse.Analytics.Tests/HeartRateWindowsTests.cs` | Offset-required tests |
| `apps/windows-bridge/SimPulse.Bridge.Core/Ports/IracingPorts.cs` | `IIracingSharedMemory` |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs` | Header constants + copyright |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSessionInfoParser.cs` | YAML subset → track/car/session type |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingHeaderReader.cs` | Parse header + session info blob from bytes |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/WindowsIracingSharedMemory.cs` | Open `Local\IRSDKMemMapFileName` |
| `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/IRacingAdapter.cs` | Replace stub with live + unavailable paths |
| `apps/windows-bridge/SimPulse.Bridge.Core.Tests/Iracing*.cs` | Synthetic buffer fixtures |
| `tests/fixtures/iracing/session-info-sample.yaml` | Synthetic session YAML |
| `docs/THIRD_PARTY.md` | Note vendored constants |

---

### Task 1: INFRA-002 — mark CI DONE

**Backlog:** INFRA-002  
**Files:**
- Modify: `docs/BACKLOG.md` (INFRA-002 → DONE)
- Modify: `docs/CURRENT_STATE.md` (note CI green)
- Modify: `docs/handoffs/INFRA-002.md` (create)

**Interfaces:** none (docs only)

- [ ] **Step 1: Verify CI already satisfies AC**

Confirm `.github/workflows/ci.yml` runs `dotnet test` on `windows-latest` and `ubuntu-latest` with `retention-days: 7`. Confirm recent GitHub Actions runs passed (PR #1 checks already showed green).

- [ ] **Step 2: Update docs**

Set INFRA-002 status DONE with notes citing green Actions. Create handoff. Do not invent new workflow steps unless something is missing from AC.

- [ ] **Step 3: Commit**

```text
docs(ci): mark INFRA-002 .NET GitHub Actions as done
```

---

### Task 2: ANALYTICS-003 — HR by lap and event windows

**Backlog:** ANALYTICS-003  
**Files:**
- Create: `packages/analytics/SimPulse.Analytics/HeartRateWindows.cs`
- Create: `packages/analytics/SimPulse.Analytics.Tests/HeartRateWindowsTests.cs`
- Modify: docs BACKLOG / CURRENT_STATE / KNOWN_ISSUES

**Interfaces:**
- Consumes: `HeartRateSample`, `Lap`, `RaceEvent`, `TimestampInstant`, `OptionalValue<TimeSpan>` timeline offset, `HeartRateMetrics`
- Produces:
  - `HeartRateWindows.AverageBpmInSimulatorWindow(IReadOnlyList<HeartRateSample> workoutSamples, TimestampInstant simulatorWindowStart, TimestampInstant simulatorWindowEnd, OptionalValue<TimeSpan> timelineOffset)`
  - `HeartRateWindows.AverageBpmForLap(IReadOnlyList<HeartRateSample> workoutSamples, Lap lap, OptionalValue<TimeSpan> timelineOffset)`
  - `HeartRateWindows.AverageBpmAroundEvent(IReadOnlyList<HeartRateSample> workoutSamples, RaceEvent raceEvent, TimeSpan halfWindow, OptionalValue<TimeSpan> timelineOffset)`
- Semantics: `timelineOffset` is **workoutTime = simulatorTime + offset** (workout UTC ≈ simulator UTC + offset). If offset Presence is not Available → return `OptionalValue<double>.Unavailable()` without joining. Empty samples → Unavailable. Lap without CompletedAt → Unavailable for AverageBpmForLap.

- [ ] **Step 1: Failing tests**

```csharp
using SimPulse.Domain;

namespace SimPulse.Analytics.Tests;

public sealed class HeartRateWindowsTests
{
    [Fact]
    public void Refuses_join_when_timeline_offset_unknown()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        HeartRateSample[] samples =
        [
            new(new TimestampInstant(start.AddMinutes(1), ClockSource.WorkoutSession), 120)
        ];
        Lap lap = new(
            SessionId.New(),
            1,
            new TimestampInstant(start, ClockSource.SimulatorSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(2), ClockSource.SimulatorSession)),
            OptionalValue<TimeSpan>.Available(TimeSpan.FromMinutes(2)),
            OptionalValue<int>.Unavailable());

        OptionalValue<double> avg = HeartRateWindows.AverageBpmForLap(
            samples,
            lap,
            OptionalValue<TimeSpan>.Unavailable());

        Assert.Equal(DataPresence.Unavailable, avg.Presence);
    }

    [Fact]
    public void Averages_hr_inside_lap_after_applying_offset()
    {
        DateTimeOffset simStart = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        // Workout clock is 5 seconds ahead of simulator clock.
        TimeSpan offset = TimeSpan.FromSeconds(5);
        HeartRateSample[] samples =
        [
            new(new TimestampInstant(simStart.AddSeconds(5), ClockSource.WorkoutSession), 100),  // maps to sim t=0
            new(new TimestampInstant(simStart.AddSeconds(65), ClockSource.WorkoutSession), 140), // maps to sim t=60
            new(new TimestampInstant(simStart.AddSeconds(125), ClockSource.WorkoutSession), 180) // maps to sim t=120 — outside lap ending at 90s
        ];
        Lap lap = new(
            SessionId.New(),
            1,
            new TimestampInstant(simStart, ClockSource.SimulatorSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(simStart.AddSeconds(90), ClockSource.SimulatorSession)),
            OptionalValue<TimeSpan>.Available(TimeSpan.FromSeconds(90)),
            OptionalValue<int>.Unavailable());

        OptionalValue<double> avg = HeartRateWindows.AverageBpmForLap(
            samples,
            lap,
            OptionalValue<TimeSpan>.Available(offset));

        Assert.True(avg.TryGet(out double value));
        Assert.Equal(120, value, 0); // (100+140)/2
    }

    [Fact]
    public void Event_window_uses_half_window_around_simulator_event()
    {
        DateTimeOffset simEvent = DateTimeOffset.Parse("2026-08-18T10:01:00Z");
        TimeSpan offset = TimeSpan.Zero;
        HeartRateSample[] samples =
        [
            new(new TimestampInstant(simEvent.AddSeconds(-2), ClockSource.WorkoutSession), 110),
            new(new TimestampInstant(simEvent, ClockSource.WorkoutSession), 130),
            new(new TimestampInstant(simEvent.AddSeconds(2), ClockSource.WorkoutSession), 150),
            new(new TimestampInstant(simEvent.AddSeconds(10), ClockSource.WorkoutSession), 200)
        ];
        RaceEvent evt = RaceEvent.Create(
            SessionId.New(),
            RaceEventType.LapComplete,
            new TimestampInstant(simEvent, ClockSource.SimulatorSession));

        OptionalValue<double> avg = HeartRateWindows.AverageBpmAroundEvent(
            samples,
            evt,
            TimeSpan.FromSeconds(3),
            OptionalValue<TimeSpan>.Available(offset));

        Assert.True(avg.TryGet(out double value));
        Assert.Equal(130, value, 0); // 110,130,150 only
    }
}
```

- [ ] **Step 2: RED** — missing type

- [ ] **Step 3: Implement `HeartRateWindows`** using `HeartRateMetrics.AverageBpm` on filtered samples. Inclusive window bounds. Document offset convention in a one-line XML comment.

- [ ] **Step 4: Full suite PASS**

- [ ] **Step 5: Mark ANALYTICS-003 DONE. Commit:**

```text
feat(analytics): add HR windows gated by timeline offset
```

---

### Task 3: BRIDGE-003 — iRacing mmap reader

**Backlog:** BRIDGE-003  
**Files:** (see file map) replace stub `IRacingAdapter`

**Interfaces:**
- `IIracingSharedMemory`: `bool TryOpen()`, `void Close()`, `bool TryReadSnapshot(out IracingMemorySnapshot snapshot)` where snapshot holds `ReadOnlyMemory<byte> HeaderAndBuffers` or structured fields the parser needs
- Prefer snapshot DTO with `string? SessionYaml` + `bool Connected` for tests without full binary layout
- `IracingSessionInfoParser.Parse(string yaml)` → record with `OptionalValue` track id/name, vehicle id/name, session type string
- `IRacingAdapter(IIracingSharedMemory memory, IClock clock, ILogger?)` — `IsAvailableAsync` true when `TryOpen()` succeeds; `SubscribeAsync` yields `NormalizedSimulatorUpdate` with SessionStart once when connected + session snapshot fields filled from YAML; when memory unavailable behave like old stub (unavailable / empty stream)
- Default DI in `Program.cs` uses `WindowsIracingSharedMemory`; tests inject a fake that returns synthetic YAML

**YAML subset to parse (synthetic fixture):** extract commonly present IRSDK keys when present:
- Track: `WeekendInfo:TrackName` and/or `TrackID` (string/int as string id)
- Car: `DriverInfo:Drivers` first driver `CarPath` / `CarScreenName` if present; otherwise Unavailable
- Session type: `SessionInfo:Sessions` current session `SessionType` if present

Do **not** require live iRacing for tests. One integration-style unit test: fake memory returns sample YAML → adapter `IsAvailable` true → first updates include track/car when YAML has them.

`WindowsIracingSharedMemory`: attempt open `Local\IRSDKMemMapFileName`; if missing return false (no throw). Include copyright notice in constants file:

```csharp
// Copyright (c) iRacing.com Motorsport Simulations, LLC.
// Constants derived from the official IRSDK headers (BSD-style notice).
// Redistribution retains this notice. No endorsement by iRacing.com.
```

Constants needed (minimum): map name, event name if used, header field offsets sufficient to locate session info string length/offset. Prefer reading session info via documented offsets; if full binary parsing is too large for one task, it is acceptable for `WindowsIracingSharedMemory.TryReadSnapshot` to expose session YAML by reading header SessionInfoOffset/Len when connected, and for the fake to supply YAML directly — still no GPL.

Wire `SessionLifecycleTracker` already in BridgeRuntime — adapter should emit RaceEvent SessionStart when connection established and SessionEnd when connection lost (if detectable); otherwise at least SessionSnapshot fields.

Update KI-002: live path exists but still requires iRacing running + memmap enabled; stub behavior when closed.

- [ ] **Step 1: Failing tests** for parser + fake-memory adapter

```csharp
[Fact]
public void Parses_track_car_and_session_type_from_yaml_subset()
{
    string yaml = File.ReadAllText(FixturePath("iracing", "session-info-sample.yaml"));
    IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml);
    Assert.True(info.TrackDisplayName.TryGet(out string? track));
    Assert.Equal("Okayama International Raceway", track);
    // car + session type assertions matching fixture
}

[Fact]
public async Task Adapter_available_when_fake_memory_open()
{
    FakeIracingSharedMemory memory = new(open: true, yaml: File.ReadAllText(...));
    IRacingAdapter adapter = new(memory, new SystemClock());
    Assert.True(await adapter.IsAvailableAsync(CancellationToken.None));
    List<NormalizedSimulatorUpdate> updates = [];
    await foreach (var u in adapter.SubscribeAsync(CancellationToken.None))
    {
        updates.Add(u);
        if (updates.Count > 3) break;
    }
    Assert.NotEmpty(updates);
    Assert.Equal(SimulatorIds.IRacing, updates[0].SimulatorId);
    Assert.True(updates[^1].SessionSnapshot!.Track.TryGet(out _));
}
```

Create `tests/fixtures/iracing/session-info-sample.yaml` as **synthetic** YAML shaped like IRSDK session info (original, not scraped from a copyrighted pack).

- [ ] **Step 2: RED**

- [ ] **Step 3: Implement ports, parser, fake, Windows opener, replace stub, update Program.cs DI**

- [ ] **Step 4: Full suite PASS without live iRacing**

- [ ] **Step 5: Mark BRIDGE-003 DONE if AC met (detect + YAML subset + no GPL + notice). Update KI-002. Commit:**

```text
feat(bridge): add first-party iRacing mmap session reader
```

Optional second commit if THIRD_PARTY-only: `docs: note vendored IRSDK constants`

---

## Out of scope

- Full 60 Hz telemetry variable table beyond what is needed for session metadata detection
- BRIDGE-007 tray
- Live-required CI jobs
- GPL wrappers

## Self-review checklist

1. INFRA-002, ANALYTICS-003, BRIDGE-003 each have a task.
2. Offset refuse-to-join covered by tests.
3. No live iRacing required for green CI.
