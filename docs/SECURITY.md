# Security

## Threat model (Phase 0, lightweight)

| Threat | Impact | Mitigations now | Later |
| --- | --- | --- | --- |
| Malicious LAN device talks to Bridge | Fake session metadata, nuisance | Bridge accepts sessions only after pairing; bind configurable | TLS with pinned cert |
| Unauthorized Bridge connection | Untrusted PC feeds iPhone | Explicit PIN pairing; persist trusted device IDs; revocation | Device attestation if needed |
| Replay of protocol messages | Duplicate events | Message IDs; idempotent merge | Sequence numbers + window |
| Spoofed simulator events | Distorted reports | Adapter is local process; paired Bridge is the trust boundary | Signed session summaries |
| Compromised pairing credentials | Attacker becomes trusted | PIN is not persisted; generated with `RandomNumberGenerator`; valid only during an explicit pairing window (5 minutes, successful pair, or 5 failed attempts) | Rotate pairing material |
| Local data exposure | Biometrics on disk | HealthKit for workouts; Bridge does not persist HR | iOS Data Protection review |
| Sensitive logs | HR in log files | Never log sample payloads; redact IDs if needed | Log scrub tests |

## Defaults

- Pairing is required before telemetry is sent.
- Do not listen on public WAN interfaces by product default. LAN only.
- No analytics SDK, no ads SDK.
- Do not invent cryptography. Use platform TLS and OS credential storage when those steps are implemented.

## Secrets

See `.env.example`. Never commit Apple certificates, App Store Connect keys, pairing stores, or tokens.

## What we are not doing in Phase 0

Mutual TLS, custom crypto protocols, and kernel-level hardening. Unsafe defaults (open unauthenticated WebSocket that streams forever) are also not acceptable.
