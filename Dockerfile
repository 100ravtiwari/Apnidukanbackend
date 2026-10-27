# ==========================================
# Multi-stage Dockerfile for .NET 9 Web API
# Optimized for minimal image size & low RAM usage
# ==========================================

# 1. Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj first for layer caching
COPY ["Dukan.API.csproj", "./"]
RUN dotnet restore "Dukan.API.csproj"

# Copy source code and publish release build
COPY . .
RUN dotnet publish "Dukan.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Run as non-root user for security
RUN groupadd -r dukan && useradd -r -g dukan dukan

COPY --from=build /app/publish .

# Environment settings: ASP.NET Core port 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# Create secrets directory and grant permissions
RUN mkdir -p /app/secrets && chown -R dukan:dukan /app

USER dukan
EXPOSE 8080

HEALTHCHECK --interval=20s --timeout=4s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Dukan.API.dll"]
