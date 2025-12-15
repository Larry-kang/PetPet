@echo off
chcp 65001 > nul
echo ⚠️ Docker Not Found. Switching to Local Native Mode...
echo 🚀 Starting PetPet Modern System (LocalDB)...

cd /d "%~dp0PetPet.Modern\src\PetPet.Web"

set "ConnectionStrings__DefaultConnection=Server=(localdb)\MSSQLLocalDB;Database=PetPetModern_Local;Trusted_Connection=True;MultipleActiveResultSets=true"

echo 🔧 Database: (localdb)\MSSQLLocalDB [PetPetModern_Local]
echo ▶️ Launching .NET App...

dotnet run
pause
