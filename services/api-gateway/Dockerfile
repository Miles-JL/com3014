# Use the official .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the solution file
COPY ["com3014.sln", "./"]

# Copy the project file and restore dependencies
COPY ["services/api-gateway/api-gateway.csproj", "services/api-gateway/"]
COPY ["shared/Shared.Models/Shared.Models.csproj", "shared/Shared.Models/"]
COPY ["shared/Shared.Auth/Shared.Auth.csproj", "shared/Shared.Auth/"]
COPY ["shared/Shared.Logging/Shared.Logging.csproj", "shared/Shared.Logging/"]

# Restore all projects
RUN dotnet restore "services/api-gateway/api-gateway.csproj"

# Copy the rest of the source code
COPY . .

# Publish the application
RUN dotnet publish "services/api-gateway/api-gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install required runtime dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy the published application
COPY --from=build /app/publish .

# Copy the entrypoint script and make it executable
COPY ["services/api-gateway/entrypoint.sh", "./"]
RUN chmod +x ./entrypoint.sh

# Expose the port the app runs on
EXPOSE 80
EXPOSE 443

# Set the entry point
ENTRYPOINT ["dotnet", "api-gateway.dll"]
