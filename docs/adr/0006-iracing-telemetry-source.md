# ADR 0006 — iRacing telemetry source

- **Status:** ACCEPTED
- **Date:** 2026-08-18

## Context

Need a legal, maintainable way to read iRacing live telemetry on Windows without copying proprietary third-party apps.

## Research summary

iRacing exposes live telemetry through a local Windows memory-mapped file `Local\IRSDKMemMapFileName` and an event `Local\IRSDKDataValidEvent`. Session info is a YAML string; variables are packed binary buffers typically at ~60 Hz, triple-buffered. `.ibt` logs use the same layout. Memory telemetry must be enabled (`irsdkEnableMem=1` in `app.ini`).

The official C headers carry an iRacing.com BSD-style copyright (retain notice; no endorsement).

Popular C# wrappers:

| Library | License | Verdict |
| --- | --- | --- |
| IRSDKSharper (NuGet) | **GPL-3.0** | **Rejected** — incompatible with a closed commercial product without GPL-ing SimPulse |
| irsdkSharp | MIT | Possible later; extra dependency not justified in Phase 0 |

## Decision

Write a **first-party** `IRacingAdapter` that reads the documented mmap layout. Vendor official header constants only when implementation starts, with the iRacing copyright notice.

Phase 0 ships a stub that reports unavailable, plus fixture replay that does not talk to iRacing.

Do not copy third-party proprietary source. Do not add IRSDKSharper.

## Alternatives considered

- **IRSDKSharper:** Fastest path, GPL risk, rejected.
- **irsdkSharp MIT:** Acceptable license, still a wrapper to learn and pin. Prefer a thin in-repo reader we fully own.
- **Parse .ibt files only:** Useful for tests; not sufficient for live Pro features.

## Consequences

- Live iRacing work is a dedicated task (BRIDGE-003).
- Fixture YAML/JSON must be original or recorded by us, not scraped from copyrighted third-party packs if that would violate terms. Synthetic fixtures are preferred in-repo.

## Migration / reversal

If a future MIT library clearly reduces risk, add it via a new ADR and `THIRD_PARTY.md`.
