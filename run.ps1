param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Automation,
    [int]$Port = 9221,
    [switch]$Build
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

if ($Build) {
    & "$ProjectRoot\build.ps1" -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit 1
    }
}

$ExePath = Join-Path $ProjectRoot "src\Letterist\bin\x64\$Configuration\net9.0-windows10.0.22621.0\Letterist.exe"

if (-not (Test-Path $ExePath)) {
    Write-Host "Executable not found at: $ExePath" -ForegroundColor Red
    Write-Host "Please run build.ps1 first or use -Build flag" -ForegroundColor Yellow
    exit 1
}

$Arguments = @()
if ($Automation) {
    $Arguments += "--automation"
    $Arguments += "--port"
    $Arguments += $Port.ToString()
    Write-Host "Starting Letterist in automation mode on port $Port..." -ForegroundColor Cyan
} else {
    Write-Host "Starting Letterist..." -ForegroundColor Cyan
}

& $ExePath @Arguments
