# watchOS app (SimPulse Watch)

Generated with the iOS app via `project.yml` ([ADR 0012](../../docs/adr/0012-xcodegen-apple-project.md)). The Watch records a Sim Racing workout without a permanent iPhone connection (`WKRunsIndependentlyOfCompanionApp`). HealthKit capture uses `HKWorkoutActivityType.other` plus Sim Racing metadata.

```sh
./scripts/build-watch.sh
./scripts/test-ios.sh
```
