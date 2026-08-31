# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["LanteGateway.csproj", "./"]
RUN dotnet restore "LanteGateway.csproj"

COPY . .

RUN dotnet publish "LanteGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 5000

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0

ENTRYPOINT ["dotnet", "LanteGateway.dll"]
