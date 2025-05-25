# Use the official .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Create a temporary solution file
RUN dotnet new sln -n TempSolution

# Copy only the necessary project files
COPY ["services/user-service/user-service.csproj", "services/user-service/"]
COPY ["shared/Shared.Models/Shared.Models.csproj", "shared/Shared.Models/"]
COPY ["shared/Shared.Auth/Shared.Auth.csproj", "shared/Shared.Auth/"]
COPY ["shared/Shared.Logging/Shared.Logging.csproj", "shared/Shared.Logging/"]

# Add projects to the solution
RUN dotnet sln TempSolution.sln add services/user-service/user-service.csproj
RUN dotnet sln TempSolution.sln add shared/Shared.Models/Shared.Models.csproj
RUN dotnet sln TempSolution.sln add shared/Shared.Auth/Shared.Auth.csproj
RUN dotnet sln TempSolution.sln add shared/Shared.Logging/Shared.Logging.csproj

# Restore packages
RUN dotnet restore "TempSolution.sln"

# Create the Data directory and copy the seed data
RUN mkdir -p /src/services/user-service/Data
COPY ["services/user-service/Data/user_users_seed.csv", "services/user-service/Data/"]

# Copy the rest of the source code
COPY . .

# Create the Data directory in the output directory
RUN mkdir -p /app/publish/Data

# Copy the seed data to the output directory
COPY ["services/user-service/Data/user_users_seed.csv", "/app/publish/Data/"]

# Build and publish the application
RUN dotnet publish "services/user-service/user-service.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install required runtime dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    postgresql-client \
    && rm -rf /var/lib/apt/lists/*

# Create a directory for file uploads
RUN mkdir -p /app/wwwroot/uploads

# Copy the published application
COPY --from=build /app/publish .

# Copy the entrypoint script and make it executable
COPY ["services/user-service/entrypoint.sh", "./"]
RUN chmod +x ./entrypoint.sh

# Expose the port the app runs on
EXPOSE 80
EXPOSE 443

# Set the entry point
ENTRYPOINT ["./entrypoint.sh"]
