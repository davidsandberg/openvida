@echo off
setlocal
title Volvo VIDA 2014D Standalone Launcher
cd /d "%~dp0"

echo ============================================================
echo   Starting Volvo VIDA 2014D Standalone Diagnostics (Native)
echo ============================================================
echo.

:: 1. Check SQL Server
sc query "MSSQL$SQLEXPRESS" | find "RUNNING" >nul
if %errorlevel% neq 0 (
    echo [INFO] Starting SQL Server (SQLEXPRESS)...
    net start "MSSQL$SQLEXPRESS" >nul 2>&1
)

:: 2. Check JBoss HTTP Listener (Port 80)
powershell -NoProfile -Command "$tcp = New-Object System.Net.Sockets.TcpClient; try { $tcp.Connect('127.0.0.1', 80); exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Starting JBoss Application Server in background...
    pushd "%~dp0VIDA"
    start /min "VIDA JBoss Server" cmd /c "jboss.bat start"
    popd
    echo [INFO] Waiting for JBoss backend to initialize...
    :wait_loop
    timeout /t 2 /nobreak >nul
    powershell -NoProfile -Command "$tcp = New-Object System.Net.Sockets.TcpClient; try { $tcp.Connect('127.0.0.1', 80); exit 0 } catch { exit 1 }" >nul 2>&1
    if %errorlevel% neq 0 goto wait_loop
    echo [INFO] JBoss is up and responding!
)

:: 3. Launch Standalone Diagnostics Application
set "VIDA_HOME=%~dp0VIDA\"
set "LAUNCHER=%~dp0VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics\VidaLauncher.exe"

echo [INFO] Launching Diagnostics GUI...
start "" "%LAUNCHER%"
echo [OK] VIDA Diagnostics is running!
