FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/SpazaSure.AuthService/SpazaSure.AuthService.csproj", "src/Services/SpazaSure.AuthService/"]
COPY ["src/Shared/SpazaSure.Infrastructure/SpazaSure.Infrastructure.csproj", "src/Shared/SpazaSure.Infrastructure/"]
COPY ["src/Shared/SpazaSure.Shared/SpazaSure.Shared.csproj", "src/Shared/SpazaSure.Shared/"]
RUN dotnet restore "src/Services/SpazaSure.AuthService/SpazaSure.AuthService.csproj"
COPY . .
RUN dotnet build "src/Services/SpazaSure.AuthService/SpazaSure.AuthService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/SpazaSure.AuthService/SpazaSure.AuthService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpazaSure.AuthService.dll"]
