# Watch UI screenshots

Captured 2026-08-18 from the **watchOS Simulator** (Apple Watch Series 11, 46mm) on simpulse-mac, Xcode 26.6. DEBUG launch arguments feed `MockWorkoutDataSource` so HealthKit permission is not required.

| File | Mode | Launch argument |
| --- | --- | --- |
| `watch-002-idle.png` | Idle, interactive | `--simpulse-preview-idle` |
| `watch-002-recording.png` | Recording, interactive | `--simpulse-preview-recording` |
| `watch-002-always-on.png` | Recording, Always On | `--simpulse-preview-always-on` |

Always On is simulated with `.environment(\.isLuminanceReduced, true)`. Physical Watch Always On was not captured.

```sh
xcrun simctl io <watch-udid> screenshot docs/screenshots/watchos/<name>.png
```
