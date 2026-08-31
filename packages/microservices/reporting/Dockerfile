# Stage 1: Base runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ReportingService.Api/ReportingService.Api.csproj", "ReportingService.Api/"]
COPY ["src/ReportingService.Core/ReportingService.Core.csproj", "ReportingService.Core/"]
COPY ["src/ReportingService.Infrastructure/ReportingService.Infrastructure.csproj", "ReportingService.Infrastructure/"]
RUN dotnet restore "ReportingService.Api/ReportingService.Api.csproj"
COPY src/ .
RUN dotnet build "ReportingService.Api/ReportingService.Api.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "ReportingService.Api/ReportingService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
RUN mkdir -p /app/logs
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "ReportingService.Api.dll"]
