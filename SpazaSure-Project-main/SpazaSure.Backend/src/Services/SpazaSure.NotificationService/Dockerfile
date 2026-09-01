FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5184

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/SpazaSure.NotificationService/SpazaSure.NotificationService.csproj", "src/Services/SpazaSure.NotificationService/"]
RUN dotnet restore "src/Services/SpazaSure.NotificationService/SpazaSure.NotificationService.csproj"
COPY . .
RUN dotnet build "src/Services/SpazaSure.NotificationService/SpazaSure.NotificationService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/SpazaSure.NotificationService/SpazaSure.NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpazaSure.NotificationService.dll"]
