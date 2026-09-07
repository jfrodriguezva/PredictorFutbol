#Requires -Version 5.1
<#
.SYNOPSIS
  Builds a self-contained "staging" folder with everything the installer needs:
  the published API, the WPF launcher, the Next.js standalone build, and the
  Python ML service's source (Python itself is not bundled — see README.md).

  Run this before compiling installer/SportsPredictor.iss with Inno Setup.
#>

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $PSScriptRoot "staging"

Write-Host "== Cleaning staging directory ==" -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Path $staging | Out-Null

# --- API (C#) ---------------------------------------------------------------
Write-Host "== Publishing SportsPredictor.Api ==" -ForegroundColor Cyan
dotnet publish "$root/backend/SportsPredictor.Api/SportsPredictor.Api.csproj" `
    -c Release -o "$staging/Api" --self-contained false
if ($LASTEXITCODE -ne 0) { throw "API publish failed." }

# --- WPF launcher -------------------------------------------------------------
Write-Host "== Publishing SportsPredictor.Desktop ==" -ForegroundColor Cyan
dotnet publish "$root/desktop/SportsPredictor.Desktop/SportsPredictor.Desktop.csproj" `
    -c Release -o "$staging/Desktop" --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed." }

# Ship the installed-layout defaults (paths relative to the Desktop exe) —
# never the machine-specific launcher.settings.local.json used for dev testing.
Copy-Item "$PSScriptRoot/launcher.settings.json" "$staging/Desktop/launcher.settings.json" -Force

# --- Frontend (Next.js standalone) -------------------------------------------
Write-Host "== Building frontend (standalone output) ==" -ForegroundColor Cyan
Push-Location "$root/frontend"
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed." }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed." }
} finally {
    Pop-Location
}

New-Item -ItemType Directory -Path "$staging/Frontend" -Force | Out-Null
Copy-Item "$root/frontend/.next/standalone/*" "$staging/Frontend/" -Recurse -Force
New-Item -ItemType Directory -Path "$staging/Frontend/.next/static" -Force | Out-Null
Copy-Item "$root/frontend/.next/static/*" "$staging/Frontend/.next/static/" -Recurse -Force
if (Test-Path "$root/frontend/public") {
    Copy-Item "$root/frontend/public" "$staging/Frontend/public" -Recurse -Force
}

# --- ML service (Python source — see README.md for the "Python must be installed
#     on the target machine" trade-off; this does not bundle a Python runtime) ---
Write-Host "== Copying ML service source ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Path "$staging/Ml" -Force | Out-Null
Copy-Item "$root/ml/app" "$staging/Ml/app" -Recurse -Force
Copy-Item "$root/ml/training" "$staging/Ml/training" -Recurse -Force
Copy-Item "$root/ml/evaluation" "$staging/Ml/evaluation" -Recurse -Force
Copy-Item "$root/ml/backtesting" "$staging/Ml/backtesting" -Recurse -Force
New-Item -ItemType Directory -Path "$staging/Ml/models" -Force | Out-Null
Copy-Item "$root/ml/requirements.txt" "$staging/Ml/requirements.txt" -Force

# --- database/ placeholder (SQLite file is created on first run) -------------
New-Item -ItemType Directory -Path "$staging/database" -Force | Out-Null

Write-Host ""
Write-Host "Staging build complete: $staging" -ForegroundColor Green
Write-Host "Next: on the target/build machine, run installer/setup-ml-venv.ps1 once," -ForegroundColor Green
Write-Host "then compile installer/SportsPredictor.iss with Inno Setup." -ForegroundColor Green
