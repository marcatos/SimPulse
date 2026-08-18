# Development

## Bootstrapping workstation (2026-08-18)

Inspected on the Phase 0 bootstrap machine:

| Tool | Status |
| --- | --- |
| OS | Windows 10.0.26200 (win-x64) |
| Git | 2.52.0.windows.1 |
| GitHub CLI | 2.87.3, authenticated as `marcatos` |
| .NET SDK | 8.0.301 |
| PowerShell | 7.6.3 |
| Node.js | 22.22.0 (not required) |
| Python | 3.14.2 (not required) |
| Swift | **not available** |
| Xcode / xcodebuild | **not available** |

Do not claim iOS/watchOS builds succeeded on this machine.

## Getting started

```powershell
cd C:\Users\simot\Documents\Projects\SimPulse
dotnet test SimPulse.sln
pwsh -File scripts/test-bridge.ps1
```

Copy `.env.example` to `.env` only if you need local overrides. Never commit `.env`.

## Apple development

Apple project generation is pending a macOS/Xcode environment. Swift sources under `apps/ios` and `apps/watchos` are specifications, not a buildable Xcode project.

When a Mac is available:

```sh
./scripts/build-ios.sh
./scripts/test-ios.sh
./scripts/build-watch.sh
./scripts/archive-ios.sh
```

Those scripts must fail clearly until an `.xcodeproj` / `.xcworkspace` exists. Do not hand-author project.pbxproj unless a later ADR supersedes [ADR 0009](adr/0009-apple-project-generation.md).

Expected future path: Windows workstation → git → CI → self-hosted Mac mini → TestFlight.

## Line endings

`.gitattributes` forces LF except for `.ps1`, `.sln`, `.bat`, `.cmd`.

## Windows Bridge tray vs console

On Windows the Bridge host builds as `WinExe` (no console window). A notification-area icon shows the pairing PIN in a balloon. **Show current PIN** redisplays the last PIN (tooltip/balloon) without opening a new pairing window. **Pair new device** calls `BeginPairingWindow()` so a new PIN opens without restarting the process. The PIN stays in the tray tooltip until a later PIN or process exit.

If the tray STA/`NotifyIcon` fails or does not become ready within 5 seconds, Bridge logs ERROR and continues with `ConsolePairingUx` instead of exiting.

| Goal | How |
| --- | --- |
| Normal run (no console, PIN in tray) | Double-click the Windows build, or `dotnet run --project apps/windows-bridge/SimPulse.Bridge` |
| Debug with a console (logs visible; tray still used when interactive) | `dotnet run --project apps/windows-bridge/SimPulse.Bridge --property:OutputType=Exe` |
| Console pairing UX instead of tray | `SIMPULSE_BRIDGE_TRAY=0` (PIN logged at Information). Combine with `OutputType=Exe` so the console exists |
| Linux / Ubuntu CI | Stays `Exe` + `net8.0`; pairing UX is console |

`SIMPULSE_BRIDGE_CONSOLE=1` documents intent to debug with a console. It does **not** change `OutputType` (MSBuild cannot read that env at build time). Pass `--property:OutputType=Exe` (or set `OutputType` to `Exe` in the project while debugging).

With `WinExe`, the console is hidden. File logs are written under `%LOCALAPPDATA%\SimPulse\logs` (or the user-profile equivalent). Override with `SIMPULSE_LOG_DIR`; disable with `SIMPULSE_LOG_FILE=0`. Use `SIMPULSE_LOG_LEVEL` for verbosity.

## Logging

Use `Microsoft.Extensions.Logging` with levels Trace/Debug/Information/Warning/Error/Critical.

Default level: Information (`SIMPULSE_LOG_LEVEL`).

Every long-running process logs start, major steps with durations, and a finish summary. Never log raw HR/energy payloads or pairing secrets. PIN is logged at Information once when a pairing window opens (and the tray balloon may show it). **Show current PIN** redisplays that PIN without logging it again.

## Tests without hardware

Ordinary automated tests use fixtures in `tests/fixtures`. An active iRacing session is never required for CI.

## Adding a dependency

Answer the five questions in [`THIRD_PARTY.md`](THIRD_PARTY.md) and record the license before merging.
