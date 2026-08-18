# ADR 0009 — Apple project generation deferred

- **Status:** ACCEPTED
- **Date:** 2026-08-18

## Context

Phase 0 runs on Windows without Swift or Xcode. Hand-writing `project.pbxproj` is fragile and often wrong.

## Decision

Do **not** fabricate an `.xcodeproj`. Commit Swift source scaffolding and scripts that fail with a clear message until a macOS/Xcode environment generates the project (Xcode GUI, XcodeGen, or Tuist — chosen on the Mac).

Record all Apple builds/tests as **NOT EXECUTED**.

## Alternatives considered

- **Hand-authored pbxproj:** High chance of a repo that looks complete and does not open.
- **Swift Package only:** Possible later for shared logic; still cannot run Watch HealthKit without an app target.

## Consequences

- Phase 1 is blocked on a Mac.
- Windows agents can still finish analytics, protocol, and Bridge.

## Migration / reversal

When the Xcode project exists, this ADR remains historical. A new ADR records XcodeGen vs native project if we adopt a generator.
