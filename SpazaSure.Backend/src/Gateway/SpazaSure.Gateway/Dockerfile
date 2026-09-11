FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5181

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Gateway/SpazaSure.Gateway/SpazaSure.Gateway.csproj", "src/Gateway/SpazaSure.Gateway/"]
RUN dotnet restore "src/Gateway/SpazaSure.Gateway/SpazaSure.Gateway.csproj"
COPY . .
RUN dotnet build "src/Gateway/SpazaSure.Gateway/SpazaSure.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Gateway/SpazaSure.Gateway/SpazaSure.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpazaSure.Gateway.dll"]
