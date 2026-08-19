# KI-003 Bridge TLS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Default Bridge WebSocket to TLS (`wss://`) with a self-signed cert and logged SHA-256 fingerprint for pinning; keep PIN pairing unchanged; allow cleartext only on loopback via explicit env opt-out.

**Architecture:** `IBridgeCertificateSource` loads or creates a PFX; TLS listen via **Kestrel** WebSocket host (avoids HttpListener HTTP.sys SSL binding/elevation); cleartext opt-out keeps existing `HttpListenerWebSocketTransport`. `Program` selects transport from env. Pairing/message pump unchanged.

**Tech Stack:** .NET 8, Kestrel (`Microsoft.AspNetCore.App`), `System.Security.Cryptography.X509Certificates`, existing `WebSocketMessagePump`, xUnit

## Global Constraints

- Branch/worktree: `feat/bridge-tls` from `main`
- Plane: create/claim KI-003 work item In Progress
- Never log private keys, PFX bytes, or cert passwords
- Never commit `*.pfx` (already gitignored)
- Fingerprint = SHA-256 of **raw cert DER**, lowercase hex, no colons
- Default `SIMPULSE_BRIDGE_TLS=1`; cleartext only if TLS=0 **and** host is `127.0.0.1` or `localhost`
- Spec: `docs/superpowers/specs/2026-08-19-bridge-tls-design.md`
- Verify with `dotnet test SimPulse.sln --configuration Release` on Windows
- Conventional commits; executing this plan authorizes per-task commits
- No Apple UI → screenshots N/A

## File map

| Path | Role |
| --- | --- |
| `.../Ports/BridgeCertificatePorts.cs` | `IBridgeCertificateSource` |
| `.../Adapters/FileBridgeCertificateSource.cs` | Load/create PFX + fingerprint |
| `.../Adapters/KestrelWebSocketTransport.cs` | TLS `wss` listener |
| `.../Adapters/HttpListenerWebSocketTransport.cs` | Cleartext; add loopback guard helper if needed |
| `.../Adapters/BridgeTransportFactory.cs` (or Program) | Choose TLS vs cleartext |
| `SimPulse.Bridge.Core.csproj` | `FrameworkReference` AspNetCore.App |
| `SimPulse.Bridge/Program.cs` | Wire cert + transport |
| `...Tests/BridgeCertificateSourceTests.cs` | Cert + fingerprint |
| `...Tests/TlsWebSocketTransportTests.cs` | Pin accept / wrong pin fail / cleartext guards |
| `docs/SECURITY.md`, `DEVELOPMENT.md`, `KNOWN_ISSUES.md`, ADR note, `.env.example` | Docs |

---

### Task 1: Claim + worktree + design commit

- [ ] Plane KI-003 In Progress (create if missing)
- [ ] Worktree `feat/bridge-tls` from `origin/main`
- [ ] Commit design spec: `docs(bridge): design KI-003 pinned TLS transport`
- [ ] Handoff stub `docs/handoffs/KI-003.md`; CURRENT_STATE active work note optional until Task 5

---

### Task 2: Certificate source (TDD)

**Interfaces:**

```csharp
public interface IBridgeCertificateSource
{
    X509Certificate2 GetOrCreate();
    string Sha256FingerprintHex { get; }
}

public sealed class FileBridgeCertificateSource : IBridgeCertificateSource
{
    public FileBridgeCertificateSource(
        string? certPath,
        string? certPassword,
        string certDirectory,
        ILogger logger);
    // GetOrCreate: load PFX or create self-signed RSA 2048, CN=SimPulse Bridge, write PFX to certDirectory/bridge-dev.pfx
    // Sha256FingerprintHex from certificate.RawData
}
```

- [ ] **Tests** (temp directory):
  - Creates PFX when missing; second call reloads same fingerprint
  - Loads from explicit path
  - Fingerprint is 64 hex chars
- [ ] Implement with `CertificateRequest` / `X509Certificate2` (.NET 8)
- [ ] Commit `feat(bridge): add self-signed Bridge TLS certificate source`
- [ ] `dotnet test` filter `BridgeCertificate` green

---

### Task 3: Kestrel TLS transport + cleartext policy (TDD)

**csproj:**

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

**Interfaces:**

```csharp
public sealed class KestrelWebSocketTransport : IBridgeTransport
{
    public KestrelWebSocketTransport(
        string host,
        int port,
        X509Certificate2 certificate,
        string fingerprintHex,
        IClientSessionHub hub,
        IClock clock,
        ILogger logger,
        Func<...> onMessage,
        Action<...> onDisconnected);
    // Listen HTTPS, Map /ws WebSocket, AcceptWebSocketAsync → existing pump + hub register
}

public static class BridgeTlsPolicy
{
    public static bool IsLoopbackHost(string host);
    public static void EnsureCleartextAllowed(string host, bool tlsEnabled);
    // throws InvalidOperationException if !tls && !loopback
}
```

- [ ] **Tests:**
  - `Tls_accepts_websocket_with_pinned_fingerprint` — `ClientWebSocket` + `RemoteCertificateValidationCallback` comparing SHA-256
  - `Tls_rejects_client_when_pin_mismatches`
  - `Cleartext_refused_when_tls_disabled_and_host_is_all_interfaces`
  - Keep existing HttpListener tests; they still use cleartext transport explicitly
- [ ] Implement Kestrel host: bind to host/port with `Listen` + `UseHttps(certificate)`; path `/ws` or `/ws/`
- [ ] Log at start: `TlsEnabled=true TlsCertSha256={fingerprint} Host= Port=`
- [ ] Commit `feat(bridge): listen for WebSocket clients over TLS`
- [ ] Full `dotnet test SimPulse.sln --configuration Release`

**Reuse:** After accept, wrap `System.Net.WebSockets.WebSocket` the same way HttpListener path does (shared connection adapter if one exists — follow existing code).

---

### Task 4: Program wiring + env + docs

- [ ] `Program.cs`: register `IBridgeCertificateSource`; if TLS enabled → `KestrelWebSocketTransport`, else → `HttpListenerWebSocketTransport` after `BridgeTlsPolicy.EnsureCleartextAllowed`
- [ ] Read env: `SIMPULSE_BRIDGE_TLS`, cert path/password/dir (document defaults)
- [ ] `.env.example` new vars
- [ ] `docs/DEVELOPMENT.md` — how to find fingerprint, `wss://` URL, cleartext opt-out
- [ ] `docs/SECURITY.md` — TLS now; pin for clients; KI-006 still open
- [ ] `docs/KNOWN_ISSUES.md` — KI-003 closed or “mitigated: TLS shipped; IOS-005 pin pending”
- [ ] ADR: short `docs/adr/0013-bridge-tls-kestrel.md` ACCEPTED (Kestrel for TLS; HttpListener cleartext opt-out) + note on ADR 0003
- [ ] Handoff `docs/handoffs/KI-003.md` complete; BACKLOG if there is a Bridge TLS item; CURRENT_STATE
- [ ] Commit `docs(bridge): record KI-003 TLS and close cleartext default`
- [ ] Commit `feat(bridge): enable TLS by default for Bridge transport` if Program change not in Task 3

---

### Task 5: Final verify + Plane

- [ ] `dotnet test SimPulse.sln --configuration Release` — all green (expect ≥129)
- [ ] Plane comment + Done when user merges
- [ ] PR title when asked: `feat(bridge): TLS with pinned self-signed certificate`

---

## Spec coverage

| Spec item | Task |
| --- | --- |
| Cert load/create + fingerprint | 2 |
| TLS default / cleartext loopback-only | 3–4 |
| Kestrel (or managed) TLS path | 3 |
| Pairing unchanged | 3–4 (no PairingCoordinator edits) |
| Docs SECURITY / KI-003 / env | 4 |
| Pin test client | 3 |

## Self-review notes

- Locked transport host: **Kestrel for TLS**, **HttpListener for cleartext opt-out** (ADR 0013).
- Existing `WebSocketTransportTests` remain valid against HttpListener; new TLS tests use Kestrel + pin callback.
- Tray may later show fingerprint (optional, out of scope unless trivial log already suffices).
