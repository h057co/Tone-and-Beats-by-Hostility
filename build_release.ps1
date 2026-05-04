# ============================================================================
# build_release.ps1 — Tone And Beats By Hostility
# One-click build, package, and release script.
#
# Usage:
#   .\build_release.ps1                   # Build + Installer only
#   .\build_release.ps1 -Publish          # Build + Installer + GitHub Release
#   .\build_release.ps1 -Publish -Notes "Changelog here"
#
# The version is read from CMakeLists.txt automatically.
# ============================================================================

param(
    [switch]$Publish,
    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$ProjectRoot\CMakeLists.txt")) { $ProjectRoot = $PSScriptRoot }

# --- Step 1: Extract version from CMakeLists.txt (Single Source of Truth) ---
$cmakeContent = Get-Content "$ProjectRoot\CMakeLists.txt" -Raw
if ($cmakeContent -match 'project\([^)]*VERSION\s+(\d+\.\d+\.\d+)') {
    $Version = $Matches[1]
} else {
    Write-Error "Could not extract version from CMakeLists.txt"
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Tone & Beats By Hostility — Release v$Version" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# --- Step 2: Configure (CMake generates version_info.iss automatically) ---
Write-Host "[1/5] Configuring project..." -ForegroundColor Yellow
cmake -B "$ProjectRoot\build" -S "$ProjectRoot" -G "Visual Studio 17 2022" -A x64 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "CMake configuration failed"; exit 1 }
Write-Host "  OK" -ForegroundColor Green

# --- Step 3: Build Release ---
Write-Host "[2/5] Building Release..." -ForegroundColor Yellow
cmake --build "$ProjectRoot\build" --config Release -j 8
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
Write-Host "  OK" -ForegroundColor Green

# --- Step 4: Generate Installer ---
Write-Host "[3/5] Generating Installer..." -ForegroundColor Yellow
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { Write-Error "Inno Setup not found at: $iscc"; exit 1 }

& $iscc "$ProjectRoot\Installers\tone_and_beats_installer.iss"
if ($LASTEXITCODE -ne 0) { Write-Error "Installer compilation failed"; exit 1 }

$InstallerName = "ToneBeats_v${Version}_Installer.exe"
$InstallerPath = "$ProjectRoot\Installers\$InstallerName"
if (-not (Test-Path $InstallerPath)) { Write-Error "Installer not found: $InstallerPath"; exit 1 }

$size = [math]::Round((Get-Item $InstallerPath).Length / 1MB, 2)
Write-Host "  OK — $InstallerName ($size MB)" -ForegroundColor Green

# --- Step 5: Publish to GitHub ---
if ($Publish) {
    Write-Host "[4/5] Pushing to GitHub..." -ForegroundColor Yellow
    Set-Location $ProjectRoot
    git add .
    git commit -m "Release v$Version"
    git push
    Write-Host "  OK" -ForegroundColor Green

    Write-Host "[5/5] Creating GitHub Release v$Version..." -ForegroundColor Yellow
    
    # Delete existing release if present
    gh release delete "v$Version" --yes 2>$null
    git tag -d "v$Version" 2>$null
    git push origin --delete "v$Version" 2>$null

    if ([string]::IsNullOrWhiteSpace($Notes)) {
        $Notes = "### Tone & Beats By Hostility v$Version`nRelease generated automatically."
    }

    gh release create "v$Version" $InstallerPath --title "Tone And Beats By Hostility v$Version" --notes $Notes
    if ($LASTEXITCODE -ne 0) { Write-Error "GitHub Release creation failed"; exit 1 }
    Write-Host "  OK" -ForegroundColor Green
} else {
    Write-Host "[4/5] Skipped (use -Publish to upload)" -ForegroundColor DarkGray
    Write-Host "[5/5] Skipped" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Release v$Version complete!" -ForegroundColor Green
Write-Host "  Installer: $InstallerPath" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
