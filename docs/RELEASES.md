# Releases

Marketing versions follow SemVer for the product (`0.1.0` while pre-release).

Protocol versions are independent (`protocolVersion: 1` in envelopes).

## What belongs where

| Artifact | Role |
| --- | --- |
| `CHANGELOG.md` | User-facing notable changes per version |
| This file | How we cut a release |
| Git tags | `vMAJOR.MINOR.PATCH` |

Do not duplicate long changelogs here.

## Apple

TestFlight / App Store require a Mac, signing identities, and App Store Connect API keys stored as CI secrets — none of which exist yet.

Record Apple archives as NOT EXECUTED until then.

## Windows Bridge

`dotnet publish` of `apps/windows-bridge/SimPulse.Bridge` for `win-x64`. Code signing is not set up.

## Checklist (future)

1. Tests green on available platforms
2. CURRENT_STATE and CHANGELOG updated
3. Version bump in `Directory.Build.props` and Apple marketing version (when it exists)
4. Tag
5. GitHub Release notes from CHANGELOG
