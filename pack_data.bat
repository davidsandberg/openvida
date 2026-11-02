@echo off
setlocal EnableDelayedExpansion
title OpenVIDA — Data Archive Packer

cd /d "%~dp0"

set "SEVENZIP=%~dp0tools\7z.exe"
if not exist "%SEVENZIP%" set "SEVENZIP=%~dp0tools\7za.exe"
if not exist "%SEVENZIP%" (
    echo [ERROR] 7-Zip binary not found in tools\ directory.
    pause
    exit /b 1
)

set "OUT_ARCHIVE=%~1"
if "%OUT_ARCHIVE%"=="" set "OUT_ARCHIVE=%~dp0..\OpenVIDA_Data_2014D.7z"

echo ============================================================
echo   OpenVIDA — Standalone Prerequisite Data Packer
echo ============================================================
echo.
echo Source VIDA Directory : "%~dp0VIDA"
echo Target Archive File   : "%OUT_ARCHIVE%"
echo.
echo Note: Electronic Wiring Diagrams (wiring_diagrams\US_Eng) are
echo excluded by default so you can keep Volvo_EWD_2014D.iso separate.
echo (Pass --include-ewd as 2nd argument to bundle EWD inside the archive).
echo.

set "EXCLUDE_EWD=-xr!wiring_diagrams"
if /i "%~2"=="--include-ewd" (
    set "EXCLUDE_EWD="
    echo [INFO] Including wiring_diagrams in archive.
)

echo [1/3] Flushing and taking SQL Server databases temporarily OFFLINE...
sqlcmd -S "localhost\SQLEXPRESS" -E -Q "DECLARE @sql NVARCHAR(MAX) = N''; SELECT @sql += N'ALTER DATABASE [' + name + N'] SET OFFLINE WITH ROLLBACK IMMEDIATE; ' FROM sys.databases WHERE name IN ('BaseData','EPC','AccessServer','DiagSwdlRepository','CarCom','DiagSwdlSession','DiceTiming','imagerepository','servicerep_en-US') AND state = 0; EXEC sp_executesql @sql;" >nul 2>&1

echo [2/3] Compressing VIDA directory into "%OUT_ARCHIVE%"...
"%SEVENZIP%" a -t7z -mx=3 "%OUT_ARCHIVE%" "%~dp0VIDA\*" %EXCLUDE_EWD% -xr!*.log -xr!messagingjournal -xr!tmp -xr!data\tx-object-store

set "PACK_ERR=%ERRORLEVEL%"

echo [3/3] Bringing SQL Server databases back ONLINE...
sqlcmd -S "localhost\SQLEXPRESS" -E -Q "DECLARE @sql NVARCHAR(MAX) = N''; SELECT @sql += N'ALTER DATABASE [' + name + N'] SET ONLINE; ' FROM sys.databases WHERE name IN ('BaseData','EPC','AccessServer','DiagSwdlRepository','CarCom','DiagSwdlSession','DiceTiming','imagerepository','servicerep_en-US') AND state != 0; EXEC sp_executesql @sql;" >nul 2>&1

echo.
if not "%PACK_ERR%"=="0" (
    echo [ERROR] Archive creation failed with exit code %PACK_ERR%.
    exit /b %PACK_ERR%
)

echo ============================================================
echo   DATA ARCHIVE CREATED SUCCESSFULLY!
echo   Output: %OUT_ARCHIVE%
echo ============================================================
exit /b 0
