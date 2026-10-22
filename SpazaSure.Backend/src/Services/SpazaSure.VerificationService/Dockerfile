FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/SpazaSure.VerificationService/SpazaSure.VerificationService.csproj", "src/Services/SpazaSure.VerificationService/"]
COPY ["src/Shared/SpazaSure.Infrastructure/SpazaSure.Infrastructure.csproj", "src/Shared/SpazaSure.Infrastructure/"]
COPY ["src/Shared/SpazaSure.Shared/SpazaSure.Shared.csproj", "src/Shared/SpazaSure.Shared/"]
RUN dotnet restore "src/Services/SpazaSure.VerificationService/SpazaSure.VerificationService.csproj"
COPY . .
RUN dotnet build "src/Services/SpazaSure.VerificationService/SpazaSure.VerificationService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/SpazaSure.VerificationService/SpazaSure.VerificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpazaSure.VerificationService.dll"]
