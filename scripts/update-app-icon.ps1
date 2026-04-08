param(
    [string]$SourcePng = "src/Letterist/Assets/AppIcon.png",
    [string]$OutputIco = "src/Letterist/Assets/AppIcon.ico"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $SourcePng)) {
    throw "Source PNG not found: $SourcePng"
}

$pngBytes = [System.IO.File]::ReadAllBytes($SourcePng)
$outDir = Split-Path -Parent $OutputIco
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# ICO container with a single 256x256 32-bit image payload.
# Width/height bytes are 0 to indicate 256.
$stream = [System.IO.File]::Open($OutputIco, [System.IO.FileMode]::Create)
$writer = New-Object System.IO.BinaryWriter($stream)

$writer.Write([UInt16]0)      # reserved
$writer.Write([UInt16]1)      # type = icon
$writer.Write([UInt16]1)      # image count
$writer.Write([byte]0)        # width = 256
$writer.Write([byte]0)        # height = 256
$writer.Write([byte]0)        # color count
$writer.Write([byte]0)        # reserved
$writer.Write([UInt16]1)      # color planes
$writer.Write([UInt16]32)     # bits per pixel
$writer.Write([UInt32]$pngBytes.Length)
$writer.Write([UInt32]22)     # header (6) + entry (16)
$writer.Write($pngBytes)

$writer.Flush()
$writer.Close()
$stream.Close()

Write-Host "Updated icon: $OutputIco from $SourcePng"
