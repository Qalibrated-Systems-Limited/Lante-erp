FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update && \
    apt-get install -y wget && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/CrmService.Api/CrmService.Api.csproj", "CrmService.Api/"]
COPY ["src/CrmService.Core/CrmService.Core.csproj", "CrmService.Core/"]
COPY ["src/CrmService.Infrastructure/CrmService.Infrastructure.csproj", "CrmService.Infrastructure/"]

RUN dotnet restore "CrmService.Api/CrmService.Api.csproj"

COPY src/ .

RUN dotnet build "CrmService.Api/CrmService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CrmService.Api/CrmService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

RUN mkdir -p /app/logs && mkdir -p /app/uploads

ENV UPLOADS_PATH=/app/uploads

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CrmService.Api.dll"]
