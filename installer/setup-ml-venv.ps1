#Requires -Version 5.1
<#
.SYNOPSIS
  Creates the Python virtual environment for the Ml/ folder and installs its
  dependencies. Run once after installing SportsPredictor (the installer runs
  this automatically — see SportsPredictor.iss's [Run] section) or any time you
  need to rebuild it. Requires Python 3.12 already installed on this machine
  (see README.md — Python itself is not bundled by the installer).
#>

param(
    [string]$MlDirectory = (Join-Path $PSScriptRoot "Ml")
)

$ErrorActionPreference = "Stop"

$python = Get-Command py -ErrorAction SilentlyContinue
if ($python) {
    $pythonCmd = { param($args) & py -3.12 @args }
} else {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) {
        Write-Error "Python was not found on PATH. Install Python 3.12 first: https://www.python.org/downloads/"
        exit 1
    }
    $pythonCmd = { param($args) & python @args }
}

Write-Host "== Creating virtual environment in $MlDirectory\.venv ==" -ForegroundColor Cyan
& $pythonCmd @("-m", "venv", "$MlDirectory\.venv")
if ($LASTEXITCODE -ne 0) { throw "Failed to create the virtual environment." }

Write-Host "== Installing requirements ==" -ForegroundColor Cyan
& "$MlDirectory\.venv\Scripts\pip.exe" install -r "$MlDirectory\requirements.txt"
if ($LASTEXITCODE -ne 0) { throw "Failed to install ML service requirements." }

Write-Host "Done." -ForegroundColor Green
