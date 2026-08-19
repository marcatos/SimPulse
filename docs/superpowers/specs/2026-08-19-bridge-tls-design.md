# KI-003 — Bridge transport TLS (pinned self-signed)

**Date:** 2026-08-19  
**Status:** Approved  
**Backlog / tracker:** KI-003 (KNOWN_ISSUES); related ADR 0003, SECURITY.md  
**Depends on:** BRIDGE-005/006 (DONE). Does **not** include KI-006 reconnect secrets or iOS pairing client (IOS-005).

## Goal

Encrypt Bridge ↔ client WebSocket traffic with **TLS** using a **self-signed certificate**, expose a stable **SHA-256 fingerprint** for pinning, and keep message-layer PIN pairing / DeviceId trust unchanged. Default is TLS on; cleartext `ws://` only as an explicit loopback opt-out for local tests.

## Non-goals

- KI-006 per-device reconnect secret / challenge
- iOS Bridge pairing UI and pin enforcement (IOS-005) — document fingerprint for later pin
- Mutual TLS (client certificates)
- Custom cryptography
- Committing private keys or PFX files to git
- Changing race-event payload contents or pairing PIN UX

## Context

- Transport today: `HttpListenerWebSocketTransport` → `http://127.0.0.1:8742/ws/` → clients use `ws://`.
- Trust today: PIN pairing then DeviceId-only reconnect over cleartext (KI-006).
- ADR 0003 / SECURITY.md: next step is **TLS with pinned self-signed cert**.
- User-approved option **A** and implementation approach **1** (cert file + pin + `wss://`; cleartext opt-in loopback only).

## Chosen decisions

| Topic | Choice |
| --- | --- |
| Cert | Self-signed, Bridge-owned; generate-once or load from path |
| Pin | SHA-256 of cert (or public key — pick **cert DER SHA-256**, document format) logged at INFO at listen start |
| Default | TLS **on** |
| Cleartext | Only if `SIMPULSE_BRIDGE_TLS=0` **and** bind host is loopback (`127.0.0.1` / `localhost`); refuse cleartext on `0.0.0.0` |
| Pairing | Unchanged (`PairingCoordinator`, trusted store) |
| KI-006 | Deferred |

## Architecture

```text
Program / DI
  → IBridgeCertificateSource (load or create)
  → IBridgeTransport
       TLS mode: secure WebSocket listener (wss / https prefix)
       Cleartext opt-out: existing HttpListener http://… (loopback only)
  → WebSocketMessagePump → PairingCoordinator (unchanged)
```

### Certificate source (port + adapter)

```csharp
public interface IBridgeCertificateSource
{
    /// <summary>Certificate used for TLS listen. Caller must not log private key material.</summary>
    X509Certificate2 GetOrCreate();
    string Sha256FingerprintHex { get; } // lowercase hex, no colons, of cert DER
}
```

Adapter behavior:

1. If `SIMPULSE_BRIDGE_CERT_PATH` is set → load PFX (password from `SIMPULSE_BRIDGE_CERT_PASSWORD` if needed; never log password).
2. Else → ensure a persisted self-signed cert under `%LOCALAPPDATA%\SimPulse\certs\bridge-dev.pfx` (or `SIMPULSE_BRIDGE_CERT_DIR`); create if missing (CN=`SimPulse Bridge`, validity ~825 days, RSA or ECDSA via platform APIs).
3. Expose fingerprint for logging and tests.

### Transport

- Extend or replace the listen path behind `IBridgeTransport` so callers (`Worker`, tests) do not care.
- **Practical note:** Windows `HttpListener` HTTPS usually needs an HTTP.sys SSL cert binding (often elevation). Prefer a **Kestrel-based** (or equivalent managed) HTTPS WebSocket host for the TLS path so loopback TLS works in tests without `netsh`. Keep **HttpListener** for the cleartext opt-out path **or** unify on one host that can do both — plan spike picks the minimal change that preserves existing cleartext tests.
- Default listen URL becomes `wss://127.0.0.1:8742/ws/` (document scheme change).
- On start, log: `TlsEnabled=true`, `TlsCertSha256=<hex>`, host, port — **never** private key / PFX bytes / password.

### Configuration (env)

| Variable | Default | Meaning |
| --- | --- | --- |
| `SIMPULSE_BRIDGE_TLS` | `1` | `0` = cleartext only if host is loopback |
| `SIMPULSE_BRIDGE_HOST` | `127.0.0.1` | unchanged |
| `SIMPULSE_BRIDGE_PORT` | `8742` | unchanged |
| `SIMPULSE_BRIDGE_CERT_PATH` | empty | Load existing PFX |
| `SIMPULSE_BRIDGE_CERT_PASSWORD` | empty | Optional PFX password |
| `SIMPULSE_BRIDGE_CERT_DIR` | `%LOCALAPPDATA%\SimPulse\certs` | Dev cert persistence |

Update `.env.example` and `docs/DEVELOPMENT.md`.

### Client pinning (Bridge-side contract for later iOS)

Document for IOS-005:

- Expect `wss://`
- Validate server cert SHA-256 equals the fingerprint shown by Bridge (tray/console/log)
- Reject mismatch

No iOS code in this slice.

## Testing (Windows / `dotnet test`)

- Generate/load cert in temp dir; fingerprint stable across reload.
- TLS transport accepts WebSocket; unknown message type ignored (parity with existing tests).
- Test client uses custom remote cert validation that **pins** the expected fingerprint (and fails on wrong pin).
- Cleartext opt-out works on loopback when `SIMPULSE_BRIDGE_TLS=0`.
- Cleartext refused (or fails fast) when TLS=0 and host is `0.0.0.0`.
- Existing pairing / hub tests remain green (message layer).
- Do not commit generated PFX from tests (temp dirs only).

## Docs / tracker

- Close or update **KI-003** when merged (TLS shipped; note IOS-005 still must pin).
- Update SECURITY.md threat table “Later” → current for TLS pin; keep KI-006 open.
- ADR 0003: append short “TLS follow-up implemented” note or superseding ADR if transport host changes materially (Kestrel vs HttpListener) — prefer short ADR amendment / new ADR **0003a** or **0013** if host changes.
- CURRENT_STATE / handoff `KI-003.md`.
- Plane: work item for KI-003 if present / create.

## Acceptance mapping

| Criterion | How met |
| --- | --- |
| TLS on wire by default | `wss` / HTTPS listen |
| Pinned self-signed | Fingerprint logged + test pin validation |
| Pairing unchanged | No `PairingCoordinator` trust model change |
| Cleartext constrained | Loopback + explicit env only |
| No secrets in git | `.gitignore` / localappdata certs; tests use temp |

## Implementation order (summary)

1. Claim Plane + branch `feat/bridge-tls`
2. Certificate source + unit tests
3. TLS transport path + cleartext guard + transport tests
4. Wire `Program.cs` / env; docs SECURITY / DEVELOPMENT / KI-003
5. `dotnet test` green; PR

## Follow-ups (explicitly later)

- KI-006 reconnect secret  
- IOS-005 pin fingerprint in pairing UI  
- KI-002 live iRacing mmap smoke (user has iRacing on this PC when ready)
