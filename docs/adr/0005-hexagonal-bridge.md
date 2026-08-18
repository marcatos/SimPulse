# ADR 0005 — Hexagonal Bridge and simulator adapters

- **Status:** ACCEPTED
- **Date:** 2026-08-18

## Context

iRacing is first; LMU, ACC/AC, AMS2, Rennsport will follow. iRacing structs must not leak into the domain or iOS.

## Decision

`ISimulatorAdapter` is the inbound port:

- Identify simulator
- Report availability
- Subscribe to normalized updates (session lifecycle, telemetry samples, laps, race events)

Implementations:

- `FixtureSimulatorAdapter` — required for tests and Windows development without a live sim
- `IRacingAdapter` — live mmap client (stub in Phase 0)
- Future: `LmuAdapter`, `AccAdapter`, `AcAdapter`, `Ams2Adapter`, `RennsportAdapter`

Normalized types live in `SimPulse.Domain`. Adapter projects may contain simulator-specific DTOs **internally**.

## Alternatives considered

- **One mega-parser with if (sim == iRacing):** Fast to start, impossible to test per sim.
- **Push iRacing YAML to iOS:** Couples clients to one vendor.

## Consequences

- New sims are new adapters plus fixtures, not domain changes.
- If a sim cannot emit an event, the domain already models `DataPresence`.

## Migration / reversal

Replacing an adapter does not require protocol changes if the normalized events stay stable.
