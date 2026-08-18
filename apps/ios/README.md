# iOS app (SimPulse)

Apple project generation is done on macOS with [XcodeGen](https://github.com/yonaskolb/XcodeGen) (`project.yml`, [ADR 0012](../../docs/adr/0012-xcodegen-apple-project.md)). Do not hand-edit `SimPulse.xcodeproj` for routine source changes.

```sh
brew install xcodegen
xcodegen generate
./scripts/build-ios.sh
./scripts/test-ios.sh
```

On Windows those scripts record **NOT EXECUTED**.
