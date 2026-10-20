#Requires -Version 5.1
<#
.SYNOPSIS
    Starts the TableNotes server for local development.

.DESCRIPTION
    Runs the ASP.NET Core server (TableNotes.Server) bound to a URL. By default
    it uses Windows Integrated Authentication, so anonymous API calls are
    rejected with 401.

    With -AllowUserOverride it also enables the dev-only identity override:
    a loopback caller may send an X-TableNotes-User header to act as that user.
    This is meant for simulating multiple users on one machine. The override is
    ignored for non-loopback callers.

.PARAMETER Url
    Base URL to bind. Default: http://localhost:5217
    Use http://0.0.0.0:5217 to accept connections from the LAN.

.PARAMETER AllowUserOverride
    Enable the dev-only X-TableNotes-User identity override (loopback only).

.PARAMETER Configuration
    Build configuration. Default: Debug

.PARAMETER NoBuild
    Skip the implicit build step before running.

.EXAMPLE
    .\scripts\run-server.ps1

.EXAMPLE
    .\scripts\run-server.ps1 -AllowUserOverride -Url http://0.0.0.0:5217
#>
[CmdletBinding()]
param(
    [string]$Url = "http://localhost:5217",
    [switch]$AllowUserOverride,
    [string]$Configuration = "Debug",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot "TableNotes.Server\TableNotes.Server.csproj"

if (-not (Test-Path -LiteralPath $project)) {
    throw "Server project not found at '$project'."
}

$env:Auth__AllowUserOverride = if ($AllowUserOverride) { "true" } else { $null }
if ($AllowUserOverride) {
    Write-Host "Identity override ENABLED (loopback callers may set X-TableNotes-User)." -ForegroundColor Yellow
} else {
    Write-Host "Strict Windows auth: anonymous API calls are rejected (401)." -ForegroundColor DarkGray
}

$runArgs = @("run", "--project", $project, "-c", $Configuration, "--urls", $Url)
if ($NoBuild) { $runArgs += "--no-build" }

Write-Host "Starting server at $Url ..." -ForegroundColor Cyan
& dotnet @runArgs
