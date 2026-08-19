# ADR 0013 — Bridge TLS via Kestrel

- **Status:** ACCEPTED
- **Date:** 2026-08-19

## Context

ADR 0003 selected WebSocket transport and identified pinned TLS as the next security step. The original Bridge listener used `HttpListener` cleartext. On Windows, `HttpListener` HTTPS commonly requires an HTTP.sys certificate binding and elevation, which conflicts with a user-level Bridge and automated loopback tests.

The Bridge needs encrypted WebSocket traffic by default, a stable certificate identity that future clients can pin, and an explicit local-only escape hatch for diagnostics. PIN pairing and trusted-device behavior must not change.

## Decision

- Use Kestrel for the default TLS WebSocket listener at `wss://<host>:<port>/ws/`.
- Load or generate a Bridge-owned self-signed PFX through `IBridgeCertificateSource`.
- Publish the lowercase, no-colon SHA-256 fingerprint of the certificate DER in Information logs as `TlsCertSha256`. Clients must pin this value and reject mismatches.
- Enable TLS when `SIMPULSE_BRIDGE_TLS` is unset, empty, or any value other than `0`, `false`, or `off`.
- Retain `HttpListenerWebSocketTransport` only for explicit cleartext opt-out on `127.0.0.1` or `localhost`; refuse non-loopback cleartext before listening.
- Keep `PairingCoordinator`, the protocol envelope, and trusted-device reconnect behavior unchanged.

## Consequences

- Normal Bridge traffic is encrypted without HTTP.sys setup or administrator rights.
- The generated private key persists locally under `%LOCALAPPDATA%\SimPulse\certs` by default and must remain outside source control.
- The Bridge logs a pinning contract, but IOS-005 still has to enforce that pin in the iPhone client.
- TLS does not provide per-device reconnect proof; KI-006 remains open.
- `SimPulse.Bridge.Core` references `Microsoft.AspNetCore.App` for the Kestrel adapter.

## Alternatives considered

- **HttpListener HTTPS:** rejected because HTTP.sys certificate binding adds elevation and machine configuration.
- **Cleartext plus pairing only:** rejected as the default because PIN pairing does not encrypt LAN traffic.
- **Custom transport encryption:** rejected; platform TLS is the correct security boundary.
- **Mutual TLS:** deferred because it adds client certificate lifecycle complexity beyond Phase 0.

## Verification

Certificate tests cover create/load stability and fingerprint format. Transport tests connect with the expected pin, reject a mismatched pin, and enforce the loopback-only cleartext policy.
