param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$SolutionFile = Join-Path $ProjectRoot "Letterist.sln"

$VSWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $VSWhere) {
    $MSBuildPath = & $VSWhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\amd64\MSBuild.exe | Select-Object -First 1
} else {
    $MSBuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
}

if (-not (Test-Path $MSBuildPath)) {
    Write-Host "MSBuild not found at: $MSBuildPath" -ForegroundColor Red
    Write-Host "Please install Visual Studio with the '.NET desktop development' workload." -ForegroundColor Yellow
    exit 1
}

Write-Host "Building Letterist ($Configuration)..." -ForegroundColor Cyan
Write-Host "Using MSBuild: $MSBuildPath" -ForegroundColor Gray

if ($Clean) {
    Write-Host "Cleaning previous build artifacts..." -ForegroundColor Yellow
    & $MSBuildPath $SolutionFile -t:Clean -p:Configuration=$Configuration -p:Platform=x64 -verbosity:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Clean failed!" -ForegroundColor Red
        exit 1
    }
}

dotnet restore $SolutionFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore failed!" -ForegroundColor Red
    exit 1
}

& $MSBuildPath $SolutionFile -p:Configuration=$Configuration -p:Platform=x64 -verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build succeeded!" -ForegroundColor Green
