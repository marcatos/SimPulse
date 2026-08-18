# ADR 0001 — Monorepo structure

- **Status:** ACCEPTED
- **Date:** 2026-08-18

## Context

SimPulse has three runtime apps (iOS, watchOS, Windows Bridge) plus shared domain, protocol, and analytics. Multiple agents will work in parallel. The product must stay a single vertical slice, not three disconnected repos.

## Decision

Use a single Git monorepo:

```text
apps/ios, apps/watchos, apps/windows-bridge
packages/protocol, packages/domain-model, packages/analytics
docs, scripts, tools, tests, .github
```

.NET executable contracts live under `packages/*` as C# projects. Swift sources live under `apps/ios` and `apps/watchos` without an Xcode project until macOS is available.

Tests for .NET projects sit next to the project (`*.Tests`) because that is the standard `dotnet test` layout. Shared fixtures live in `tests/fixtures/` so adapters and analytics reuse the same files. This is a deliberate deviation from a single top-level `tests/` tree for all languages.

## Alternatives considered

- **Polyrepo per app:** Strong isolation, weak protocol discipline, painful agent handoffs.
- **Swift Package as the only shared model:** Cannot compile or test on the current Windows workstation.
- **One giant .NET solution including fake iOS:** Would invent Apple project files.

## Consequences

- Agents can own directories with low merge conflict.
- Protocol changes are visible in one PR and must update schema + C# (+ later Swift).
- Apple CI is a second pipeline.

## Migration / reversal

Splitting a mature app into its own repo is possible later by git subtree or filter. Do not split before the vertical slice works.
