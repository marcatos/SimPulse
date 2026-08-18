# Windows Bridge

.NET 8 worker that will detect simulators, normalize telemetry, and talk to paired iPhones.

```powershell
$env:SIMPULSE_LOG_LEVEL = "Debug"
$env:SIMPULSE_FIXTURE_PATH = ".\tests\fixtures\telemetry\iracing-practice-short.json"
dotnet run --project apps/windows-bridge/SimPulse.Bridge --property:OutputType=Exe
```

On Windows the host is `WinExe` (no console; pairing PIN in the tray balloon). **Show current PIN** redisplays the last PIN without rotating it while the window is open. File logs go to `%LOCALAPPDATA%\SimPulse\logs` and include the coordinator `Pin=` line. Pass `--property:OutputType=Exe` to keep a console for logs. `SIMPULSE_BRIDGE_TRAY=0` uses console pairing UX instead of the tray. See [`docs/DEVELOPMENT.md`](../../docs/DEVELOPMENT.md).

Without `SIMPULSE_FIXTURE_PATH`, the iRacing adapter reports unavailable (BRIDGE-003).
