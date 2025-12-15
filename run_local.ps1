$ErrorActionPreference = "Stop"

Write-Host "⚠️ Docker Not Found. Switching to Local Native Mode..."
Write-Host "🚀 Starting PetPet Modern System (LocalDB)..."

$projectPath = Join-Path $PSScriptRoot "PetPet.Modern/src/PetPet.Web"
Set-Location $projectPath

# Update Connection String for Local Execution (Runtime Override)
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=PetPetModern_Local;Trusted_Connection=True;MultipleActiveResultSets=true"

Write-Host "🔧 Database: (localdb)\MSSQLLocalDB [PetPetModern_Local]"
Write-Host "▶️ Launching .NET App..."

dotnet run
