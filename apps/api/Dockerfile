# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and shared build config first for restore caching
COPY Directory.Packages.props Directory.Build.props global.json ./
COPY SupportDesk.sln ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
COPY apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj apps/api/tests/SupportDesk.UnitTests/
COPY apps/api/tests/SupportDesk.ArchitectureTests/SupportDesk.ArchitectureTests.csproj apps/api/tests/SupportDesk.ArchitectureTests/

# Restore dependencies (cached layer)
RUN dotnet restore SupportDesk.sln

# Copy all source code
COPY apps/api/src/ apps/api/src/
COPY apps/api/tests/ apps/api/tests/

# Build and publish the API
WORKDIR /src/apps/api/src/SupportDesk.Presentation
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Use existing non-root user (app user is pre-created in the base image)
USER app

# Copy published app
COPY --from=build /app/publish .

# Expose HTTP port
EXPOSE 8080

# Start the API
ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]