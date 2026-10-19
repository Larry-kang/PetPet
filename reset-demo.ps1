[CmdletBinding()]
param ()

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host "⚠️ PetPet.Modern 本機展示資料重設程序 (Demo Reset)" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Yellow

$demoDir = Join-Path $PSScriptRoot "PetPet.Modern\src\PetPet.Web\App_Data\demo"
$resolvedDemoDir = [System.IO.Path]::GetFullPath($demoDir)

Write-Host "目標展示目錄: $resolvedDemoDir" -ForegroundColor Gray

if (-not (Test-Path -LiteralPath $resolvedDemoDir)) {
    Write-Host "展示目錄不存在，無需重設。" -ForegroundColor Green
    exit 0
}

# 1. 檢查目標目錄及其所有祖先目錄是否為 Symlink / Reparse Point
$curr = $resolvedDemoDir
while ($curr -ne $null -and $curr -ne "") {
    if (Test-Path -LiteralPath $curr) {
        $dirItem = Get-Item -LiteralPath $curr -Force
        if ($dirItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            Write-Host "❌ 安全拒絕：路徑包含 ReparsePoint (Symlink/Junction): $curr" -ForegroundColor Red
            exit 1
        }
    }
    $parent = [System.IO.Path]::GetDirectoryName($curr)
    if ($parent -eq $curr -or [string]::IsNullOrEmpty($parent)) { break }
    $curr = $parent
}

# 2. 獲取並持有排他鎖 (.run.lock) 直到操作結束
$runLockPath = Join-Path $resolvedDemoDir ".run.lock"
$lockStream = $null

try {
    try {
        $lockStream = [System.IO.File]::Open($runLockPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    }
    catch {
        Write-Host "❌ 錯誤：PetPet Demo 服務目前正在執行中 (.run.lock 被佔用)。" -ForegroundColor Red
        Write-Host "請先停止正在執行的 Demo 程序，再執行重設。" -ForegroundColor Yellow
        exit 1
    }

    # 3. 要求使用者明示輸入 RESET
    Write-Host ""
    Write-Host "此操作將會刪除本機 Demo SQLite 資料庫檔案 (.db, .db-shm, .db-wal) 與暫存檔案。" -ForegroundColor Yellow
    Write-Host "下次啟動 ./start-demo.ps1 時將會重新建立初始展示資料庫與 Seed。" -ForegroundColor Yellow
    Write-Host ""
    $userInput = Read-Host "請輸入 'RESET' 確認執行重設 (輸入其他任何字元將取消操作)"

    if ($userInput -cne "RESET") {
        Write-Host "操作已取消，未進行任何變更。" -ForegroundColor Cyan
        exit 0
    }

    # 4. 精確 Allowlist 刪除 (嚴格限定檔案名稱，不可使用任意通配符，拒絕 ReparsePoint)
    $exactAllowlist = @("petpet-demo.db", "petpet-demo.db-wal", "petpet-demo.db-shm", ".init.lock")
    $hasError = $false

    foreach ($fileName in $exactAllowlist) {
        $targetPath = Join-Path $resolvedDemoDir $fileName
        if (Test-Path -LiteralPath $targetPath) {
            $fileItem = Get-Item -LiteralPath $targetPath -Force
            if ($fileItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                Write-Host "❌ 安全警告：目標檔案為 ReparsePoint，拒絕刪除: $targetPath" -ForegroundColor Red
                $hasError = $true
                continue
            }

            try {
                Remove-Item -LiteralPath $targetPath -Force
                Write-Host "已刪除: $fileName" -ForegroundColor Gray
            }
            catch {
                Write-Host "無法刪除 $targetPath: $($_.Exception.Message)" -ForegroundColor Red
                $hasError = $true
            }
        }
    }

    if ($hasError) {
        Write-Host ""
        Write-Host "❌ 重設失敗：部分目標檔案未能成功刪除。" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "✅ 重設完成！展示資料庫已清空。" -ForegroundColor Green
    Write-Host "您現在可以隨時執行 ./start-demo.ps1 重新生成全新初始展示環境。" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Yellow
}
finally {
    if ($lockStream -ne $null) {
        $lockStream.Dispose()
        $lockStream = $null
    }
}
