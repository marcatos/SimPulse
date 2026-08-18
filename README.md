# SimPulse

SimPulse is a biometric telemetry platform for sim racing. It correlates physiological data from wearables with simulator telemetry and race events.

First wearable stack: **Apple Watch**, **iPhone**, **HealthKit**.
First simulator: **iRacing on Windows**.

This is not a calorie counter. The long-term differentiator is synchronization of simulator telemetry and race events with physiological telemetry.

## Current status

Read these before changing anything:

1. [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) — truthful snapshot of what works
2. [`docs/AGENTS.md`](docs/AGENTS.md) — how agents must work in this repo
3. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
4. [`docs/BACKLOG.md`](docs/BACKLOG.md)

## Repository layout

```text
/
├── apps/
│   ├── ios/                 # Swift sources; Xcode project pending macOS
│   ├── watchos/             # Swift sources; Xcode project pending macOS
│   └── windows-bridge/      # .NET 8 Bridge host
├── packages/
│   ├── protocol/            # Versioned LAN protocol (C# + JSON Schema)
│   ├── domain-model/        # Simulator-independent domain
│   └── analytics/           # Pure, testable metrics
├── docs/
├── scripts/
├── tools/
├── tests/fixtures/
└── .github/workflows/
```

See [ADR 0001](docs/adr/0001-monorepo-structure.md) for why this layout exists, and [ADR 0007](docs/adr/0007-language-split.md) for the C# / Swift split.

## Development (Windows)

Requires: Git, .NET SDK 8.

```powershell
dotnet test SimPulse.sln
pwsh -File scripts/test-bridge.ps1
```

Apple builds require macOS and Xcode. They are **not executed** on this workstation. See [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).

## Principles

- Local-first. No cloud required for core features.
- Biometric data is sensitive. See [`docs/PRIVACY_ARCHITECTURE.md`](docs/PRIVACY_ARCHITECTURE.md).
- SimPulse is not a medical device. Analytics describe measurements, not diagnoses.
- Smallest real vertical slice over many partial features.

## License

Copyright (c) 2026 Simone Marcato. See [`LICENSE`](LICENSE).
