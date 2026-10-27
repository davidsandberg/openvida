@echo off
setlocal
cd /d "%~dp0VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics"
if exist "VidaLauncher_new.exe" (
    taskkill /f /im VidaLauncher.exe >nul 2>&1
    timeout /t 1 /nobreak >nul
    move /y "VidaLauncher_new.exe" "VidaLauncher.exe" >nul 2>&1
)
start "" VidaLauncher.exe %*
