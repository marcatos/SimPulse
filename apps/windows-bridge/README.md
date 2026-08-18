# Windows Bridge

.NET 8 worker that will detect simulators, normalize telemetry, and talk to paired iPhones.

```powershell
$env:SIMPULSE_LOG_LEVEL = "Debug"
$env:SIMPULSE_FIXTURE_PATH = ".\tests\fixtures\telemetry\iracing-practice-short.json"
dotnet run --project apps/windows-bridge/SimPulse.Bridge
```

Without `SIMPULSE_FIXTURE_PATH`, the iRacing adapter reports unavailable (BRIDGE-003).
