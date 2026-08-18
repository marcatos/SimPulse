# ADR 0007 — Language split for shared contracts

- **Status:** ACCEPTED
- **Date:** 2026-08-18

## Context

Shared domain and protocol must be testable on Windows now, and used from Swift later. There is no Swift toolchain on the bootstrap machine.

## Decision

- **Canonical executable model (now):** C# in `packages/domain-model`, `packages/protocol`, `packages/analytics`.
- **Canonical wire contract:** JSON Schema in `packages/protocol/schemas`.
- **Swift:** type mirrors under `apps/ios` / `apps/watchos` matching names. Not compiled until Xcode exists.
- **Do not** generate Swift from C# in Phase 0 (no generator in-repo yet).

When both languages exist, protocol fixtures are the compatibility test. Domain behavior tests stay in C#; Swift repeats only what must run on-device.

## Alternatives considered

- **JSON Schema only, no code:** Too easy to drift; no tests.
- **Kotlin Multiplatform / protobuf codegen:** Extra platform, extra toolchain.
- **Swift-only packages:** Untestable here.

## Consequences

- Duplicate type names until a generator is justified.
- Changing a C# domain field that is serialized requires updating the schema and the Swift mirror in the same change set.

## Migration / reversal

A later ADR may introduce protobuf or a shared codegen step. Schema versioning remains.
