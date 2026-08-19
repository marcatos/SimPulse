# ADR 0003 — Bridge protocol

- **Status:** ACCEPTED (wire format and pairing posture); discovery transport details may be refined
- **Date:** 2026-08-18

## Context

iPhone and Windows Bridge must talk on the LAN with no cloud. Need versioning, timestamps, session IDs, reconnect, forward-compatible messages, and explicit pairing.

## Decision

**Encoding:** JSON (`System.Text.Json` / Foundation `JSONEncoder`) for protocol v1.

**Transport:** WebSocket over TCP for the session after pairing. HTTP is not required for v1.

**Envelope:** every message has `protocolVersion`, `type`, `messageId`, `sentAtUtc`, and `payload`. Unknown JSON fields are ignored. Unknown `type` values are logged and ignored.

**Discovery (v1):** manual — Bridge displays IP, port, and a short pairing PIN (and later a QR code). mDNS/Bonjour is deferred (Windows Bonjour is an extra dependency and a common failure point).

**Pairing:** user starts pairing on both sides, PIN is compared, Bridge stores a device identifier. PIN is not stored. Trusted devices can be revoked. Unpaired clients do not receive telemetry.

**TLS:** the original Phase 0 skeleton used cleartext LAN transport. The 2026-08-19 follow-up in [ADR 0013](0013-bridge-tls-kestrel.md) implemented TLS by default with a Bridge-owned self-signed certificate and a SHA-256 fingerprint for client pinning. Explicit cleartext opt-out is restricted to loopback. Pairing semantics and the v1 envelope remain unchanged.

**Reconnect proof:** [ADR 0014](0014-reconnect-token.md) requires a per-device opaque token after PIN pairing; DeviceId alone no longer authorizes reconnect.

## Alternatives considered

- **Protobuf / MessagePack:** Smaller and stricter. Worse to inspect during early development; codegen across Swift and C# adds tool surface. Revisit if profiling shows JSON cost on dense telemetry.
- **HTTP + REST polling:** Poor for 60 Hz-class events; possible for pairing only.
- **mDNS first:** Better UX, more moving parts on Windows. Manual pairing is the reversible default.
- **IRSDKSharper or other GPL SDK on the wire:** Not applicable to the protocol; rejected at the adapter layer.

## Consequences

- Agents can implement iOS and Bridge against JSON fixtures independently.
- Dense telemetry may need batching or a binary encoding in a later protocol version (new ADR).

## Migration / reversal

`protocolVersion` is the compatibility switch. v2 can introduce MessagePack while v1 JSON remains for one release. Clients reject versions below `minimumCompatible`.
