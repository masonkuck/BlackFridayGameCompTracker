@echo off
rem Starts the tournament tracker. Installs the .NET 10 SDK if it's missing, offers to open the
rem firewall so phones on your Wi-Fi can connect, then runs the app. SQLite needs no install:
rem it ships inside the app.
setlocal
cd /d "%~dp0"
title Comp Tracker
set PORT=5080

rem ---- 1. .NET 10 SDK ----
call :has_sdk
if not errorlevel 1 goto :firewall

echo The .NET 10 SDK is not installed. Installing it now (one time)...
echo.
where winget >nul 2>nul
if errorlevel 1 goto :install_script
winget install --id Microsoft.DotNet.SDK.10 --exact --accept-source-agreements --accept-package-agreements
rem This window doesn't see PATH changes from the install, so add the default location by hand.
set "PATH=%ProgramFiles%\dotnet;%PATH%"
call :has_sdk
if not errorlevel 1 goto :firewall

:install_script
rem No winget (or it failed): use Microsoft's install script, which needs no admin rights.
echo Downloading the .NET install script from dot.net...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = Join-Path $env:TEMP 'dotnet-install.ps1'; Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $s; & $s -Channel 10.0"
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
call :has_sdk
if not errorlevel 1 goto :firewall
echo.
echo Couldn't install .NET 10. Install the ".NET 10 SDK" from https://dotnet.microsoft.com/download
echo and run this file again.
pause
exit /b 1

rem ---- 2. Firewall rule so phones can connect ----
:firewall
netsh advfirewall firewall show rule name=CompTracker >nul 2>nul
if not errorlevel 1 goto :run
echo.
echo Phones on your Wi-Fi can't reach this PC until Windows Firewall allows port %PORT%.
choice /c YN /m "Add the firewall rule now (asks for admin)"
if errorlevel 2 goto :run
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile','-Command','New-NetFirewallRule -DisplayName CompTracker -Direction Inbound -Protocol TCP -LocalPort %PORT% -Action Allow'"

rem ---- 3. Run ----
:run
echo.
echo Starting... the first start takes a minute while it builds.
echo Open http://localhost:%PORT% on this PC. Phones: scan the QR code on the dashboard.
echo Press Ctrl+C in this window to stop.
echo.
dotnet run --project src\CompTracker
pause
exit /b

rem Sets errorlevel 0 if a .NET 10 SDK is installed, 1 if not.
:has_sdk
dotnet --list-sdks 2>nul | findstr /b "10." >nul
exit /b
