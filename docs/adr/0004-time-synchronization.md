# ADR 0004 — Time synchronization

- **Status:** ACCEPTED (strategy); implementation is Phase 5
- **Date:** 2026-08-18

## Context

Heart-rate samples, Watch workout clocks, iPhone clocks, Windows clocks, and iRacing session time are different clocks. Network delay, reconnects, drift, and different sample rates are expected. Assuming they match produces fake precision.

## Decision

1. **Every timestamp is a value plus a clock source** (`Utc`, `DeviceLocal`, `SimulatorSession`, `WorkoutSession`, `EstimatedUtc`) and an optional estimated error.
2. **Do not compare raw wall clocks across devices** to join events.
3. **Workouts** use HealthKit sample times (Apple's clock domain).
4. **Simulator events** use (a) adapter-read UTC when the Bridge captured the packet, and (b) simulator session time when the sim provides it (iRacing session time / lap time).
5. **Join key** for a DriverSession is an explicit `TimelineOffset` (and later a piecewise offset if drift is measured) between workout timeline and simulator timeline.
6. **LAN time sync** uses an NTP-style four-timestamp exchange (`TimeSyncRequest` / `TimeSyncResponse`) to estimate Bridge↔iPhone offset and round-trip. Use the offset only when RTT is below a threshold; otherwise treat offset as unknown.
7. **After reconnect**, run time sync again. Do not reuse a stale offset indefinitely.
8. **Correlation quality is explicit:** reports must say when peak-HR cannot be honestly bound to a race event.

## Alternatives considered

- **Trust NTP on all devices:** Better than nothing, still drifts, and Watch/iPhone may already be close; still insufficient for sub-second racing events without measuring RTT.
- **Use only simulator session time for everything:** Watch cannot see it while disconnected.
- **Central cloud timestamping:** Violates local-first and adds latency.

## Consequences

- Analytics APIs take a correlated timeline or refuse to join.
- Tests must include clock-skewed fixtures.

## Migration / reversal

A later ADR may add clock-drift models. The `ClockSource` field remains.
