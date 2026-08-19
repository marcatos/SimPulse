# Task 2: Protocol DTOs — Report

**Task:** KI-006 Task 2 — Protocol DTOs (TDD)  
**Branch:** `feat/reconnect-token`  
**Base:** `8a6fe733749abe4db046a0bcf409f6e8ea09114b`  
**Status:** DONE

## Summary

Extended `HelloMessage` and `PairingAcceptMessage` with optional/required `ReconnectToken` wire fields. TDD: failing tests added first (compile fail confirmed), then DTO updates. Bridge not touched; full solution not built (expected compile break until Task 3).

## Changes

| File | Change |
| --- | --- |
| `packages/protocol/SimPulse.Protocol/Messages.cs` | `HelloMessage.ReconnectToken` optional default `null`; `PairingAcceptMessage.ReconnectToken` required |
| `packages/protocol/SimPulse.Protocol.Tests/EnvelopeCodecTests.cs` | +3 tests: hello round-trip, hello without token → null, pairing.accept round-trip |

## TDD steps

1. Added three failing tests → compile errors (missing ctor/property) — confirmed.
2. Updated `Messages.cs` per plan.
3. Protocol tests green.

## Tests

```powershell
dotnet test packages/protocol/SimPulse.Protocol.Tests/SimPulse.Protocol.Tests.csproj --filter FullyQualifiedName~EnvelopeCodecTests --configuration Release
```

**Result:** 9 passed, 0 failed (includes 3 new + 6 existing EnvelopeCodec tests).

## Commit

- `feat(protocol): add reconnectToken to hello and pairing.accept`

## Notes

- JSON uses camelCase `reconnectToken`; null hello token omitted via existing `DefaultIgnoreCondition.WhenWritingNull`.
- 3-arg `HelloMessage(...)` call sites remain valid via default parameter.
- `PairingAcceptMessage` arity change will break Bridge until Task 3 — intentional.

## Next

Task 3: `ReconnectToken` helper, store `AuthorizeReconnectAsync`, coordinator issue/authorize.
