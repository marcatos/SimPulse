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

XcodeGen spec is `project.yml` ([ADR 0012](adr/0012-xcodegen-apple-project.md)). Generate on a Mac:

```sh
export PATH="/opt/homebrew/bin:/opt/homebrew/Cellar/xcodegen/2.46.0/bin:$PATH"
xcodegen generate
./scripts/build-ios.sh
./scripts/test-ios.sh
./scripts/build-watch.sh
```

Do not hand-edit `project.pbxproj` for routine source changes. On Windows those scripts record **NOT EXECUTED**. Archive still needs a Development Team (`scripts/archive-ios.sh`).

Build Mac (2026-08-18): `simonemarcato@10.100.20.107` (`ssh simpulse-mac`), macOS 26.6.2, Xcode 26.6. Apple ID for this user: marcato.simone@gmail.com.

Expected future path: Windows workstation → git → CI → self-hosted Mac mini → TestFlight.

## Line endings

`.gitattributes` forces LF except for `.ps1`, `.sln`, `.bat`, `.cmd`.

## Windows Bridge tray vs console

On Windows the Bridge host builds as `WinExe` (no console window). A notification-area icon shows the pairing PIN in a balloon. **Show current PIN** redisplays the last PIN (tooltip/balloon) only while that pairing window is still open; after consume, expiry, or lockout it reports `pairing window closed` and does not rotate the PIN. **Pair new device** calls `BeginPairingWindow()` so a new PIN opens without restarting the process. The PIN stays in the tray tooltip until the window closes, a later PIN, or process exit.

If the tray STA/`NotifyIcon` fails or does not become ready within 5 seconds, Bridge logs ERROR and continues with `ConsolePairingUx` instead of exiting.

| Goal | How |
| --- | --- |
| Normal run (no console, PIN in tray) | Double-click the Windows build, or `dotnet run --project apps/windows-bridge/SimPulse.Bridge` |
| Debug with a console (logs visible; tray still used when interactive) | `dotnet run --project apps/windows-bridge/SimPulse.Bridge --property:OutputType=Exe` |
| Console pairing UX instead of tray | `SIMPULSE_BRIDGE_TRAY=0` (PIN logged at Information). Combine with `OutputType=Exe` so the console exists |
| Linux / Ubuntu CI | Stays `Exe` + `net8.0`; pairing UX is console |

`SIMPULSE_BRIDGE_CONSOLE=1` documents intent to debug with a console. It does **not** change `OutputType` (MSBuild cannot read that env at build time). Pass `--property:OutputType=Exe` (or set `OutputType` to `Exe` in the project while debugging).

With `WinExe`, the console is hidden. File logs are written under `%LOCALAPPDATA%\SimPulse\logs` (or the user-profile equivalent). Override with `SIMPULSE_LOG_DIR`; disable with `SIMPULSE_LOG_FILE=0`. Use `SIMPULSE_LOG_LEVEL` for verbosity.

## Windows Bridge TLS

The Bridge listens on `wss://127.0.0.1:8742/ws/` by default. On first TLS startup it creates `%LOCALAPPDATA%\SimPulse\certs\bridge-dev.pfx`; `SIMPULSE_BRIDGE_CERT_DIR` changes the generated-certificate directory. To supply an existing PFX, set `SIMPULSE_BRIDGE_CERT_PATH` and, when required, `SIMPULSE_BRIDGE_CERT_PASSWORD`. Never commit the PFX or password.

At Information level, startup logs include `TlsEnabled=true` and `TlsCertSha256=<fingerprint>`. The fingerprint is lowercase SHA-256 of the certificate DER, without colons. Clients must pin this exact value and reject a mismatch; the future IOS-005 pairing client will implement that client-side check.

TLS is enabled when `SIMPULSE_BRIDGE_TLS` is unset, empty, `1`, or any value except the explicit cleartext values `0`, `false`, and `off` (case-insensitive). Cleartext is for local diagnostics only:

```powershell
$env:SIMPULSE_BRIDGE_TLS = "0"
dotnet run --project apps/windows-bridge/SimPulse.Bridge --property:OutputType=Exe
# ws://127.0.0.1:8742/ws/
```

The Bridge fails fast if cleartext is selected with a non-loopback `SIMPULSE_BRIDGE_HOST`. TLS mode may bind to an explicitly configured LAN address; do not expose the Bridge to the public WAN.

## Logging

Use `Microsoft.Extensions.Logging` with levels Trace/Debug/Information/Warning/Error/Critical.

Default level: Information (`SIMPULSE_LOG_LEVEL`).

Every long-running process logs start, major steps with durations, and a finish summary. Never log raw HR/energy payloads. The pairing PIN is logged at Information **once**, by `PairingCoordinator.BeginPairingWindow` (`Pin=`). Tray/console UX may display the PIN but must not log `Pin=`. **Show current PIN** redisplays an open-window PIN without logging it. File logs therefore persist that coordinator PIN line — restrict ACLs on the log directory.

## Tests without hardware

Ordinary automated tests use fixtures in `tests/fixtures`. An active iRacing session is never required for CI.

## Live iRacing mmap smoke

With iRacing in a session or replay (`irsdkEnableMem=1` in `Documents\iRacing\app.ini`):

```powershell
pwsh -File scripts/smoke-iracing-mmap.ps1
```

The script waits for `Local\IRSDKMemMapFileName`, runs the Release Bridge host without `SIMPULSE_FIXTURE_PATH`, and looks for Information logs `mmap open succeeded` and `iRacing session started`. Do not commit those log files or pairing PINs.

## Adding a dependency

Answer the five questions in [`THIRD_PARTY.md`](THIRD_PARTY.md) and record the license before merging.
