param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Building SimPulse.sln ($Configuration)"
dotnet build "$root\SimPulse.sln" --configuration $Configuration
