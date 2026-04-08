param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [int]$Port = 9221,
    [int]$StartupWaitSeconds = 5,
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ScreenshotDir = Join-Path $ProjectRoot "test-output"

if (-not (Test-Path $ScreenshotDir)) {
    New-Item -ItemType Directory -Path $ScreenshotDir | Out-Null
}

Write-Host "=== Letterist Automation Test ===" -ForegroundColor Cyan

Write-Host "`nBuilding..." -ForegroundColor Yellow
& "$ProjectRoot\build.ps1" -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$ExePath = Join-Path $ProjectRoot "src\Letterist\bin\x64\$Configuration\net9.0-windows10.0.22621.0\Letterist.exe"

Write-Host "`nStarting Letterist in automation mode on port $Port..." -ForegroundColor Yellow
$process = Start-Process -FilePath $ExePath -ArgumentList "--automation", "--port", $Port -PassThru

try {
    Write-Host "Waiting $StartupWaitSeconds seconds for startup..." -ForegroundColor Yellow
    Start-Sleep -Seconds $StartupWaitSeconds

    if ($process.HasExited) {
        Write-Host "Application exited unexpectedly with code: $($process.ExitCode)" -ForegroundColor Red
        exit 1
    }

    $BaseUrl = "http://localhost:$Port"
    $TestsPassed = 0
    $TestsFailed = 0
    $advancedBalloonId = $null

    Write-Host "`n[Test 1] GET /state" -ForegroundColor Cyan
    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/state" -Method Get -TimeoutSec 10
        if ($response.success -eq $true) {
            Write-Host "  PASS: State endpoint returned success" -ForegroundColor Green
            Write-Host "  Data: documentLoaded=$($response.data.documentLoaded)" -ForegroundColor Gray
            $TestsPassed++
        } else {
            Write-Host "  FAIL: State endpoint returned success=false" -ForegroundColor Red
            $TestsFailed++
        }
    } catch {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $TestsFailed++
    }

    Write-Host "`n[Test 2] GET /screenshot" -ForegroundColor Cyan
    try {
        $screenshotPath = Join-Path $ScreenshotDir "test-screenshot.png"
        Invoke-WebRequest -Uri "$BaseUrl/screenshot" -OutFile $screenshotPath -TimeoutSec 10

        if (Test-Path $screenshotPath) {
            $fileInfo = Get-Item $screenshotPath
            if ($fileInfo.Length -gt 0) {
                Write-Host "  PASS: Screenshot saved to $screenshotPath ($($fileInfo.Length) bytes)" -ForegroundColor Green
                $TestsPassed++
            } else {
                Write-Host "  FAIL: Screenshot file is empty" -ForegroundColor Red
                $TestsFailed++
            }
        } else {
            Write-Host "  FAIL: Screenshot file not created" -ForegroundColor Red
            $TestsFailed++
        }
    } catch {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $TestsFailed++
    }

    Write-Host "`n[Test 3] POST /commands" -ForegroundColor Cyan
    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/commands" -Method Post -Body "[]" -ContentType "application/json" -TimeoutSec 10
        if ($response.success -eq $true) {
            Write-Host "  PASS: Commands endpoint responded" -ForegroundColor Green
            $TestsPassed++
        } else {
            Write-Host "  FAIL: Commands endpoint returned success=false" -ForegroundColor Red
            $TestsFailed++
        }
    } catch {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $TestsFailed++
    }

    Write-Host "`n[Test 4] POST /commands (transaction batch)" -ForegroundColor Cyan
    try {
        $stateBefore = Invoke-RestMethod -Uri "$BaseUrl/state" -Method Get -TimeoutSec 10
        $beforeCount = @($stateBefore.data.balloons).Count
        $layerId = $stateBefore.data.activeLayerId

        $payload = @{
            transaction = $true
            commands = @(
                @{
                    type = "CreateBalloon"
                    parameters = @{
                        layerId = $layerId
                        x = 120
                        y = 140
                        text = "Batch A"
                    }
                },
                @{
                    type = "CreateBalloon"
                    parameters = @{
                        layerId = $layerId
                        x = 260
                        y = 160
                        text = "Batch B"
                    }
                }
            )
        }

        $body = $payload | ConvertTo-Json -Depth 6
        $response = Invoke-RestMethod -Uri "$BaseUrl/commands" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 10

        if ($response.success -ne $true -or $response.data.executed -ne 2) {
            Write-Host "  FAIL: Batch command submission failed" -ForegroundColor Red
            $TestsFailed++
        } else {
            $stateAfter = Invoke-RestMethod -Uri "$BaseUrl/state" -Method Get -TimeoutSec 10
            $afterCount = @($stateAfter.data.balloons).Count
            if ($afterCount -eq ($beforeCount + 2)) {
                Write-Host "  PASS: Batch command submission created 2 balloons" -ForegroundColor Green
                $TestsPassed++
            } else {
                Write-Host "  FAIL: Expected balloon count $($beforeCount + 2), got $afterCount" -ForegroundColor Red
                $TestsFailed++
            }
        }
    } catch {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $TestsFailed++
    }

    Write-Host "`n[Test 5] POST /commands (style updates)" -ForegroundColor Cyan
    try {
        $state = Invoke-RestMethod -Uri "$BaseUrl/state" -Method Get -TimeoutSec 10
        $layerId = $state.data.activeLayerId

        $createPayload = @{
            commands = @(
                @{
                    type = "CreateBalloon"
                    parameters = @{
                        layerId = $layerId
                        x = 180
                        y = 240
                        text = "Styled"
                    }
                }
            )
        }

        $createBody = $createPayload | ConvertTo-Json -Depth 6
        $createResponse = Invoke-RestMethod -Uri "$BaseUrl/commands" -Method Post -Body $createBody -ContentType "application/json" -TimeoutSec 10

        if ($createResponse.success -ne $true -or @($createResponse.data.createdIds).Count -lt 1) {
            Write-Host "  FAIL: Could not create balloon for style test" -ForegroundColor Red
            $TestsFailed++
        } else {
            $balloonId = $createResponse.data.createdIds[0]
            $stylePayload = @{
                transaction = $true
                commands = @(
                    @{
                        type = "SetBalloonStyle"
                        parameters = @{
                            balloonId = $balloonId
                            style = @{
                                fillColor = @{ r = 255; g = 0; b = 0; a = 255 }
                                strokeColor = @{ r = 0; g = 128; b = 0; a = 255 }
                                strokeWidth = 5
                                cornerRadius = 18
                                paddingLeft = 10
                                paddingTop = 9
                                paddingRight = 10
                                paddingBottom = 9
                            }
                        }
                    },
                    @{
                        type = "SetTextStyle"
                        parameters = @{
                            balloonId = $balloonId
                            style = @{
                                fontFamily = "Segoe UI"
                                fontSize = 18
                                bold = $true
                                italic = $false
                                alignment = "center"
                                fitMode = "shrinkToFit"
                                overflowMode = "clip"
                            }
                        }
                    }
                )
            }

            $styleBody = $stylePayload | ConvertTo-Json -Depth 8
            $styleResponse = Invoke-RestMethod -Uri "$BaseUrl/commands" -Method Post -Body $styleBody -ContentType "application/json" -TimeoutSec 10

            if ($styleResponse.success -ne $true -or $styleResponse.data.executed -ne 2) {
                Write-Host "  FAIL: Style commands did not execute" -ForegroundColor Red
                $TestsFailed++
            } else {
                $balloonState = Invoke-RestMethod -Uri "$BaseUrl/state/balloon/$balloonId" -Method Get -TimeoutSec 10
                if ($balloonState.success -ne $true) {
                    Write-Host "  FAIL: Could not load balloon state after style update" -ForegroundColor Red
                    $TestsFailed++
                } else {
                    $styleOk = $balloonState.data.style.fillColor -eq "#FF0000" -and $balloonState.data.style.strokeColor -eq "#008000" -and $balloonState.data.style.strokeWidth -eq 5
                    $textOk = $balloonState.data.textStyle.fontFamily -eq "Segoe UI" -and $balloonState.data.textStyle.bold -eq $true -and $balloonState.data.textStyle.alignment -eq "Center" -and $balloonState.data.textStyle.fitMode -eq "ShrinkToFit" -and $balloonState.data.textStyle.overflowMode -eq "Clip"
                    if ($styleOk -and $textOk) {
                        Write-Host "  PASS: Style commands applied and verified" -ForegroundColor Green
                        $TestsPassed++
                    } else {
                        Write-Host "  FAIL: Style verification failed" -ForegroundColor Red
                        Write-Host "  Style: fillColor=$($balloonState.data.style.fillColor), strokeWidth=$($balloonState.data.style.strokeWidth)" -ForegroundColor Gray
                        Write-Host "  Text: fontFamily=$($balloonState.data.textStyle.fontFamily), bold=$($balloonState.data.textStyle.bold), alignment=$($balloonState.data.textStyle.alignment), fitMode=$($balloonState.data.textStyle.fitMode)" -ForegroundColor Gray
                        $TestsFailed++
                    }
                }
            }
        }
    } catch {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $TestsFailed++
    }

    Write-Host "`n[Test 6] POST /commands (text-only + advanced text style)" -ForegroundColor Cyan
    try {
        $state = Invoke-RestMethod -Uri "$BaseUrl/state" -Method Get -TimeoutSec 10
        $layerId = $state.data.activeLayerId

        $createPayload = @{
            commands = @(
                @{
                    type = "CreateBalloon"
                    parameters = @{
                        layerId = $layerId
                        x = 420
                        y = 300
                        text = "NO BALLOON"
                        shape = "None"
                    }
                }
            )
        }
        $createBody = $createPayload | ConvertTo-Json -Depth 8
        $createResponse = Invoke-RestMethod -Uri "$BaseUrl/commands" -Method Post -Body $createBody -ContentType "application/json" -TimeoutSec 10

        if ($createResponse.success -ne $true -or @($createResponse.data.createdIds).Count -lt 1) {
            Write-Host "  FAIL: Could not create text-only balloon" -ForegroundColor Red
            $TestsFailed++
        } else {
            $advancedBalloonId = $createResponse.data.createdIds[0]
            $stylePayload = @{
                commands = @(
                    @{
                        type = "SetTextStyle"
                        parameters = @{
                            balloonId = $advancedBalloonId
                            style = @{
                                fillType = "linear"
                                fillSecondaryColor = @{ r = 240; g = 170; b = 40; a = 255 }
                                fillAngle = 26
                                outlineColor = @{ r = 20; g = 20; b = 20; a = 255 }
                                outlineWidth = 4
                                additionalStrokes = @(
                                    @{ color = @{ r = 255; g = 230; b = 120; a = 255 }; width = 4 },
                                    @{ color = @{ r = 120; g = 18; b = 24; a = 255 }; width = 6 }
                                )
                                shadows = @(
                                    @{ color = @{ r = 0; g = 0; b = 0; a = 255 }; offsetX = 4; offsetY = 3; blur = 2; opacity = 0.45 }
                                )
                                outerGlowEnabled = $true
                                outerGlowColor = @{ r = 255; g = 220; b = 90; a = 255 }
                                outerGlowSize = 7
                                outerGlowOpacity = 0.6
                                motionBlurEnabled = $true
                                motionBlurDistance = 6
                                motionBlurAngle = 18
                                ragMode = "tight"
                                hyphenationLocale = "en-US"
                                justificationStrength = 78
                                hyphenationLevel = 31
                                fillHeight = $true
                                warpPreset = "wave"
                                warpIntensity = 0.55
                                warpHorizontalDistortion = 0.2
                                warpVerticalDistortion = -0.15
                                warpMesh = @{
                                    topLeftOffset = @{ x = -0.1; y = 0.04 }
                                    topRightOffset = @{ x = 0.09; y = -0.03 }
                                    bottomRightOffset = @{ x = 0.07; y = 0.03 }
                                    bottomLeftOffset = @{ x = -0.06; y = -0.02 }
                                }
                            }
                        }
                    }
                )
            }
            $styleBody = $stylePayload | ConvertTo-Json -Depth 10
            $styleResponse = Invoke-RestMethod -Uri "$BaseUrl/commands" -Method Post -Body $styleBody -ContentType "application/json" -TimeoutSec 10

            if ($styleResponse.success -ne $true) {
                Write-Host "  FAIL: Could not apply advanced text style" -ForegroundColor Red
                $TestsFailed++
            } else {
                $balloonState = Invoke-RestMethod -Uri "$BaseUrl/state/balloon/$advancedBalloonId" -Method Get -TimeoutSec 10
                if ($balloonState.success -ne $true) {
                    Write-Host "  FAIL: Could not load advanced balloon state" -ForegroundColor Red
                    $TestsFailed++
                } else {
                    $shapeOk = $balloonState.data.shape -eq "None"
                    $fillOk = $balloonState.data.textStyle.fillType -eq "Linear"
                    $strokesOk = @($balloonState.data.textStyle.additionalStrokes).Count -eq 2
                    $effectsOk = $balloonState.data.textStyle.outerGlowEnabled -eq $true -and $balloonState.data.textStyle.motionBlurEnabled -eq $true
                    $warpOk = $balloonState.data.textStyle.warpPreset -eq "Wave" -and ([Math]::Abs([double]$balloonState.data.textStyle.warpIntensity - 0.55) -lt 0.001)
                    if ($shapeOk -and $fillOk -and $strokesOk -and $effectsOk -and $warpOk) {
                        Write-Host "  PASS: Text-only balloon and advanced text style verified" -ForegroundColor Green
                        $TestsPassed++
                    } else {
                        Write-Host "  FAIL: Advanced text style verification failed" -ForegroundColor Red
                        Write-Host "  Shape=$($balloonState.data.shape), FillType=$($balloonState.data.textStyle.fillType), WarpPreset=$($balloonState.data.textStyle.warpPreset)" -ForegroundColor Gray
                        $TestsFailed++
                    }
                }
            }
        }
    } catch {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $TestsFailed++
    }

    Write-Host "`n[Test 7] POST /undo and /redo (advanced text style)" -ForegroundColor Cyan
    try {
        if (-not $advancedBalloonId) {
            Write-Host "  FAIL: No advanced balloon id available from Test 6" -ForegroundColor Red
            $TestsFailed++
        } else {
            $undoResponse = Invoke-RestMethod -Uri "$BaseUrl/undo" -Method Post -TimeoutSec 10
            if ($undoResponse.success -ne $true) {
                Write-Host "  FAIL: Undo endpoint failed" -ForegroundColor Red
                $TestsFailed++
            } else {
                $afterUndo = Invoke-RestMethod -Uri "$BaseUrl/state/balloon/$advancedBalloonId" -Method Get -TimeoutSec 10
                $undoOk = $afterUndo.success -eq $true -and $afterUndo.data.textStyle.fillType -eq "Solid" -and $afterUndo.data.textStyle.outerGlowEnabled -eq $false -and $afterUndo.data.textStyle.warpPreset -eq "None"

                $redoResponse = Invoke-RestMethod -Uri "$BaseUrl/redo" -Method Post -TimeoutSec 10
                $afterRedo = Invoke-RestMethod -Uri "$BaseUrl/state/balloon/$advancedBalloonId" -Method Get -TimeoutSec 10
                $redoOk = $redoResponse.success -eq $true -and $afterRedo.success -eq $true -and $afterRedo.data.textStyle.fillType -eq "Linear" -and $afterRedo.data.textStyle.outerGlowEnabled -eq $true -and $afterRedo.data.textStyle.warpPreset -eq "Wave"

                if ($undoOk -and $redoOk) {
                    Write-Host "  PASS: Undo/redo restored advanced text style correctly" -ForegroundColor Green
                    $TestsPassed++
                } else {
                    Write-Host "  FAIL: Undo/redo verification failed" -ForegroundColor Red
                    Write-Host "  AfterUndo fillType=$($afterUndo.data.textStyle.fillType), warpPreset=$($afterUndo.data.textStyle.warpPreset)" -ForegroundColor Gray
                    Write-Host "  AfterRedo fillType=$($afterRedo.data.textStyle.fillType), warpPreset=$($afterRedo.data.textStyle.warpPreset)" -ForegroundColor Gray
                    $TestsFailed++
                }
            }
        }
    } catch {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $TestsFailed++
    }

    Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
    Write-Host "Passed: $TestsPassed" -ForegroundColor Green
    Write-Host "Failed: $TestsFailed" -ForegroundColor $(if ($TestsFailed -gt 0) { "Red" } else { "Green" })

    if ($KeepRunning) {
        Write-Host "`nApplication kept running. Press Ctrl+C to stop." -ForegroundColor Yellow
        Wait-Process -Id $process.Id
    }

    if ($TestsFailed -gt 0) {
        exit 1
    }
} finally {
    if (-not $KeepRunning -and -not $process.HasExited) {
        Write-Host "`nStopping application..." -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`nAll tests passed!" -ForegroundColor Green
