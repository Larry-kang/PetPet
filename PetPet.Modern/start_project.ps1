# One-Click Start Script for PetPet.Modern
# created by Agent Swarm (Ops)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting PetPet Modernization Environment..." -ForegroundColor Green

# 1. Check if Docker is installed
if (!(Get-Command "docker" -ErrorAction SilentlyContinue)) {
    Write-Error "❌ Docker is not installed or not in PATH."
    exit 1
}

# 2. Check Docker Daemon
try {
    docker info > $null
}
catch {
    Write-Error "❌ Docker Desktop is not running. Please start Docker Desktop first."
    exit 1
}

Write-Host "✅ Docker environment is healthy." -ForegroundColor Green

# 3. Build and Start Containers
Write-Host "📦 Building and starting containers..." -ForegroundColor Cyan
Set-Location "d:\Users\003565\Desktop\Larry\Practice\petpet\PetPet.Modern"
docker-compose up -d --build

# 4. Wait for Web Service
Write-Host "⏳ Waiting for service to be fully ready..." -ForegroundColor Cyan
Start-Sleep -Seconds 10
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5000" -UseBasicParsing -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ System is Online!" -ForegroundColor Green
    }
}
catch {
    Write-Warning "⚠️ Web service might be still starting up. Please wait a few more seconds."
}

# 5. Open Browser
Write-Host "🌐 Opening Browser..." -ForegroundColor Yellow
Start-Process "http://localhost:5000"

Write-Host "🎉 Deployment Complete. Happy Matching!" -ForegroundColor Magenta
