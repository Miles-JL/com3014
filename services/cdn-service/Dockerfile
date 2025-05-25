# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["services/cdn-service/cdn-service.csproj", "services/cdn-service/"]
RUN dotnet restore "services/cdn-service/cdn-service.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/services/cdn-service"
RUN dotnet build "cdn-service.csproj" -c Release -o /app/build

# Publish the app
FROM build AS publish
RUN dotnet publish "cdn-service.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create storage and seed data directories
RUN mkdir -p /app/storage /app/seed-data && \
    chmod -R 777 /app/storage && \
    chmod -R 755 /app/seed-data

# Copy the published app
COPY --from=publish /app/publish .

# Copy seed data
COPY services/cdn-service/seed-data/ /app/seed-data/

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5250
ENV ASPNETCORE_ENVIRONMENT=Production
ENV STORAGE_PATH=/app/storage

# Expose the port the app runs on
EXPOSE 5250

# Set the entry point
ENTRYPOINT ["dotnet", "cdn-service.dll"]
