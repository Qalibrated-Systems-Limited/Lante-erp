FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update && \
    apt-get install -y wget && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FleetService.Api/FleetService.Api.csproj", "FleetService.Api/"]
COPY ["src/FleetService.Core/FleetService.Core.csproj", "FleetService.Core/"]
COPY ["src/FleetService.Infrastructure/FleetService.Infrastructure.csproj", "FleetService.Infrastructure/"]

RUN dotnet restore "FleetService.Api/FleetService.Api.csproj"

COPY src/ .

RUN dotnet build "FleetService.Api/FleetService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FleetService.Api/FleetService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

RUN mkdir -p /app/logs && mkdir -p /app/uploads

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FleetService.Api.dll"]
