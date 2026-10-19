[CmdletBinding()]
param (
    [ValidateRange(1024, 65535)]
    [int]$Port = 5213,

    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "🐾 PetPet.Modern 本機展示版啟動程序 (Demo Mode)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. 檢查 .NET 8 SDK
Write-Host "檢查 .NET 8 SDK 環境..." -ForegroundColor Gray
$sdks = & dotnet --list-sdks 2>$null
$hasNet8 = $false
foreach ($sdk in $sdks) {
    if ($sdk -match "^8\.") {
        $hasNet8 = $true
        break
    }
}

if (-not $hasNet8) {
    Write-Host "❌ 錯誤：未偵測到已安裝的 .NET 8 SDK。" -ForegroundColor Red
    Write-Host "本系統需要 .NET 8 SDK (8.0.x) 才能執行。" -ForegroundColor Yellow
    Write-Host "請前往微軟官方網站下載並安裝 .NET 8 SDK：" -ForegroundColor Yellow
    Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
    exit 1
}

# 2. 檢查連接埠是否被佔用
Write-Host "檢查連接埠 $Port 是否可用..." -ForegroundColor Gray
$isPortOccupied = $false
try {
    $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Parse("127.0.0.1"), $Port)
    $tcpListener.Start()
    $tcpListener.Stop()
}
catch {
    $isPortOccupied = $true
}

if ($isPortOccupied) {
    Write-Host "❌ 錯誤：連接埠 $Port 已被其他程序佔用。" -ForegroundColor Red
    Write-Host "請關閉佔用該連接埠的程序，或使用 -Port 參數指定其他連接埠：" -ForegroundColor Yellow
    Write-Host "例如: ./start-demo.ps1 -Port 5214" -ForegroundColor White
    exit 1
}

# 3. 設定程序範圍環境變數 (不影響主機或全域設定)
$projectDir = Join-Path $PSScriptRoot "PetPet.Modern\src\PetPet.Web"
$originalEnv = $env:ASPNETCORE_ENVIRONMENT
$originalUrls = $env:ASPNETCORE_URLS
$originalDemo = $env:Demo__Enabled
$originalDb = $env:Database__Provider

$demoUrl = "http://127.0.0.1:$Port"
$readyUrl = "$demoUrl/health/ready"

Write-Host "正在啟動 PetPet.Modern Demo 服務 ($demoUrl)..." -ForegroundColor Green

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = "dotnet"
$psi.Arguments = "run --project `"$projectDir`" --no-launch-profile"
$psi.WorkingDirectory = $PSScriptRoot
$psi.UseShellExecute = $false
$psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Demo"
$psi.EnvironmentVariables["Demo__Enabled"] = "true"
$psi.EnvironmentVariables["Database__Provider"] = "Sqlite"
$psi.EnvironmentVariables["ASPNETCORE_URLS"] = $demoUrl

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $psi

try {
    $started = $process.Start()
    if (-not $started) {
        Write-Host "❌ 無法啟動 dotnet 程序。" -ForegroundColor Red
        exit 1
    }

    Write-Host "等候資料庫 Migration 與展示資料 Seed 初始化完成 (最長 60 秒)..." -ForegroundColor Yellow
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $isReady = $false

    while ($stopwatch.Elapsed.TotalSeconds -lt 60) {
        if ($process.HasExited) {
            Write-Host "❌ 子程序異常退出，結束代碼: $($process.ExitCode)" -ForegroundColor Red
            exit $process.ExitCode
        }

        try {
            $req = [System.Net.WebRequest]::Create($readyUrl)
            $req.Timeout = 1000
            $resp = $req.GetResponse()
            $httpResp = [System.Net.HttpWebResponse]$resp
            if ($httpResp.StatusCode -eq [System.Net.HttpStatusCode]::OK) {
                $isReady = $true
                $resp.Close()
                break
            }
            $resp.Close()
        }
        catch {
            # 尚未準備好，等候下一次輪詢
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $isReady) {
        Write-Host "❌ 等候服務就緒逾時 (60 秒)。" -ForegroundColor Red
        if (-not $process.HasExited) {
            $process.Kill()
        }
        exit 1
    }

    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "🎉 PetPet.Modern 本機展示版已成功啟動！" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "🌐 展示網址: $demoUrl" -ForegroundColor Cyan
    Write-Host "📦 資料庫: PetPet.Modern/src/PetPet.Web/App_Data/demo/petpet-demo.db (SQLite)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "🔑 固定展示帳號 (密碼皆為: DemoPass!2026):" -ForegroundColor Yellow
    Write-Host "   1. 飼主展示: demo-owner@example.test   (擁有 4 隻寵物、待接受/已接受好友)" -ForegroundColor White
    Write-Host "   2. 新手展示: demo-nopet@example.test   (0 隻寵物，呈現無寵物導引)" -ForegroundColor White
    Write-Host "   3. 好友展示: demo-friend@example.test  (可接受 owner 邀請)" -ForegroundColor White
    Write-Host "   4. 管理員:   admin@example.test        (管理員權限、停權操作)" -ForegroundColor White
    Write-Host "   5. 停權帳號: disabled@example.test     (停權登入拒絕驗證)" -ForegroundColor White
    Write-Host ""
    Write-Host "按 Ctrl+C 可停止本機展示程序。" -ForegroundColor DarkGray
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host ""

    if (-not $NoBrowser) {
        try {
            Start-Process $demoUrl
        }
        catch {
            Write-Host "請手動開啟瀏覽器前往: $demoUrl" -ForegroundColor Yellow
        }
    }

    # 等候子程序執行 (前景管理)
    $process.WaitForExit()
}
finally {
    if ($process -ne $null -and -not $process.HasExited) {
        Write-Host "`n正在停止 PetPet Demo 程序..." -ForegroundColor Gray
        try {
            $process.Kill()
            $process.WaitForExit(5000)
        }
        catch {}
    }
}
