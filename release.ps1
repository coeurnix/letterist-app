param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release",
    [string]$OutputRoot = (Join-Path $PSScriptRoot "artifacts\release"),
    [string]$DistributionName = "Letterist-win-x64",
    [switch]$CreateZip,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$SolutionFile = Join-Path $ProjectRoot "Letterist.sln"
$ProjectFile = Join-Path $ProjectRoot "src\Letterist\Letterist.csproj"
$ReadmeFile = Join-Path $ProjectRoot "README.md"
$LicenseFile = Join-Path $ProjectRoot "LICENSE.txt"
$TargetFramework = "net9.0-windows10.0.22621.0"

function Resolve-MSBuildPath {
    $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vsWhere) {
        $candidate = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\amd64\MSBuild.exe" | Select-Object -First 1
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    $fallback = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
    if (Test-Path $fallback) {
        return $fallback
    }

    throw "MSBuild not found. Install Visual Studio with the '.NET desktop development' workload."
}

function Get-ProjectVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectFilePath
    )

    $projectContent = Get-Content -Path $ProjectFilePath -Raw
    if ($projectContent -match '<Version>\s*([^<]+)\s*</Version>') {
        return $matches[1].Trim()
    }

    throw "Unable to find <Version> in project file: $ProjectFilePath"
}

function Get-GlobalPackagesPath {
    $line = dotnet nuget locals global-packages --list | Select-String "global-packages:"
    if (-not $line) {
        throw "Unable to resolve NuGet global-packages location."
    }

    return $line.ToString().Split(":", 2)[1].Trim()
}

function Get-RuntimePackages {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssetsPath
    )

    if (-not (Test-Path $AssetsPath)) {
        throw "Assets file not found: $AssetsPath"
    }

    $assets = Get-Content $AssetsPath -Raw | ConvertFrom-Json
    $targetName = $assets.targets.PSObject.Properties.Name | Where-Object { $_ -like "*win-x64*" } | Select-Object -First 1
    if (-not $targetName) {
        $targetName = $assets.targets.PSObject.Properties.Name | Select-Object -First 1
    }
    if (-not $targetName) {
        throw "No targets found in $AssetsPath"
    }

    $packages = New-Object System.Collections.Generic.List[object]
    foreach ($lib in $assets.targets.$targetName.PSObject.Properties) {
        if ($lib.Name -notmatch "/") {
            continue
        }

        if (-not ($lib.Value.runtime -or $lib.Value.native)) {
            continue
        }

        $parts = $lib.Name.Split("/")
        if ($parts.Count -ne 2) {
            continue
        }

        $packages.Add([pscustomobject]@{
                Name = $parts[0]
                Version = $parts[1]
            })
    }

    return $packages | Sort-Object Name, Version -Unique
}

function Get-PackageLicenseInfo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageName,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$GlobalPackagesPath
    )

    $packageDir = Join-Path $GlobalPackagesPath ("{0}\{1}" -f $PackageName.ToLowerInvariant(), $Version)
    $nuspecPath = Get-ChildItem -Path $packageDir -Filter "*.nuspec" -File -ErrorAction SilentlyContinue | Select-Object -First 1

    $licenseType = ""
    $licenseValue = ""
    $licenseUrl = ""
    $projectUrl = ""
    $noticeFiles = New-Object System.Collections.Generic.List[string]

    if ($nuspecPath) {
        [xml]$xml = Get-Content $nuspecPath.FullName -Raw
        $metadata = $xml.package.metadata
        if ($metadata) {
            if ($metadata.license) {
                $licenseType = [string]$metadata.license.type
                if ($metadata.license."#text") {
                    $licenseValue = [string]$metadata.license."#text"
                }
                elseif ($metadata.license.InnerText) {
                    $licenseValue = [string]$metadata.license.InnerText
                }
            }

            if ($metadata.licenseUrl) {
                $licenseUrl = [string]$metadata.licenseUrl
            }

            if ($metadata.projectUrl) {
                $projectUrl = [string]$metadata.projectUrl
            }
        }
    }

    if ($licenseType -eq "file" -and -not [string]::IsNullOrWhiteSpace($licenseValue)) {
        $filePath = Join-Path $packageDir $licenseValue
        if (Test-Path $filePath) {
            $noticeFiles.Add($filePath)
        }
    }

    $candidateNames = @(
        "LICENSE.txt", "license.txt", "License.txt",
        "NOTICE.txt", "notice.txt", "Notice.txt",
        "THIRD-PARTY-NOTICES.txt", "ThirdPartyNotices.txt"
    )

    foreach ($candidate in $candidateNames) {
        $candidatePath = Join-Path $packageDir $candidate
        if (Test-Path $candidatePath) {
            $noticeFiles.Add($candidatePath)
        }
    }

    $uniqueNoticeFiles = $noticeFiles |
        Select-Object -Unique |
        Where-Object { Test-Path $_ }

    $noticeContents = New-Object System.Collections.Generic.List[object]
    foreach ($file in $uniqueNoticeFiles) {
        $noticeContents.Add([pscustomobject]@{
                FileName = Split-Path $file -Leaf
                RelativePath = $file.Substring($packageDir.Length).TrimStart('\')
                Content = Get-Content $file -Raw
            })
    }

    $scanTextParts = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($licenseValue)) { $scanTextParts.Add($licenseValue) }
    if (-not [string]::IsNullOrWhiteSpace($licenseUrl)) { $scanTextParts.Add($licenseUrl) }
    foreach ($notice in $noticeContents) { $scanTextParts.Add($notice.Content) }
    $scanText = ($scanTextParts -join "`n").ToUpperInvariant()

    $hasStrongCopyleft = $false
    $hasWeakCopyleft = $false

    if ($scanText -match "GNU AFFERO GENERAL PUBLIC LICENSE" -or
        $scanText -match "\bAGPL\b" -or
        ($scanText -match "GNU GENERAL PUBLIC LICENSE" -and $scanText -notmatch "GNU LESSER GENERAL PUBLIC LICENSE") -or
        $scanText -match "\bGPL-2\.0\b" -or
        $scanText -match "\bGPL-3\.0\b") {
        $hasStrongCopyleft = $true
    }

    if ($scanText -match "GNU LESSER GENERAL PUBLIC LICENSE" -or
        $scanText -match "\bLGPL\b") {
        $hasWeakCopyleft = $true
    }

    return [pscustomobject]@{
        Name = $PackageName
        Version = $Version
        PackageDirectory = $packageDir
        LicenseType = $licenseType
        LicenseValue = $licenseValue
        LicenseUrl = $licenseUrl
        ProjectUrl = $projectUrl
        NoticeFiles = $noticeContents
        StrongCopyleftDetected = $hasStrongCopyleft
        WeakCopyleftReferenceDetected = $hasWeakCopyleft
    }
}

function Get-LicenseDisplayName {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Package
    )

    if (-not [string]::IsNullOrWhiteSpace($Package.LicenseValue)) {
        return $Package.LicenseValue.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($Package.LicenseType)) {
        return $Package.LicenseType.Trim()
    }

    if ($Package.StrongCopyleftDetected) {
        return "GPL/AGPL reference detected in package notices"
    }

    if ($Package.WeakCopyleftReferenceDetected) {
        return "LGPL reference detected in package notices"
    }

    return "License not specified in package metadata"
}

function Test-PermissiveLicenseName {
    param(
        [string]$LicenseName
    )

    if ([string]::IsNullOrWhiteSpace($LicenseName)) {
        return $false
    }

    $normalized = $LicenseName.ToUpperInvariant()
    return $normalized -match "\bMIT\b" -or
           $normalized -match "\bBSD\b" -or
           $normalized -match "\bAPACHE\b" -or
           $normalized -match "\bISC\b" -or
           $normalized -match "\bZLIB\b" -or
           $normalized -match "\bBOOST\b" -or
           $normalized -match "\bBSL\b" -or
           $normalized -match "\bMPL\b"
}

function Get-IncludedNoticeFiles {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Package
    )

    $included = New-Object System.Collections.Generic.List[object]
    $licenseName = Get-LicenseDisplayName -Package $Package
    $isPermissiveLicense = Test-PermissiveLicenseName -LicenseName $licenseName

    foreach ($notice in $Package.NoticeFiles) {
        $fileName = [string]$notice.FileName
        $content = [string]$notice.Content
        if ([string]::IsNullOrWhiteSpace($content)) {
            continue
        }

        $upperName = $fileName.ToUpperInvariant()
        $isNoticeFile = $upperName -like "*NOTICE*"
        $isThirdPartyFile = $upperName -like "*THIRD*PARTY*"
        $isLicenseFile = $upperName -like "LICENSE*"
        $isMicrosoftEulaText = $content -match "(?im)^\s*MICROSOFT SOFTWARE LICENSE TERMS"
        $looksPermissiveText = $content -match "(?im)The MIT License|Redistribution and use in source and binary forms|Apache License|Permission is hereby granted"

        if ($isNoticeFile -or $isThirdPartyFile) {
            $included.Add($notice)
            continue
        }

        if ($isLicenseFile -and -not $isMicrosoftEulaText -and ($isPermissiveLicense -or $looksPermissiveText)) {
            $included.Add($notice)
            continue
        }
    }

    $deduped = New-Object System.Collections.Generic.List[object]
    $seen = @{}
    foreach ($item in $included) {
        $fingerprint = "{0}|{1}" -f $item.FileName, $item.Content
        if (-not $seen.ContainsKey($fingerprint)) {
            $seen[$fingerprint] = $true
            $deduped.Add($item)
        }
    }

    return $deduped.ToArray()
}

function Build-LicenseText {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IEnumerable]$Packages
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("LETTERIST THIRD-PARTY LICENSES")
    $lines.Add("Runtime dependency notices for third-party software and permissive licenses (MIT/BSD-style, etc.).")
    $lines.Add("")

    $packageList = @($Packages | Sort-Object Name, Version)

    $lines.Add("PACKAGE LICENSE SUMMARY")
    $lines.Add("-----------------------")
    foreach ($package in $packageList) {
        $licenseName = Get-LicenseDisplayName -Package $package
        $noticeFiles = Get-IncludedNoticeFiles -Package $package

        $lines.Add(("* {0} {1}" -f $package.Name, $package.Version))
        $lines.Add(("  License: {0}" -f $licenseName))
        if (-not [string]::IsNullOrWhiteSpace($package.LicenseUrl)) {
            $lines.Add(("  License URL: {0}" -f $package.LicenseUrl.Trim()))
        }
        if (-not [string]::IsNullOrWhiteSpace($package.ProjectUrl)) {
            $lines.Add(("  Project URL: {0}" -f $package.ProjectUrl.Trim()))
        }
        if ($noticeFiles.Count -gt 0) {
            $lines.Add(("  Included notice files: {0}" -f (($noticeFiles | ForEach-Object { $_.FileName }) -join ", ")))
        }
        else {
            $lines.Add("  Included notice files: none found in package contents.")
        }
        $lines.Add("")
    }

    $lines.Add("INCLUDED THIRD-PARTY NOTICE / LICENSE TEXTS")
    $lines.Add("-------------------------------------------")
    $lines.Add("")

    $hasIncludedText = $false
    foreach ($package in $packageList) {
        $noticeFiles = Get-IncludedNoticeFiles -Package $package
        foreach ($notice in $noticeFiles) {
            $hasIncludedText = $true
            $title = "{0} {1} :: {2}" -f $package.Name, $package.Version, $notice.FileName
            $lines.Add(("===== BEGIN {0} =====" -f $title))
            $lines.Add($notice.Content.Trim())
            $lines.Add(("===== END {0} =====" -f $title))
            $lines.Add("")
        }
    }

    if (-not $hasIncludedText) {
        $lines.Add("No third-party notice/license text files were found in runtime packages.")
    }

    return ($lines -join "`r`n")
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (-not (Test-Path $Source)) {
        throw "Source directory does not exist: $Source"
    }

    if (Test-Path $Destination) {
        Remove-Item -Path $Destination -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

function Resolve-DistributionSourceDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRootPath,
        [Parameter(Mandatory = $true)]
        [string]$ConfigurationName,
        [Parameter(Mandatory = $true)]
        [string]$FrameworkName
    )

    $candidates = @(
        (Join-Path $ProjectRootPath "src\Letterist\bin\x64\$ConfigurationName\$FrameworkName\win-x64"),
        (Join-Path $ProjectRootPath "src\Letterist\bin\x64\$ConfigurationName\$FrameworkName")
    )

    return $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Remove-DistributionFluff {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistributionPath
    )

    $filePatterns = @(
        "*.pdb",
        "*.build.appxrecipe"
    )

    foreach ($pattern in $filePatterns) {
        Get-ChildItem -Path $DistributionPath -Filter $pattern -File -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }

    $directoriesToRemove = @(
        (Join-Path $DistributionPath "publish")
    )

    foreach ($directory in $directoriesToRemove) {
        if (Test-Path $directory) {
            Remove-Item -Path $directory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

$msbuildPath = Resolve-MSBuildPath
Write-Host "MSBuild: $msbuildPath" -ForegroundColor Gray

if ($Clean -and (Test-Path $OutputRoot)) {
    Write-Host "Cleaning $OutputRoot" -ForegroundColor Yellow
    Remove-Item -Path $OutputRoot -Recurse -Force
}

dotnet restore $SolutionFile
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed."
}

Write-Host "Building Letterist ($Configuration, x64)..." -ForegroundColor Cyan
& $msbuildPath $SolutionFile -p:Configuration=$Configuration -p:Platform=x64 -p:Optimize=true -verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed."
}

$distributionSource = Resolve-DistributionSourceDirectory `
    -ProjectRootPath $ProjectRoot `
    -ConfigurationName $Configuration `
    -FrameworkName $TargetFramework

if (-not $distributionSource) {
    throw "Distribution source build output not found."
}

$distributionOutput = Join-Path $OutputRoot $DistributionName
Write-Host "Preparing distribution folder..." -ForegroundColor Cyan
Copy-DirectoryContents -Source $distributionSource -Destination $distributionOutput

if (-not (Test-Path $LicenseFile)) {
    throw "LICENSE.txt not found: $LicenseFile"
}

Copy-Item -Path $LicenseFile -Destination (Join-Path $distributionOutput "LICENSE.txt") -Force
if (Test-Path $ReadmeFile) {
    Copy-Item -Path $ReadmeFile -Destination (Join-Path $distributionOutput "README.md") -Force
}
Remove-DistributionFluff -DistributionPath $distributionOutput

$assetsPath = Join-Path $ProjectRoot "src\Letterist\obj\project.assets.json"
$runtimePackages = Get-RuntimePackages -AssetsPath $assetsPath
$globalPackagesPath = Get-GlobalPackagesPath

$licensePackages = New-Object System.Collections.Generic.List[object]
foreach ($runtimePackage in $runtimePackages) {
    $licensePackages.Add((Get-PackageLicenseInfo -PackageName $runtimePackage.Name -Version $runtimePackage.Version -GlobalPackagesPath $globalPackagesPath))
}

$strongCopyleft = $licensePackages | Where-Object { $_.StrongCopyleftDetected }
if ($strongCopyleft.Count -gt 0) {
    $names = ($strongCopyleft | ForEach-Object { "$($_.Name) $($_.Version)" }) -join ", "
    throw "Strong copyleft dependency detected in runtime package set: $names"
}

$licenseText = Build-LicenseText -Packages $licensePackages
$thirdPartyLicensePath = Join-Path $OutputRoot "THIRDPARTY-LICENSES.txt"
Set-Content -Path $thirdPartyLicensePath -Value $licenseText -Encoding UTF8
Set-Content -Path (Join-Path $distributionOutput "THIRDPARTY-LICENSES.txt") -Value $licenseText -Encoding UTF8

$licenseSummary = $licensePackages | Select-Object Name, Version, LicenseType, LicenseValue, LicenseUrl, ProjectUrl, StrongCopyleftDetected, WeakCopyleftReferenceDetected
$summaryPath = Join-Path $OutputRoot "license-summary.json"
$licenseSummary | ConvertTo-Json -Depth 4 | Set-Content -Path $summaryPath -Encoding UTF8

$zipPath = Join-Path $OutputRoot ($DistributionName + ".zip")
if ($CreateZip) {
    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $distributionOutput "*") -DestinationPath $zipPath -CompressionLevel Optimal
}

$weakCopyleftCount = ($licensePackages | Where-Object { $_.WeakCopyleftReferenceDetected }).Count
Write-Host ""
Write-Host "Release artifacts created:" -ForegroundColor Green
Write-Host "  Distribution:  $distributionOutput"
if ($CreateZip) {
    Write-Host "  Zip:           $zipPath"
}
Write-Host "  LICENSE.txt:   $LicenseFile"
Write-Host "  Third-Party:   $thirdPartyLicensePath"
Write-Host "  License JSON:  $summaryPath"
Write-Host ""
Write-Host "License check: PASS (no strong copyleft runtime dependency detected)." -ForegroundColor Green
if ($weakCopyleftCount -gt 0) {
    Write-Host "Weak copyleft reference count (LGPL mentions in upstream notices): $weakCopyleftCount" -ForegroundColor Yellow
}
