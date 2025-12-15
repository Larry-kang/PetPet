$ErrorActionPreference = "Stop"

# Use UTF-8 for output
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "🚀 [Agent: Architect] 初始化現代化專案骨架..."

$solutionName = "PetPet.Modern"
$solutionDir = Join-Path $PSScriptRoot $solutionName

if (Test-Path $solutionDir) {
    Write-Warning "⚠️ 目錄 $solutionDir 已存在，將略過建立或覆寫。"
} else {
    Write-Host "📂 建立解決方案目錄: $solutionName"
    dotnet new sln -n $solutionName -o $solutionDir
}

Set-Location $solutionDir

# 為了確保 Clean Architecture，建立 src 目錄
if (-not (Test-Path "src")) { New-Item -ItemType Directory -Path "src" | Out-Null }

# 1. Domain (Class Library)
Write-Host "🏗️ 建立專案: PetPet.Domain (Class Library)"
if (-not (Test-Path "src/PetPet.Domain")) {
    dotnet new classlib -n PetPet.Domain -o src/PetPet.Domain
}

# 2. Application (Class Library)
Write-Host "🏗️ 建立專案: PetPet.Application (Class Library)"
if (-not (Test-Path "src/PetPet.Application")) {
    dotnet new classlib -n PetPet.Application -o src/PetPet.Application
}

# 3. Infrastructure (Class Library)
Write-Host "🏗️ 建立專案: PetPet.Infrastructure (Class Library)"
if (-not (Test-Path "src/PetPet.Infrastructure")) {
    dotnet new classlib -n PetPet.Infrastructure -o src/PetPet.Infrastructure
}

# 4. Web (MVC)
Write-Host "🏗️ 建立專案: PetPet.Web (ASP.NET Core MVC)"
if (-not (Test-Path "src/PetPet.Web")) {
    dotnet new mvc -n PetPet.Web -o src/PetPet.Web
}

# 5. Tests
Write-Host "🏗️ 建立專案: PetPet.UnitTests (xUnit)"
if (-not (Test-Path "tests/PetPet.UnitTests")) {
    dotnet new xunit -n PetPet.UnitTests -o tests/PetPet.UnitTests
}

# Add to Solution
Write-Host "🔗 將專案加入解決方案..."
dotnet sln add src/PetPet.Domain/PetPet.Domain.csproj
dotnet sln add src/PetPet.Application/PetPet.Application.csproj
dotnet sln add src/PetPet.Infrastructure/PetPet.Infrastructure.csproj
dotnet sln add src/PetPet.Web/PetPet.Web.csproj
dotnet sln add tests/PetPet.UnitTests/PetPet.UnitTests.csproj

# Add References
Write-Host "🔗 設定專案依賴關係 (Clean Architecture)..."

# App -> Domain
Write-Host "   App -> Domain"
dotnet add src/PetPet.Application/PetPet.Application.csproj reference src/PetPet.Domain/PetPet.Domain.csproj

# Infra -> App
Write-Host "   Infra -> App"
dotnet add src/PetPet.Infrastructure/PetPet.Infrastructure.csproj reference src/PetPet.Application/PetPet.Application.csproj

# Web -> Infra & App
Write-Host "   Web -> Infra, App"
dotnet add src/PetPet.Web/PetPet.Web.csproj reference src/PetPet.Infrastructure/PetPet.Infrastructure.csproj
dotnet add src/PetPet.Web/PetPet.Web.csproj reference src/PetPet.Application/PetPet.Application.csproj

# Tests -> All
Write-Host "   Tests -> Domain, App"
dotnet add tests/PetPet.UnitTests/PetPet.UnitTests.csproj reference src/PetPet.Domain/PetPet.Domain.csproj
dotnet add tests/PetPet.UnitTests/PetPet.UnitTests.csproj reference src/PetPet.Application/PetPet.Application.csproj

Write-Host "✅ 專案骨架初始化完成！"
