# ADR 0012 — XcodeGen generates the Apple project

- **Status:** ACCEPTED
- **Date:** 2026-08-18
- **Supersedes:** generation method only; [ADR 0009](0009-apple-project-generation.md) remains the record that Windows must not invent `project.pbxproj` by hand.

## Context

A Mac with Xcode 26.6 is available (partner MacBook, user `simonemarcato`). ADR 0009 deferred the `.xcodeproj` until a Mac chose Xcode GUI, XcodeGen, or Tuist. Watch/iOS backlog items are blocked on a real project.

## Decision

Use **XcodeGen**. The spec is `project.yml` at the repo root. `SimPulse.xcodeproj` is generated on macOS (`xcodegen generate`) and committed so Windows agents can see the project exists while still being unable to compile it.

Do not hand-edit `project.pbxproj` for routine target/source changes; edit `project.yml` and regenerate on a Mac.

## Alternatives considered

- **Xcode GUI only:** Fine for one-off, poor for agents and reviewable diffs.
- **Tuist:** Extra toolchain; XcodeGen is enough for two app targets.

## Consequences

- `scripts/build-ios.sh`, `test-ios.sh`, `build-watch.sh` run `xcodebuild` on Darwin; on Windows they still record NOT EXECUTED.
- GitHub Actions Apple job stays a placeholder until a macOS runner is added (hosted macOS Xcode may lag Xcode 26).
- HealthKit usage strings live in generated Info.plist keys; WATCH-001 still owns real workout lifecycle tests.

## Migration / reversal

Switching to Tuist requires a new ADR and deleting or ignoring the XcodeGen spec.
