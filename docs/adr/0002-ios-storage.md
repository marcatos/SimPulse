# ADR 0002 — iOS local storage

- **Status:** Accepted
- **Date:** 2026-08-18
- **Accepted:** 2026-08-19 (IOS-001)

## Context

The iPhone must keep session history, correlation metadata, and derived reports. HealthKit already stores the authoritative workout samples. We need an app-owned store for SimPulse-specific data (simulator join, entitlements cache, pairing records).

## Decision

Use **SwiftData** for SimPulse-owned metadata on iPhone (pairing, correlation, report cache).

HealthKit remains the source of truth for workout samples. Do not duplicate every HR sample in SwiftData unless correlation performance requires a derived cache — if so, the cache is disposable.

**IOS-001 note:** The first history list reads Sim Racing workouts through a `SessionRepository` port (`MockSessionRepository` + `HealthKitSessionRepository`). The SwiftData schema lands when WatchConnectivity / Bridge ingest produces app-owned metadata (WATCH-003 / IOS-004+), not in the listing slice.

## Alternatives considered

- **Core Data manually:** More control, more boilerplate. SwiftData is the current Apple direction.
- **JSON files / SQLite via GRDB:** Extra dependency; not needed yet.
- **Store everything in HealthKit workouts metadata:** Metadata size and query limits; simulator telemetry does not belong in HealthKit.

## Consequences

- Schema changes need versioned migrations (required by project rules).
- Watch app should not own long-term history; iPhone does.
- Session list UI depends on the repository port, not on SwiftData types.

## Migration / reversal

If SwiftData proves inadequate, a GRDB or Core Data adapter can implement the same repository port. Domain types must not depend on SwiftData attributes.
