# Base stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/PetPet.Web/PetPet.Web.csproj", "src/PetPet.Web/"]
COPY ["src/PetPet.Infrastructure/PetPet.Infrastructure.csproj", "src/PetPet.Infrastructure/"]
COPY ["src/PetPet.Application/PetPet.Application.csproj", "src/PetPet.Application/"]
COPY ["src/PetPet.Domain/PetPet.Domain.csproj", "src/PetPet.Domain/"]
RUN dotnet restore "src/PetPet.Web/PetPet.Web.csproj"
COPY . .
WORKDIR "/src/src/PetPet.Web"
RUN dotnet build "PetPet.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PetPet.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PetPet.Web.dll"]
