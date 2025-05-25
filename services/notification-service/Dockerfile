# Use the official .NET SDK image for building the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY services/notification-service/notification-service.csproj ./


# Copy the rest of the source code
COPY services/notification-service/. ./

COPY shared ./shared
RUN dotnet restore

# Build the application (Debug configuration)
RUN dotnet build

# Use the official .NET runtime image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy build output from build stage
COPY --from=build /src/bin/Debug/net9.0 ./

# Expose the port (match with your appsettings or launchSettings)
EXPOSE 5247

# Set environment variables if needed (optional)
# ENV ASPNETCORE_ENVIRONMENT=Development

# Start the application
ENTRYPOINT ["dotnet", "notification-service.dll"]