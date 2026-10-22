FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
# QuestPDF renders invoices with SkiaSharp, which needs fontconfig and at least one font
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core && rm -rf /var/lib/apt/lists/*
EXPOSE 5038

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/SpazaSure.PaymentService/SpazaSure.PaymentService.csproj", "src/Services/SpazaSure.PaymentService/"]
COPY ["src/Shared/SpazaSure.Infrastructure/SpazaSure.Infrastructure.csproj", "src/Shared/SpazaSure.Infrastructure/"]
COPY ["src/Shared/SpazaSure.Shared/SpazaSure.Shared.csproj", "src/Shared/SpazaSure.Shared/"]
RUN dotnet restore "src/Services/SpazaSure.PaymentService/SpazaSure.PaymentService.csproj"
COPY . .
RUN dotnet build "src/Services/SpazaSure.PaymentService/SpazaSure.PaymentService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/SpazaSure.PaymentService/SpazaSure.PaymentService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpazaSure.PaymentService.dll"]
