FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5005
VOLUME ["/app/uploads"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/SpazaSure.UserService/SpazaSure.UserService.csproj", "src/Services/SpazaSure.UserService/"]
COPY ["src/Shared/SpazaSure.Infrastructure/SpazaSure.Infrastructure.csproj", "src/Shared/SpazaSure.Infrastructure/"]
COPY ["src/Shared/SpazaSure.Shared/SpazaSure.Shared.csproj", "src/Shared/SpazaSure.Shared/"]
RUN dotnet restore "src/Services/SpazaSure.UserService/SpazaSure.UserService.csproj"
COPY . .
RUN dotnet build "src/Services/SpazaSure.UserService/SpazaSure.UserService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/SpazaSure.UserService/SpazaSure.UserService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpazaSure.UserService.dll"]
