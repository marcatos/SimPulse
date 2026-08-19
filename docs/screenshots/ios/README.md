# iOS UI screenshots

Simulator captures for the iPhone app. Update this folder whenever user-visible iOS UI changes (see `.cursor/rules/apple-screenshots.mdc`).

| File | Screen | Notes |
| --- | --- | --- |
| *(none yet)* | — | Add shots when shipping visible iOS UI (session list, detail/charts, permissions empty state, etc.) |

Capture on **simpulse-mac** with Xcode simulators. Prefer DEBUG launch arguments / mocks so HealthKit prompts are not required (e.g. `--simpulse-preview-sessions`).

```sh
xcrun simctl io <iphone-udid> screenshot docs/screenshots/ios/<name>.png
```

Record date, Xcode version, and device model when adding files.
