$ErrorActionPreference = "Stop"

Write-Host "Starting PetPet Modern System..."

# Check Docker
if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
    Write-Error "Error: Docker not found. Please install Docker Desktop."
    exit 1
}

$scriptPath = $PSScriptRoot
$projectPath = Join-Path $scriptPath "PetPet.Modern"

Set-Location $projectPath

Write-Host "Building and starting containers..."
docker-compose down
docker-compose up -d --build

Write-Host "System Started!"
Write-Host "Web App: http://localhost:5000"
Write-Host "SQL Server: localhost,1433"
