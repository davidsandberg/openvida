@echo off
setlocal
cd /d "%~dp0"

set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "DIAG_DIR=%~dp0VIDA\jboss\standalone\deployments\VidaEar.ear\VidaWeb.war\Diagnostics"
set "SRC_DIR=%~dp0src"
set "ICON=%~dp0assets\vida_icon.ico"

echo ============================================================
echo   Building OpenVIDA Standalone Diagnostics
echo ============================================================

if exist "%ICON%" (
    copy /y "%ICON%" "%DIAG_DIR%\vida_icon.ico" >nul
)

pushd "%DIAG_DIR%"

echo [1/2] Compiling DummyIsoView.dll (CGM Vector Network Renderer)...
"%CSC%" /nologo /platform:x86 /target:library ^
    /out:DummyIsoView_new.dll ^
    /r:Interop.ISOVIEWXLib.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll ^
    "%SRC_DIR%\IsoView\DummyIsoView.cs"
if errorlevel 1 (
    echo [ERROR] DummyIsoView.dll compilation failed!
    popd
    exit /b 1
)
move /y DummyIsoView_new.dll DummyIsoView.dll >nul 2>&1

echo [2/2] Compiling VidaLauncher.exe from modular src\ files...
"%CSC%" /nologo /platform:x86 /target:exe /win32icon:vida_icon.ico ^
    /out:VidaLauncher_new.exe ^
    /r:UI.dll ^
    /r:Core.dll ^
    /r:Workflow.dll ^
    /r:Util.dll ^
    /r:ValueObjects.dll ^
    /r:System.Windows.Forms.DataVisualization.dll ^
    /r:DummyIsoView.dll ^
    /r:GuiComponents.dll ^
    /r:ProgressHandler.dll ^
    /r:System.IO.Compression.dll ^
    /r:System.IO.Compression.FileSystem.dll ^
    /r:System.Data.dll ^
    /r:Microsoft.Web.WebView2.WinForms.dll ^
    /r:Microsoft.Web.WebView2.Core.dll ^
    /r:BizServices.Scripting.dll ^
    /r:AppServices.Scripting.dll ^
    "%SRC_DIR%\Program.cs" ^
    "%SRC_DIR%\Core\*.cs" ^
    "%SRC_DIR%\Hardware\*.cs" ^
    "%SRC_DIR%\Diagnostics\*.cs" ^
    "%SRC_DIR%\Documents\*.cs" ^
    "%SRC_DIR%\Tabs\*.cs" ^
    "%SRC_DIR%\Testing\*.cs"
if errorlevel 1 (
    echo [ERROR] VidaLauncher.exe compilation failed!
    popd
    exit /b 1
)
move /y VidaLauncher_new.exe VidaLauncher.exe >nul 2>&1

popd
echo [OK] Build succeeded! Output: %DIAG_DIR%\VidaLauncher.exe
