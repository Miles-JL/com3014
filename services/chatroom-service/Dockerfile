# Use the official .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Create a temporary solution file
RUN dotnet new sln -n TempSolution

# Copy only the necessary project files
COPY ["services/chatroom-service/chatroom-service.csproj", "services/chatroom-service/"]
COPY ["shared/Shared.Models/Shared.Models.csproj", "shared/Shared.Models/"]
COPY ["shared/Shared.Auth/Shared.Auth.csproj", "shared/Shared.Auth/"]
COPY ["shared/Shared.Logging/Shared.Logging.csproj", "shared/Shared.Logging/"]

# Add projects to the solution
RUN dotnet sln TempSolution.sln add services/chatroom-service/chatroom-service.csproj
RUN dotnet sln TempSolution.sln add shared/Shared.Models/Shared.Models.csproj
RUN dotnet sln TempSolution.sln add shared/Shared.Auth/Shared.Auth.csproj
RUN dotnet sln TempSolution.sln add shared/Shared.Logging/Shared.Logging.csproj

# Restore packages
RUN dotnet restore "TempSolution.sln"

# Copy the rest of the source code
COPY . .

# Build and publish the application
RUN dotnet publish "services/chatroom-service/chatroom-service.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install required runtime dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    postgresql-client \
    && rm -rf /var/lib/apt/lists/*

# Copy the published application
COPY --from=build /app/publish .

# Copy the entrypoint script and make it executable
COPY ["services/chatroom-service/entrypoint.sh", "./"]
RUN chmod +x ./entrypoint.sh

# Expose the port the app runs on
EXPOSE 80
EXPOSE 443

# Set the entry point
ENTRYPOINT ["./entrypoint.sh"]
