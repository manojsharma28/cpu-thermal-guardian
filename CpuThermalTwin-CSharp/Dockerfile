# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory
WORKDIR /src

# Copy the project file and restore dependencies
COPY CpuThermalTwin.csproj ./
RUN dotnet restore

# Copy the source code and build the application
COPY . ./
RUN dotnet build --configuration Release --no-restore
RUN dotnet publish --configuration Release --no-build --output /app

# Use the official .NET 8.0 runtime image for the final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

# Set the working directory
WORKDIR /app

# Copy the published application from the build stage
COPY --from=build /app .

# Create a non-root user for security
RUN useradd --create-home --shell /bin/bash appuser
USER appuser

# Set the entry point
ENTRYPOINT ["dotnet", "CpuThermalTwin.dll"]