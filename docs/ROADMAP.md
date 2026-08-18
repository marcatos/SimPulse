# Roadmap

Dates are not committed. Phases are sequential for the vertical slice; work inside a phase may be parallelized by area.

## Phase 0 — Foundation (current)

Repository, docs, ADRs, CI skeleton, domain/protocol/analytics contracts, Bridge skeleton, Apple source placeholders.

## Phase 1 — Watch workout

Functional HealthKit Sim Racing workout: elapsed time, current/avg/max HR, active calories, persist workout.

## Phase 2 — iPhone companion

WatchConnectivity sync, local history, session details, charts from workout data.

## Phase 3 — Windows Bridge

iRacing detection, normalized session metadata, fixture replay, session/lap lifecycle.

## Phase 4 — Pairing and protocol

Discovery, pairing, trusted devices, WebSocket session, reconnect, protocol compatibility tests.

## Phase 5 — Correlation

Time sync, join workout timeline to simulator timeline, peak-HR ↔ race event.

## Phase 6 — Analytics

HR/energy summaries, HR by lap, session comparison, RaceReport model (no medical labels).

## Phase 7 — Productization

Onboarding, privacy copy, App Store artifacts, StoreKit entitlements, accessibility, localization.

## Phase 8 — Additional simulators and wearables

LMU, ACC/AC/ACEVO, AMS2, Rennsport, Garmin, Polar, WHOOP, BLE HR — only after iRacing + Apple Watch abstractions are proven.

## Commercial requirements tracked, not faked

- App Store HealthKit review questionnaire
- Privacy nutrition labels
- Public privacy policy URL
- Data deletion UX
- Localization and accessibility pass
