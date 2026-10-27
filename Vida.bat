@echo off
setlocal
cd /d "%~dp0VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics"
start "" VidaLauncher.exe %*
