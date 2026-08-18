# Test fixtures

Deterministic inputs. Ordinary tests must not require a live iRacing session or a physical Watch.

| Path | Use |
| --- | --- |
| `telemetry/iracing-practice-short.json` | FixtureSimulatorAdapter |
| `biometric/watch-practice-short.json` | Analytics / future Watch mocks |

Pipeline: fixture → adapter → normalized events → analytics → expected results.
