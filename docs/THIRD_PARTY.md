# Third-party dependencies

Before adding a dependency:

1. Can the standard library or platform do this adequately?
2. Is the dependency maintained?
3. Is its license compatible with a future commercial App Store product?
4. Does it materially reduce complexity?
5. What happens if it is abandoned?

**GPL / LGPL dependencies are not acceptable** for libraries linked into the iOS app or the redistributed Bridge without a dedicated legal decision. IRSDKSharper is GPL-3.0 and is **rejected** for that reason ([ADR 0006](adr/0006-iracing-telemetry-source.md)).

## Current runtime dependencies

| Component | Package | License | Why |
| --- | --- | --- | --- |
| All .NET projects | .NET 8 BCL | MIT | Platform |
| Bridge host | `Microsoft.Extensions.Hosting` | MIT | Worker lifetime, DI, logging |
| Bridge host | `Microsoft.Extensions.Logging.Console` | MIT | Structured console logs |
| Tests | `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | Apache-2.0 / MIT | Unit tests |

No NuGet packages for iRacing, JSON extras, or mDNS are included in Phase 0.

## iRacing SDK

The official iRacing telemetry headers (`irsdk_defines.h` and related) are copyright iRacing.com Motorsport Simulations, LLC, with a BSD-style redistribution notice. A **minimal constant subset** (map name, `IRSDKDataValidEvent`, connected status bit, session-info header offsets, `varBuf` / `varHeader` sizes and offsets, `irsdk_int` / `irsdk_double`) is vendored in `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/Iracing/IracingSdkConstants.cs` with the required copyright notice. No IRSDKSharper (GPL) or other iRacing wrapper packages.

## Apple frameworks (planned)

HealthKit, WatchConnectivity, SwiftUI, SwiftData — Apple system frameworks, not redistributed.
