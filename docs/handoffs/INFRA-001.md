# Handoff: INFRA-001

## Task

INFRA-001 — Repository bootstrap

## Goal

Create a production-quality Phase 0 foundation: docs, ADRs, domain/protocol/analytics contracts, Bridge skeleton, Apple source placeholders, CI, truthful CURRENT_STATE.

## Status

DONE

## Files changed

Entire repository (empty directory → Phase 0 tree). See git history.

## Decisions made

Recorded as ADRs 0001–0009. Notable:

- JSON + WebSocket + manual PIN pairing for protocol v1
- No IRSDKSharper (GPL-3.0)
- No fabricated Xcode project
- C# is the executable shared model until Swift compiles
- Heart-rate samples are not sent to the Windows Bridge

## Tests executed

- `dotnet test SimPulse.sln --configuration Release` on Windows 10.0.26200 / SDK 8.0.301
- `dotnet build apps/windows-bridge/SimPulse.Bridge --configuration Release`
- Apple scripts / xcodebuild: NOT EXECUTED

## Tests passing

20 / 20 .NET tests passed. Bridge host build: 0 warnings, 0 errors.

## Known failures

- Apple builds/tests NOT EXECUTED
- Live iRacing not implemented
- No LAN transport yet

## Remaining work

Phase 1+ per `docs/BACKLOG.md` and `docs/ROADMAP.md`.

## Risks

- Swift mirrors can drift from C# without a generator
- Manual pairing UX may delay first PC↔iPhone success
- First-party iRacing mmap reader is still unwritten

## Suggested next action

Windows agents: BRIDGE-005 and BRIDGE-003. Mac agent: generate Xcode project, then WATCH-001.
