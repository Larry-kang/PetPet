@echo off
echo ===================================================
echo [PetPet.Modern] Docker Environment Launcher
echo ===================================================

echo [1/3] Checking Docker status...
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Docker is NOT running. Please start Docker Desktop and try again.
    pause
    exit /b
)

echo [2/3] Building and Starting Containers...
echo This may take a few minutes for the first run (downloading images/building app).
cd PetPet.Modern
docker-compose up --build -d

echo [3/3] Deployment Initiated!
echo Container is starting up. Please wait ~30 seconds for SQL Server to initialize.
echo Application url: http://localhost:5000

echo.
echo Press any key to open the browser...
pause >nul
start http://localhost:5000
