@echo off
setlocal EnableExtensions
title LQAtools Demo

set "ROOT=%~dp0"
set "SERVER_URL=http://localhost:5217"
set "USER1=%~1"
set "USER2=%~2"
if not defined USER1 set "USER1=Alice"
if not defined USER2 set "USER2=Bob"

echo Starting LQAtools demo as "%USER1%" and "%USER2%" ...

powershell -NoProfile -Command "try { $c = New-Object Net.Sockets.TcpClient; $c.Connect('127.0.0.1', 5217); $c.Close(); exit 1 } catch { exit 0 }"
if errorlevel 1 (
    echo.
    echo [ERROR] Something is already listening on port 5217.
    echo Stop the existing server first, then run this file again.
    echo.
    pause
    exit /b 1
)

set "CLIENT_EXE="
for /r "%ROOT%LQAtools\bin\Debug" %%F in (LQAtools.exe) do set "CLIENT_EXE=%%F"
if not defined CLIENT_EXE (
    echo Client not built. Building it now...
    dotnet build "%ROOT%LQAtools\LQAtools.csproj" -c Debug -v q
    if errorlevel 1 goto :fail
)

echo Starting server with identity override...
start "LQAtools Server" powershell -NoProfile -ExecutionPolicy Bypass -NoExit -File "%ROOT%scripts\run-server.ps1" -AllowUserOverride -Url %SERVER_URL%

echo Waiting for the server to accept connections...
powershell -NoProfile -Command "$deadline = (Get-Date).AddSeconds(90); while ((Get-Date) -lt $deadline) { try { $c = New-Object Net.Sockets.TcpClient; $c.Connect('127.0.0.1', 5217); $c.Close(); exit 0 } catch { Start-Sleep -Milliseconds 500 } }; exit 1"
if errorlevel 1 (
    echo [ERROR] The server did not start within 90 seconds.
    echo Check the "LQAtools Server" window for details.
    echo.
    pause
    exit /b 1
)

echo Launching client as "%USER1%"...
start "LQAtools - %USER1%" powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\run-client.ps1" -Server %SERVER_URL% -User "%USER1%"

timeout /t 2 /nobreak >nul

echo Launching client as "%USER2%"...
start "LQAtools - %USER2%" powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\run-client.ps1" -Server %SERVER_URL% -User "%USER2%"

echo.
echo Server and two clients launched.
echo Keep the "LQAtools Server" window open; press Ctrl+C there to stop the server.
timeout /t 5 /nobreak >nul
exit /b 0

:fail
echo.
echo [ERROR] The demo could not start.
echo.
pause
exit /b 1
