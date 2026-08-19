# Testing

## Layers

| Layer | What | Where | Hardware needed |
| --- | --- | --- | --- |
| Unit | Domain and analytics | `packages/*/*.Tests` | No |
| Protocol | Serialize, unknown fields, version | `packages/protocol/*.Tests` | No |
| Adapter | Fixture replay through `ISimulatorAdapter` | `apps/windows-bridge/*.Tests` | No |
| Integration | Bridge host + protocol fixtures | later | No live sim |
| Apple | Domain/use-case tests with mocked HealthKit | `apps/ios`, `apps/watchos` | No Watch for unit tests |
| Regression | Permanent test for each important fixed bug | next to the failing layer | No |

Ordinary CI must never require an active iRacing session or a physical Watch.

Optional live mmap smoke (Windows, sim or replay running): `pwsh -File scripts/smoke-iracing-mmap.ps1`. Not part of CI.

## Fixtures

Canonical telemetry fixtures live in `tests/fixtures/`.

Pipeline to preserve:

```text
telemetry fixture → adapter → normalized events → analytics → expected results
```

Synthetic biometric fixtures live in `tests/fixtures/biometric/` so analytics can be developed on Windows before the Watch app exists.

## Apple

Abstract HealthKit behind `WorkoutDataSource`. Test the use case with a recording fake (`apps/ios/SimPulseTests`). Do not wrap every Apple API — only the boundaries needed for meaningful tests.

On a Mac: `./scripts/test-ios.sh` runs `xcodebuild test` for scheme `SimPulse` (iPhone 17 simulator). Windows still records **NOT EXECUTED**.

## Running tests

Windows / Linux:

```powershell
dotnet test SimPulse.sln
```

Apple (macOS later):

```sh
./scripts/test-ios.sh
```

## Definition of a passing change

- New behavior has tests
- Existing tests still pass on the platforms that can run
- Unavailable platforms are recorded, not faked
