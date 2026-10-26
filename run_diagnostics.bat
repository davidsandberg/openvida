@echo off
setlocal EnableDelayedExpansion
title Volvo VIDA 2014D Standalone Launcher
cd /d "%~dp0"

echo ============================================================
echo   Starting Volvo VIDA 2014D Standalone Diagnostics
echo ============================================================
echo.

rem 1. Check SQL Server
sc query "MSSQL$SQLEXPRESS" | find "RUNNING" >nul
if errorlevel 1 (
    echo [INFO] Starting SQL Server SQLEXPRESS...
    net start "MSSQL$SQLEXPRESS" >nul 2>&1
)

rem 2. Check JBoss HTTP Listener Port 80
powershell -NoProfile -Command "$tcp = New-Object System.Net.Sockets.TcpClient; try { $tcp.Connect('127.0.0.1', 80); exit 0 } catch { exit 1 }" >nul 2>&1
if errorlevel 1 goto :start_jboss
goto :launch_app

:start_jboss
echo [INFO] Starting JBoss Application Server in background...
pushd "%~dp0VIDA"
start /min "VIDA JBoss Server" cmd /c "jboss.bat start"
popd
echo [INFO] Waiting for JBoss backend to initialize...

:wait_loop
timeout /t 2 /nobreak >nul
powershell -NoProfile -Command "$tcp = New-Object System.Net.Sockets.TcpClient; try { $tcp.Connect('127.0.0.1', 80); exit 0 } catch { exit 1 }" >nul 2>&1
if errorlevel 1 goto wait_loop
echo [INFO] JBoss is up and responding!

:launch_app
rem 3. Launch Standalone Diagnostics Application
set "VIDA_HOME=%~dp0VIDA\"
set "DIAG_DIR=%~dp0VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics"

echo [INFO] Launching Diagnostics GUI...
start /D "%DIAG_DIR%" "" VidaLauncher.exe
echo [OK] VIDA Diagnostics is running!
