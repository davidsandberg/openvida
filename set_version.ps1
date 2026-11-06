<#
.SYNOPSIS
    Sets the OpenVIDA revision across the suite, updates the installer and MCP bridge,
    runs the build, and prepares the GitHub release tag.

.EXAMPLE
    .\set_version.ps1 1.1.0
    .\set_version.ps1 v1.0.1
#>

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

$cleanVer = $Version.Trim()
if ($cleanVer.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) {
    $cleanVer = $cleanVer.Substring(1)
}
$tag = "v$cleanVer"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Setting OpenVIDA Version to $cleanVer (Tag: $tag)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Update VERSION file
$versionFile = Join-Path $PSScriptRoot "VERSION"
Set-Content -Path $versionFile -Value $cleanVer -NoNewline
Write-Host "[1/3] Updated VERSION file -> $cleanVer" -ForegroundColor Green

# 2. Run build.bat to regenerate VersionInfo.cs, compile binaries and installer
Write-Host "[2/3] Executing build.bat to compile with new version..." -ForegroundColor Yellow
$buildBat = Join-Path $PSScriptRoot "build.bat"
& cmd.exe /c "`"$buildBat`""
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed! Please resolve compilation issues." -ForegroundColor Red
    exit 1
}

Write-Host "[3/3] Build completed successfully with version $cleanVer!" -ForegroundColor Green
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Ready to publish GitHub Release:" -ForegroundColor Cyan
Write-Host "   git commit -am `"Release $tag`"" -ForegroundColor Yellow
Write-Host "   git tag $tag" -ForegroundColor Yellow
Write-Host "   git push origin main --tags" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan
