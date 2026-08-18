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

The official iRacing telemetry headers (`irsdk_defines.h` and related) are copyright iRacing.com Motorsport Simulations, LLC, with a BSD-style redistribution notice. They are **not vendored in this repository yet**. When the mmap reader is implemented, include the required copyright notice in the adapter files.

## Apple frameworks (planned)

HealthKit, WatchConnectivity, SwiftUI, SwiftData — Apple system frameworks, not redistributed.
