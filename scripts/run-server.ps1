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

.PARAMETER AllowedGroup
    Optional Windows/AD group name. When set, only members of that group may use
    the server. The group is resolved (and must resolve) at startup.

.PARAMETER AllowInsecureRemote
    Allow plain-HTTP connections from non-local clients. Off by default: remote
    clients must use https://, otherwise Windows credentials would travel in the
    clear. Enable only on a trusted isolated network.

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
    [string]$AllowedGroup,
    [switch]$AllowInsecureRemote,
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
$env:Auth__AllowedGroup = if ($AllowedGroup) { $AllowedGroup } else { $null }
$env:Security__AllowInsecureRemote = if ($AllowInsecureRemote) { "true" } else { $null }
if ($AllowUserOverride) {
    Write-Host "Identity override ENABLED (loopback callers may set X-TableNotes-User)." -ForegroundColor Yellow
} else {
    Write-Host "Strict Windows auth: anonymous API calls are rejected (401)." -ForegroundColor DarkGray
}
if ($AllowedGroup) {
    Write-Host "Only members of Windows group '$AllowedGroup' may use the server." -ForegroundColor Cyan
}
if ($AllowInsecureRemote) {
    Write-Host "Plain HTTP allowed from remote clients (credentials are NOT protected)." -ForegroundColor Red
}

$runArgs = @("run", "--project", $project, "-c", $Configuration, "--urls", $Url)
if ($NoBuild) { $runArgs += "--no-build" }

Write-Host "Starting server at $Url ..." -ForegroundColor Cyan
& dotnet @runArgs
