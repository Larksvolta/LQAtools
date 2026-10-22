#Requires -Version 5.1
<#
.SYNOPSIS
    Launches the LQAtools client (WinUI) for local development.

.DESCRIPTION
    Opens the built client executable. Optionally points it at a server and/or
    makes it pretend to be a given user, so you can run several instances side
    by side as different identities.

    -Server writes the URL into server.txt next to the exe, so the client runs
    in shared-server mode. Omit it to keep the current mode (local files unless
    server.txt already exists).

    -User sets the LQATOOLS_USER environment variable for this instance. The
    server must have been started with Auth:AllowUserOverride=true for the name
    to take effect; otherwise the real Windows account is used.

.PARAMETER User
    Name to simulate (e.g. Alice). Omit to use the real Windows account.

.PARAMETER Server
    Server base URL (e.g. http://localhost:5217). Omit to leave the mode as-is.

.PARAMETER Configuration
    Build configuration to launch from. Default: Debug

.PARAMETER Wait
    Wait for the client to exit before returning.

.EXAMPLE
    .\scripts\run-client.ps1 -Server http://localhost:5217 -User Alice

.EXAMPLE
    # Launch two simulated users in two shells
    .\scripts\run-client.ps1 -User Alice
    .\scripts\run-client.ps1 -User Bob
#>
[CmdletBinding()]
param(
    [string]$User,
    [string]$Server,
    [string]$Configuration = "Debug",
    [switch]$Wait
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

$candidates = Get-ChildItem -Path (Join-Path $repoRoot "LQAtools\bin\$Configuration") `
    -Recurse -Filter "LQAtools.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending

if (-not $candidates) {
    throw "Client not built. Run: dotnet build `"$repoRoot\LQAtools.sln`" -c $Configuration"
}

$exe = $candidates[0].FullName
$exeDir = Split-Path -Parent $exe

if ($Server) {
    Set-Content -LiteralPath (Join-Path $exeDir "server.txt") -Value $Server -NoNewline
    Write-Host "Client will connect to server: $Server" -ForegroundColor Cyan
}

if ($User) {
    $env:LQATOOLS_USER = $User
    Write-Host "Simulating user '$User' (server must allow the override)." -ForegroundColor Yellow
} else {
    $env:LQATOOLS_USER = $null
    Write-Host "Using the real Windows account." -ForegroundColor DarkGray
}

Write-Host "Launching $exe" -ForegroundColor Cyan
if ($Wait) {
    & $exe
} else {
    Start-Process -FilePath $exe
}
