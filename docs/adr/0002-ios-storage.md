# ADR 0002 — iOS local storage

- **Status:** PROPOSED
- **Date:** 2026-08-18

## Context

The iPhone must keep session history, correlation metadata, and derived reports. HealthKit already stores the authoritative workout samples. We need an app-owned store for SimPulse-specific data (simulator join, entitlements cache, pairing records).

## Decision (proposed)

Use **SwiftData** for SimPulse metadata on iPhone.

HealthKit remains the source of truth for workout samples. Do not duplicate every HR sample in SwiftData unless correlation performance requires a derived cache — if so, the cache is disposable.

This is PROPOSED until a Mac exists to validate the schema and migration story.

## Alternatives considered

- **Core Data manually:** More control, more boilerplate. SwiftData is the current Apple direction.
- **JSON files / SQLite via GRDB:** Extra dependency; not needed yet.
- **Store everything in HealthKit workouts metadata:** Metadata size and query limits; simulator telemetry does not belong in HealthKit.

## Consequences

- Schema changes need versioned migrations (required by project rules).
- Watch app should not own long-term history; iPhone does.

## Migration / reversal

If SwiftData proves inadequate, a GRDB or Core Data adapter can implement the same repository port. Domain types must not depend on SwiftData attributes.
