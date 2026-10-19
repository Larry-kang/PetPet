param (
    [int]$TestPort = 5215,
    [string]$RunId = "20261016-delivery-01",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$drillGuid = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$tempRoot = [System.IO.Path]::GetFullPath($env:TEMP)
$cleanDir = Join-Path $tempRoot "petpet-clean-$RunId-$drillGuid"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "docs\reviews\results\$RunId\clean-checkout-drill.json"
}
$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$startDemoLogFile = Join-Path $cleanDir "start-demo.log"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "PetPet.Modern Clean Checkout & First-Time Startup Drill" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Run ID: $RunId" -ForegroundColor Gray
Write-Host "Drill Directory: $cleanDir" -ForegroundColor Gray
Write-Host "Test Port: $TestPort" -ForegroundColor Gray

$results = [ordered]@{
    runId                      = $RunId
    drillId                    = $drillGuid
    cleanDirectory             = $cleanDir
    timestampUtc               = (Get-Date).ToUniversalTime().ToString("o")
    exportMethod               = "workingTreeRobocopy"
    workingTreeRobocopyExport  = $false
    firstOperationWasStartDemo = $false
    startDemoCommand           = ""
    firstStartupDurationMs     = 0
    healthResponses            = @{}
    seedStatistics             = $null
    processCleaned             = $false
    testSuccess                = $false
    testCount                  = 0
    testSummary                = $null
    exitCode                   = 1
    log                        = @()
}

function Log-Step($msg) {
    Write-Host "[Drill] $msg" -ForegroundColor Yellow
    $results.log += "[$(Get-Date -Format 'HH:mm:ss')] $msg"
}

$demoProc = $null
$drillSucceeded = $false

try {
    # -------------------------------------------------------------------------
    # 1. Clean Export: Robocopy working tree (excluding bin/obj/App_Data/.db/artifacts)
    # -------------------------------------------------------------------------
    Log-Step "Exporting working tree candidate source code via robocopy (excluding bin/obj/App_Data/.db/artifacts)..."
    Log-Step "Note: This verifies a clean working tree export without build caches or pre-existing databases; it does not claim to verify git archive or remote fresh clone."
    New-Item -ItemType Directory -Path $cleanDir -Force | Out-Null

    $roboExit = (Start-Process -FilePath "robocopy" -ArgumentList "`"$repoRoot`" `"$cleanDir`" /E /XD bin obj .git .vs .artifacts_build App_Data node_modules TestResults /XF *.db *.tar *.log *.tmp" -Wait -PassThru -NoNewWindow).ExitCode
    if ($roboExit -ge 8) {
        throw "Robocopy failed with exit code $roboExit"
    }

    $results.workingTreeRobocopyExport = $true
    Log-Step "Working tree export completed via robocopy. Verifying zero pre-existing .db, bin, obj, or artifacts..."

    $dbFiles = Get-ChildItem -Path $cleanDir -Recurse -Filter "*.db" -ErrorAction SilentlyContinue
    if ($dbFiles.Count -gt 0) {
        throw "Clean directory must not contain pre-existing databases!"
    }
    $binDirs = Get-ChildItem -Path $cleanDir -Recurse -Directory -Filter "bin" -ErrorAction SilentlyContinue
    if ($binDirs.Count -gt 0) {
        throw "Clean directory must not contain pre-existing bin directories!"
    }
    $objDirs = Get-ChildItem -Path $cleanDir -Recurse -Directory -Filter "obj" -ErrorAction SilentlyContinue
    if ($objDirs.Count -gt 0) {
        throw "Clean directory must not contain pre-existing obj directories!"
    }

    # -------------------------------------------------------------------------
    # 2. First Operation: Execute start-demo.ps1 as documented in README
    # -------------------------------------------------------------------------
    $startDemoScript = Join-Path $cleanDir "start-demo.ps1"
    if (-not (Test-Path $startDemoScript)) {
        throw "start-demo.ps1 not found in clean export!"
    }

    $demoUrl = "http://127.0.0.1:$TestPort"
    $readyUrl = "$demoUrl/health/ready"
    $liveUrl = "$demoUrl/health/live"
    $homeUrl = "$demoUrl/"

    $startDemoCmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -Command `"& '$startDemoScript' -Port $TestPort -NoBrowser *>&1 | Tee-Object -FilePath '$startDemoLogFile'`""
    $results.startDemoCommand = $startDemoCmd
    $results.firstOperationWasStartDemo = $true

    Log-Step "Executing documented entry point: start-demo.ps1 (Port $TestPort)..."
    Log-Step "Command: $startDemoCmd"

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = "powershell.exe"
    $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command `"& '$startDemoScript' -Port $TestPort -NoBrowser *>&1 | Tee-Object -FilePath '$startDemoLogFile'`""
    $psi.WorkingDirectory = $cleanDir
    $psi.UseShellExecute = $false

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $demoProc = [System.Diagnostics.Process]::new()
    $demoProc.StartInfo = $psi
    $demoProc.Start() | Out-Null
    $results["startDemoPid"] = $demoProc.Id

    Log-Step "Process tree created (PID $($demoProc.Id)). Waiting for first-time build, migrations, and seeding..."

    $isReady = $false
    while ($sw.Elapsed.TotalSeconds -lt 90) {
        if ($demoProc.HasExited) {
            $logTail = if (Test-Path $startDemoLogFile) { Get-Content $startDemoLogFile -Tail 20 } else { "No log file found" }
            throw "start-demo.ps1 process exited prematurely with code $($demoProc.ExitCode). Log tail:`n$($logTail -join "`n")"
        }

        try {
            $req = [System.Net.HttpWebRequest]::Create($readyUrl)
            $req.Timeout = 1000
            $resp = $req.GetResponse()
            if ([int]$resp.StatusCode -eq 200) {
                $isReady = $true
                $stream = $resp.GetResponseStream()
                $reader = [System.IO.StreamReader]::new($stream)
                $body = $reader.ReadToEnd()
                $resp.Close()
                $results.healthResponses["ready"] = @{ statusCode = 200; body = $body }
                break
            }
            $resp.Close()
        } catch {
            Start-Sleep -Milliseconds 300
        }
    }

    if (-not $isReady) {
        throw "start-demo failed to become ready within timeout (90s)!"
    }

    $results.firstStartupDurationMs = $sw.ElapsedMilliseconds
    Log-Step "[PASS] start-demo first-time start successful! Ready in $($results.firstStartupDurationMs) ms, /health/ready returned 200 OK."

    # Verify /health/live and Home page /
    try {
        $liveReq = [System.Net.HttpWebRequest]::Create($liveUrl)
        $liveReq.Timeout = 2000
        $liveResp = $liveReq.GetResponse()
        $stream = $liveResp.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        $liveBody = $reader.ReadToEnd()
        $liveStatusCode = [int]$liveResp.StatusCode
        $liveResp.Close()
        if ($liveStatusCode -ne 200) {
            throw "/health/live responded with HTTP $liveStatusCode (expected 200 OK)"
        }
        $results.healthResponses["live"] = @{ statusCode = $liveStatusCode; body = $liveBody }
        Log-Step "[PASS] /health/live check succeeded (200 OK)."
    } catch {
        throw "/health/live check failed: $_"
    }

    try {
        $homeReq = [System.Net.HttpWebRequest]::Create($homeUrl)
        $homeReq.Timeout = 2000
        $homeResp = $homeReq.GetResponse()
        $homeStatusCode = [int]$homeResp.StatusCode
        $homeResp.Close()
        if ($homeStatusCode -ne 200) {
            throw "Home page / responded with HTTP $homeStatusCode (expected 200 OK)"
        }
        $results.healthResponses["home"] = @{ statusCode = $homeStatusCode }
        Log-Step "[PASS] Home page / check succeeded (200 OK)."
    } catch {
        throw "Home page / check failed: $_"
    }

    # -------------------------------------------------------------------------
    # 3. Verify SQLite Database & Deterministic Seed Statistics
    # -------------------------------------------------------------------------
    Log-Step "Inspecting generated SQLite database and seed statistics..."
    $sqliteDbPath = Join-Path $cleanDir "PetPet.Modern\src\PetPet.Web\App_Data\demo\petpet-demo.db"
    if (-not (Test-Path $sqliteDbPath)) {
        throw "Expected SQLite database not found at $sqliteDbPath!"
    }

    $verifierProj = Join-Path $cleanDir "tools\seed-verifier\seed-verifier.csproj"
    $verifierArtifacts = Join-Path $cleanDir ".artifacts_verifier"

    # Build seed-verifier
    $buildProc = Start-Process -FilePath "dotnet" -ArgumentList "build `"$verifierProj`" -c Release -p:ArtifactsPath=`"$verifierArtifacts`"" -WorkingDirectory $cleanDir -Wait -PassThru -NoNewWindow
    if ($buildProc.ExitCode -ne 0) {
        throw "Building seed-verifier failed with exit code $($buildProc.ExitCode)"
    }

    $verifierDll = Join-Path $verifierArtifacts "bin\seed-verifier\release\seed-verifier.dll"
    if (-not (Test-Path $verifierDll)) {
        throw "Compiled seed-verifier DLL not found at $verifierDll"
    }

    $vProc = Start-Process -FilePath "dotnet" -ArgumentList "`"$verifierDll`" `"$sqliteDbPath`"" -WorkingDirectory $cleanDir -Wait -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $cleanDir "seed-verify.json") -RedirectStandardError (Join-Path $cleanDir "seed-verify.err")

    if ($vProc.ExitCode -ne 0) {
        $errText = Get-Content (Join-Path $cleanDir "seed-verify.err") -Raw -ErrorAction SilentlyContinue
        throw "seed-verifier failed with exit code $($vProc.ExitCode): $errText"
    }

    $seedJson = Get-Content (Join-Path $cleanDir "seed-verify.json") -Raw | ConvertFrom-Json
    if (-not $seedJson.passed) {
        throw "Seed verification assertion failed! See report for details."
    }

    $results.seedStatistics = $seedJson.statistics
    Log-Step "[PASS] Seed statistics strictly verified against specification:"
    Log-Step "   - Members Total: $($seedJson.statistics.members.total) (Spec requirement >= 100)"
    Log-Step "   - Pets Total: $($seedJson.statistics.pets.total) (Range per member: $($seedJson.statistics.pets.minPerMember) to $($seedJson.statistics.pets.maxPerMember) pets)"
    Log-Step "   - Posts Total: $($seedJson.statistics.posts.total) (Spec requirement 1,200)"
    Log-Step "   - Comments Total: $($seedJson.statistics.comments.total) (Spec requirement 2,400)"
    Log-Step "   - Likes Total: $($seedJson.statistics.likes.total)"
    Log-Step "   - Friends Total: $($seedJson.statistics.friends.total) (Accepted: $($seedJson.statistics.friends.accepted), Pending: $($seedJson.statistics.friends.pending))"
    Log-Step "   - Seed Record Fingerprint: $($seedJson.statistics.seedRecord.fingerprint)"

    # -------------------------------------------------------------------------
    # 4. Stop Demo Process Tree & Confirm Port Release
    # -------------------------------------------------------------------------
    Log-Step "Stopping start-demo process tree (PID $($demoProc.Id))..."
    Start-Process -FilePath "taskkill" -ArgumentList "/PID $($demoProc.Id) /T /F" -Wait -NoNewWindow -ErrorAction SilentlyContinue | Out-Null
    Start-Sleep -Seconds 2

    $portActive = $false
    try {
        $tcp = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Parse("127.0.0.1"), $TestPort)
        $tcp.Start()
        $tcp.Stop()
    } catch {
        $portActive = $true
    }

    if ($portActive) {
        throw "Port $TestPort is still bound after process stop!"
    }
    $results.processCleaned = $true
    Log-Step "[PASS] Process tree terminated. Port $TestPort confirmed completely free."

    # -------------------------------------------------------------------------
    # 5. Independent Test Suite (Executed AFTER First-Time Start Verification)
    # -------------------------------------------------------------------------
    Log-Step "First-time startup verified. Executing independent tests (dotnet test with TRX logging)..."
    $testArtifacts = Join-Path $cleanDir ".artifacts_test"
    $trxDir = Join-Path $cleanDir "TestResults"
    $trxFileName = "clean-test-$RunId.trx"
    $testArgs = "test PetPet.Modern\PetPet.Modern.sln -c Release -p:ArtifactsPath=`"$testArtifacts`" -p:UseSharedCompilation=false -nodeReuse:false --logger `"trx;LogFileName=$trxFileName`" --results-directory `"$trxDir`""
    $testProc = Start-Process -FilePath "dotnet" -ArgumentList $testArgs -WorkingDirectory $cleanDir -Wait -PassThru -NoNewWindow

    $trxFilePath = Join-Path $trxDir $trxFileName
    if (-not (Test-Path $trxFilePath)) {
        $foundTrx = Get-ChildItem -Path $trxDir -Filter "*.trx" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($foundTrx) {
            $trxFilePath = $foundTrx.FullName
        } else {
            throw "Test results TRX file was not generated at $trxFilePath!"
        }
    }

    # Copy TRX to outputDir
    $preservedTrxPath = Join-Path $outputDir "clean-test-$RunId.trx"
    Copy-Item -Path $trxFilePath -Destination $preservedTrxPath -Force -ErrorAction SilentlyContinue
    Log-Step "Test TRX report preserved to: $preservedTrxPath"

    # Parse TRX XML
    [xml]$trxXml = Get-Content -Path $trxFilePath -Raw
    $counters = $trxXml.TestRun.ResultSummary.Counters
    $totalCount = [int]$counters.total
    $passedCount = [int]$counters.passed
    $failedCount = [int]$counters.failed
    $skippedCount = [int]$counters.notExecuted + [int]$counters.notRunnable
    $outcome = $trxXml.TestRun.ResultSummary.outcome

    $results.testSummary = [ordered]@{
        total   = $totalCount
        passed  = $passedCount
        failed  = $failedCount
        skipped = $skippedCount
        outcome = $outcome
        trxPath = $preservedTrxPath
    }
    $results.testSuccess = ($outcome -eq "Completed" -or $outcome -eq "Passed") -and ($failedCount -eq 0) -and ($testProc.ExitCode -eq 0)
    $results.testCount = $totalCount

    Log-Step "[PASS] Independent unit/integration tests parsed from TRX: $totalCount total, $passedCount passed, $failedCount failed, $skippedCount skipped (Outcome: $outcome)."

    if (-not $results.testSuccess) {
        throw "Clean tests did not succeed: $failedCount failed, exit code $($testProc.ExitCode)"
    }

    $results.exitCode = 0
    $drillSucceeded = $true
    Write-Host "`nAll Clean Checkout Drill gates PASSED successfully!" -ForegroundColor Green
}
catch {
    Write-Host "`nClean Checkout Drill FAILED: $_" -ForegroundColor Red
    $results["error"] = $_.ToString()
    $results.exitCode = 1
}
finally {
    # -------------------------------------------------------------------------
    # 6. Finally: Clean processes, verify port release, safe deletion, preserve logs
    # -------------------------------------------------------------------------
    if ($demoProc -ne $null -and -not $demoProc.HasExited) {
        Log-Step "[Finally] Ensuring process cleanup (PID $($demoProc.Id))..."
        Start-Process -FilePath "taskkill" -ArgumentList "/PID $($demoProc.Id) /T /F" -Wait -NoNewWindow -ErrorAction SilentlyContinue | Out-Null
    }

    # Copy start-demo.log to results directory
    if (Test-Path $startDemoLogFile) {
        $targetLog = Join-Path $outputDir "start-demo-$RunId.log"
        Copy-Item -Path $startDemoLogFile -Destination $targetLog -Force -ErrorAction SilentlyContinue
        Log-Step "First-start log preserved to: $targetLog"
    }

    # Ensure any generated TRX is preserved to results directory even if an error occurred later
    $trxCandidates = Get-ChildItem -Path (Join-Path $cleanDir "TestResults") -Filter "*.trx" -ErrorAction SilentlyContinue
    if ($trxCandidates -and -not (Test-Path (Join-Path $outputDir "clean-test-$RunId.trx"))) {
        Copy-Item -Path $trxCandidates[0].FullName -Destination (Join-Path $outputDir "clean-test-$RunId.trx") -Force -ErrorAction SilentlyContinue
        Log-Step "Preserved test TRX report on exit."
    }

    # Verify directory is strictly inside TEMP root before recursive deletion
    $cleanDirResolved = [System.IO.Path]::GetFullPath($cleanDir)
    $tempRootResolved = [System.IO.Path]::GetFullPath($tempRoot)
    $isSafeToDelete = $cleanDirResolved.StartsWith($tempRootResolved, [System.StringComparison]::OrdinalIgnoreCase) -and ($cleanDirResolved.Length -gt $tempRootResolved.Length)

    if ($isSafeToDelete) {
        if ($drillSucceeded) {
            Log-Step "Drill succeeded. Cleaning up temporary directory: $cleanDir..."
            Remove-Item -Path $cleanDir -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Log-Step "Warning: Drill did not succeed. Preserving diagnostic directory: $cleanDir"
        }
    } else {
        Log-Step "Warning: Safety check failed ($cleanDirResolved not inside $tempRootResolved). Skipping recursive deletion."
    }
}

$results | ConvertTo-Json -Depth 6 | Out-File -FilePath $OutputPath -Encoding utf8
Write-Host "Drill results saved to: $OutputPath" -ForegroundColor Cyan
exit $results.exitCode
