# Stage 1: Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update && \
    apt-get install -y wget && \
    rm -rf /var/lib/apt/lists/*

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/LicenseService.Api/LicenseService.Api.csproj", "LicenseService.Api/"]
COPY ["src/LicenseService.Core/LicenseService.Core.csproj", "LicenseService.Core/"]
COPY ["src/LicenseService.Infrastructure/LicenseService.Infrastructure.csproj", "LicenseService.Infrastructure/"]

RUN dotnet restore "LicenseService.Api/LicenseService.Api.csproj"

COPY src/ .

RUN dotnet build "LicenseService.Api/LicenseService.Api.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "LicenseService.Api/LicenseService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Final runtime
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

RUN mkdir -p /app/logs

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "LicenseService.Api.dll"]
