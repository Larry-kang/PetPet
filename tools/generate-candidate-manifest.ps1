param (
    [string]$RunId = "20261016-delivery-01",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "docs\reviews\results\$RunId\source-manifest.json"
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host "Generating Candidate Manifest for Run ID: $RunId..." -ForegroundColor Cyan

$includeFiles = New-Object System.Collections.Generic.List[string]

# 1. Root tools & configs
$rootFiles = @(
    "start-demo.ps1",
    "reset-demo.ps1",
    "global.json",
    ".gitignore",
    "README.md",
    "PetPet.Modern\README.md",
    "PetPet.Modern\PetPet.Modern.sln"
)
foreach ($rf in $rootFiles) {
    $full = Join-Path $repoRoot $rf
    if (Test-Path $full) {
        $includeFiles.Add($full)
    }
}

# 2. Product Source & Delivered Static Assets (src)
$srcDir = Join-Path $repoRoot "PetPet.Modern\src"
if (Test-Path $srcDir) {
    $srcFiles = Get-ChildItem -Path $srcDir -Recurse -File | Where-Object {
        $_.FullName -notmatch "[\\/](bin|obj)[\\/]" -and
        $_.Extension -notin @(".db", ".db-shm", ".db-wal", ".lock") -and
        $_.Name -notlike "*.tmp"
    }
    foreach ($f in $srcFiles) {
        $includeFiles.Add($f.FullName)
    }
}

# 3. Tests (tests)
$testsDir = Join-Path $repoRoot "PetPet.Modern\tests"
if (Test-Path $testsDir) {
    $testFiles = Get-ChildItem -Path $testsDir -Recurse -File | Where-Object {
        $_.FullName -notmatch "[\\/](bin|obj|TestResults)[\\/]" -and
        $_.Extension -in @(".cs", ".csproj")
    }
    foreach ($f in $testFiles) {
        $includeFiles.Add($f.FullName)
    }
}

# 4. Tools (All executable scripts, manifest generator, verifiers, runner & lockfiles)
$toolsDir = Join-Path $repoRoot "tools"
if (Test-Path $toolsDir) {
    $toolFiles = Get-ChildItem -Path $toolsDir -Recurse -File | Where-Object {
        $_.FullName -notmatch "[\\/](node_modules|bin|obj)[\\/]" -and
        $_.Name -notlike "*.log" -and
        $_.Name -notlike "*.tmp"
    }
    foreach ($f in $toolFiles) {
        $includeFiles.Add($f.FullName)
    }
}

$sha256 = [System.Security.Cryptography.SHA256]::Create()
$manifestEntries = @()

$sortedFiles = $includeFiles | Sort-Object -Unique
foreach ($filePath in $sortedFiles) {
    $relative = $filePath.Substring($repoRoot.Length).TrimStart('\', '/') -replace '\\', '/'
    $bytes = [System.IO.File]::ReadAllBytes($filePath)
    $hashBytes = $sha256.ComputeHash($bytes)
    $hexHash = [Convert]::ToHexString($hashBytes)
    $size = (Get-Item $filePath).Length

    $manifestEntries += [PSCustomObject]@{
        path   = $relative
        sha256 = $hexHash
        size   = $size
    }
}

# Binary build artifacts hashes (if present)
$binDir = Join-Path $repoRoot ".artifacts_build\bin"
$binaryEntries = @()
if (Test-Path $binDir) {
    $binFiles = Get-ChildItem -Path $binDir -Recurse -Filter "*.dll" | Where-Object {
        $_.FullName -match "release" -and $_.Name -like "PetPet.*"
    }
    foreach ($bf in $binFiles) {
        $bytes = [System.IO.File]::ReadAllBytes($bf.FullName)
        $hashBytes = $sha256.ComputeHash($bytes)
        $hexHash = [Convert]::ToHexString($hashBytes)
        $binaryEntries += [PSCustomObject]@{
            assembly = $bf.Name
            path     = $bf.FullName.Substring($repoRoot.Length).TrimStart('\', '/') -replace '\\', '/'
            sha256   = $hexHash
            size     = $bf.Length
        }
    }
}

$manifest = [PSCustomObject]@{
    runId            = $RunId
    generatedAtUtc   = (Get-Date).ToUniversalTime().ToString("o")
    totalSourceFiles = $manifestEntries.Count
    sourceFiles      = $manifestEntries
    buildArtifacts   = $binaryEntries
}

$json = $manifest | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.Encoding]::UTF8)

Write-Host "Candidate Manifest written to: $OutputPath ($($manifestEntries.Count) source files, $($binaryEntries.Count) binaries)" -ForegroundColor Green
