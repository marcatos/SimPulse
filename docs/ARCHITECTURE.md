# Architecture

## Principle

Build the smallest real Watch → iPhone → Bridge → report slice while keeping the boundaries needed for biometric + simulator correlation.

```text
Apple Watch                 iPhone                      Windows PC
┌─────────────┐            ┌──────────────┐            ┌──────────────────┐
│ HKWorkout   │ WatchConn. │ Session store│   LAN      │ SimPulse Bridge  │
│ HealthKit   │───────────►│ Analytics    │◄──────────►│ Simulator adapters│
│ Glance UI   │            │ Race reports │  Protocol  │ iRacing (first)  │
└─────────────┘            └──────────────┘     v1     └──────────────────┘
```

No cloud is required for core features.

## Layers

Hexagonal (ports and adapters):

| Layer | Location | Allowed to know |
| --- | --- | --- |
| Domain | `packages/domain-model` | Nothing outside itself |
| Analytics | `packages/analytics` | Domain only |
| Protocol | `packages/protocol` | Domain identifiers + wire types |
| Application | `apps/*/…` use cases | Domain, protocol, ports |
| Adapters | HealthKit, IRSDK, WebSocket, SwiftData, files | Ports they implement |

Dependencies point inward only.

## Apps

### watchOS

Records a Sim Racing workout with `HKWorkoutSession` / `HKLiveWorkoutBuilder`. Must keep recording if the iPhone is unreachable. Syncs a session summary when connectivity returns.

### iOS

Session history, details, charts, HealthKit, WatchConnectivity, Bridge pairing/sync, future StoreKit gates, export/share.

### Windows Bridge

Detects simulators, normalizes telemetry, detects session/lap lifecycle, exposes events to paired iPhones. First adapter: iRacing via the official local memory-mapped telemetry interface. Simulator-specific types stay inside adapters.

## Shared packages

C# is the executable contract language on this Windows workstation. Swift mirrors will be added when Xcode exists. JSON Schema in `packages/protocol/schemas` is the language-neutral wire contract.

See [ADR 0007](adr/0007-language-split.md).

## Time

Never assume device clocks match. Every sample carries a timestamp **and a clock source**. Correlation uses an explicit timeline offset, not wall-clock equality.

See [ADR 0004](adr/0004-time-synchronization.md).

## Communication

LAN only for MVP. Versioned JSON messages over WebSocket after explicit pairing. Unknown fields and unknown message types are ignored.

See [ADR 0003](adr/0003-bridge-protocol.md).

## Persistence

| Store | Data | Location |
| --- | --- | --- |
| HealthKit | Authoritative workout samples | On-device, Apple-managed |
| SwiftData (planned) | SimPulse session metadata, correlation, reports | iPhone app container |
| Bridge trusted-device file | Paired device IDs plus SHA-256 of the per-device reconnect token; no biometrics, no plaintext token | User-selected local path |
| Bridge does not store HR samples | — | — |

See [ADR 0002](adr/0002-ios-storage.md).

## Entitlements

`SimPulse.Domain.Entitlements` describes Free / Premium / Pro capabilities. StoreKit is not wired. UI and Bridge sync must check these functions rather than scattering boolean flags.

## Failure assumptions

Watch ↔ iPhone loss, Wi-Fi loss, Bridge restart, sim crash, clock drift, duplicate/late messages, app suspend, unexpected session end. Operations that merge session data must be idempotent (message IDs, session IDs).

## Future cloud

Optional later. Do not encode local IDs in a way that cannot map to a future account. Do not add a backend in Phase 0–6.
