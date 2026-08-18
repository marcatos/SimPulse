# BRIDGE-007 handoff

## Task

BRIDGE-007 — Tray / background UX (Task 2 of 3: Windows NotifyIcon pairing adapter).

## Goal

Compile a host-only WinForms `NotifyIcon` pairing UX without putting `System.Windows.Forms` in Core, and without breaking Ubuntu `dotnet test SimPulse.sln`.

## Status

IN_PROGRESS (Task 2 complete; do not mark BRIDGE-007 DONE)

## Files changed

- `apps/windows-bridge/SimPulse.Bridge.Core/Application/TrayPairingUxText.cs` — menu labels + PIN balloon/tooltip text (no WinForms)
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/TrayPairingUxTextTests.cs`
- `apps/windows-bridge/SimPulse.Bridge/Tray/NotifyIconPairingUx.cs` — `#if WINDOWS_TRAY` NotifyIcon adapter
- `apps/windows-bridge/SimPulse.Bridge/SimPulse.Bridge.csproj` — Windows `net8.0-windows` + `UseWindowsForms` + `WINDOWS_TRAY`; Linux stays `net8.0`
- `docs/BACKLOG.md`, `docs/CURRENT_STATE.md`, `docs/KNOWN_ISSUES.md`, this handoff

## Decisions made

- SDK 8.0.424 rejects `UseWindowsForms` on `net8.0` (NETSDK1136). Host TFM is `net8.0-windows` on Windows only so Ubuntu CI can still build `net8.0` without WinForms types.
- Adapter is not registered in `Program.cs` (Task 3). Exit injects `IHostApplicationLifetime` and disposes the icon on `ApplicationStopping`.
- PIN display text lives in Core so Ubuntu tests cover balloon/tooltip content; NotifyIcon itself is not unit-tested.

## Tests executed

- Focused `TrayPairingUxTextTests` — 3 passed
- Host Release build on Windows — 0 warnings, 0 errors (`net8.0-windows`)
- `dotnet test SimPulse.sln --configuration Release` — 81 passed, 0 failed (Domain 6, Analytics 9, Protocol 7, Bridge.Core 59)
- `dotnet build SimPulse.sln --configuration Release` — 0 warnings, 0 errors

## Tests passing

Yes

## Known failures

None.

## Remaining work

- Task 3: register NotifyIcon vs Console UX, STA message loop / WinExe, DEVELOPMENT/.env docs, mark BRIDGE-007 ACs
- Do not mark BRIDGE-007 DONE until tray ACs are met

## Risks

Without Task 3 wiring, the adapter never runs. NotifyIcon still needs an STA message loop (`Application.Run`) when the Worker is MTA.

## Suggested next action

Task 3: wire `Program.cs` (Windows interactive → NotifyIcon; else Console), hide console via WinExe on Windows, document how to run tray vs console.
