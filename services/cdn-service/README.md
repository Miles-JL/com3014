# CDN Service

A lightweight microservice for handling file uploads and static file serving in the COM3014 microservices ecosystem.

## 🚀 Project Overview

The CDN Service provides a simple, secure way to handle user-uploaded files (like profile pictures) and serve them efficiently. It's designed to be stateless and doesn't store any user metadata, making it highly scalable.

## ✨ Key Features

- **File Uploads**: Securely upload user profile pictures
- **Public File Serving**: Serve uploaded files via clean URLs
- **JWT Authentication**: Secure endpoints with JWT validation
- **Automatic Cleanup**: Optional deletion of old files
- **File Validation**: Ensures only allowed file types are uploaded

## 🔌 Endpoints

| Method | Endpoint | Description | Authentication |
|--------|----------|-------------|----------------|
| GET    | `/health` | Check service health | Public |
| POST   | `/api/upload` | Upload a new file | JWT Required |
| DELETE | `/api/upload/{filename}` | Delete a file | JWT Required |
| GET    | `/u/{filename}` | Access an uploaded file | Public |

## 🔐 Authentication Setup

1. The service uses JWT for authentication
2. Tokens are validated using shared logic from `Shared.Auth`
3. Public endpoints don't require authentication

## 🏃‍♂️ Running Locally

1. Navigate to the project root:
   ```bash
   cd services/cdn-service
   ```

2. Restore dependencies and run:
   ```bash
   dotnet restore
   dotnet run
   ```

3. The service will start on `http://localhost:5250`

## 🧪 Testing with Swagger

1. Access the Swagger UI at `http://localhost:5250/swagger`
2. Click the "Authorize" button (lock icon)
3. Paste your JWT token in the format: `Bearer your.jwt.token.here`
4. Try the following:
   - **Upload**: Use the POST `/api/upload` endpoint with a file
   - **Delete**: Use the DELETE `/api/upload/{filename}` endpoint
   - **View**: Access files directly at `http://localhost:5250/u/filename.jpg`

## 🔄 Integration Notes

- **user-service**: Makes authenticated calls to upload and delete profile pictures
- **auth-service**: Receives updated image URLs from user-service
- **Frontend**: References images using the returned URLs (e.g., `http://cdn.myapp.com/u/filename.jpg`)

## 📝 Configuration

Add these to `appsettings.json`:

```json
{
  "Cdn": {
    "BaseUrl": "http://localhost:5250"
  },
  "Jwt": {
    "Key": "your-secure-key-here",
    "Issuer": "your-issuer",
    "Audience": "your-audience"
  }
}
```

## 💾 Storage Abstraction

The CDN service uses a flexible storage abstraction that makes it easy to switch between different storage providers (local, cloud, etc.) without changing the application code.

### Current Implementation: Local Storage

By default, the service uses `LocalFileStorageService` which stores files on the local filesystem in a `storage` directory.

```csharp
// Program.cs
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
```

### How to Switch to a Different Storage Provider

1. **Create a new storage provider** by implementing the `IFileStorageService` interface:

```csharp
public class CloudFileStorageService : IFileStorageService
{
    public Task<string> UploadFileAsync(IFormFile file, string? oldFileName = null)
    {
        // Your cloud storage implementation here
    }
    
    public Task<bool> DeleteFileAsync(string filename)
    {
        // Your cloud storage implementation here
    }
    
    public string GetFileUrl(string filename)
    {
        // Return public URL for the file
    }
}
```

2. **Update the service registration** in `Program.cs`:

```csharp
// Change this line to use your new provider
builder.Services.AddScoped<IFileStorageService, CloudFileStorageService>();
```

### Benefits of This Approach

- **Easy to Test**: Mock the `IFileStorageService` in unit tests
- **No Code Changes**: Switch providers by changing one line of code
- **Future-Proof**: Add new storage providers without modifying existing code
- **Consistent API**: All storage providers implement the same interface

### Available Storage Providers

| Provider | Class | Description |
|----------|-------|-------------|
| Local File System | `LocalFileStorageService` | Stores files on the server's filesystem |
| Azure Blob Storage | (Example) `AzureBlobStorageService` | Stores files in Azure Blob Storage |
| AWS S3 | (Example) `S3StorageService` | Stores files in AWS S3 |

### Configuration

Each storage provider may require different configuration. For example, cloud providers will need connection strings or access keys, which should be stored in `appsettings.json` or environment variables.

Example for Azure Blob Storage:
```json
{
  "AzureStorage": {
    "ConnectionString": "your-connection-string",
    "ContainerName": "your-container"
  }
}
```

## ✅ Health Check Endpoint

The service exposes a health check endpoint at `/health` that monitors the service's operational status. This endpoint is particularly useful for monitoring, load balancing, and automated recovery systems.

### Response Format

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "storage_health",
      "status": "Healthy",
      "description": "Storage is accessible",
      "exception": null
    }
  ]
}
```

### Status Codes
- `200 OK`: Service is healthy and fully operational
- `503 Service Unavailable`: Service is unhealthy (check the response for details)

### Usage Examples

**Basic Health Check**
```bash
curl http://localhost:5250/health
```

**Using in Docker Healthcheck**
```dockerfile
HEALTHCHECK --interval=30s --timeout=3s \
  CMD curl -f http://localhost:5250/health || exit 1
```

**Kubernetes Liveness Probe**
```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 5250
  initialDelaySeconds: 10
  periodSeconds: 5
```

### Integration Points

1. **Load Balancers**
   - Configure health checks to route traffic only to healthy instances
   - Automatically take unhealthy instances out of rotation

2. **Monitoring Systems**
   - Set up alerts when the service becomes unhealthy
   - Track service availability metrics over time

3. **CI/CD Pipelines**
   - Verify service health after deployment
   - Roll back if health checks fail

4. **Service Dependencies**
   - Other services can check CDN health before making requests
   - Implement circuit breakers based on health status

5. **Infrastructure as Code**
   - Use in Terraform or other provisioning tools
   - Validate infrastructure health during deployment

## 📝 Logging and Monitoring

The service implements comprehensive logging and monitoring to ensure reliability and ease of debugging.

### Logging Configuration

#### Log Levels
- `Debug`: Detailed debug information (development only)
- `Information`: Normal operations (file uploads, deletes, etc.)
- `Warning`: Non-critical issues (invalid file types, missing files)
- `Error`: Critical failures (file system errors, etc.)

#### Log Format
Each log entry includes:
- Timestamp
- Log level
- Request ID (for correlation)
- Source context (controller/middleware)
- Structured message

#### Example Log Entries
```
# Successful file upload
[12:34:56 INF] [RequestId: abc123] [CdnService.UploadController] Starting file upload
[12:34:57 INF] [RequestId: abc123] [CdnService.UploadController] Validating file type: image/jpeg
[12:34:57 INF] [RequestId: abc123] [CdnService.UploadController] Saving file: abc123.jpg (2.4MB)
[12:34:57 INF] [RequestId: abc123] [CdnService.UploadController] File uploaded successfully: /u/abc123.jpg

# Error case
[12:35:01 WRN] [RequestId: def456] [CdnService.ExceptionHandling] Invalid file type: application/exe
[12:35:01 WRN] [RequestId: def456] [CdnService.UploadController] File validation failed: Invalid file type
```

### Viewing Logs

#### Development Environment
1. Run the service with:
   ```bash
   $env:ASPNETCORE_ENVIRONMENT="Development"
   dotnet run
   ```
2. Logs will appear in the console with colors and detailed information

#### Production Environment
1. Logs are written to the console in JSON format
2. Can be collected by container orchestration (Docker, Kubernetes)
3. Use `docker logs` or your container platform's log viewer

### Exception Handling

#### Global Exception Middleware
- Catches all unhandled exceptions
- Returns consistent error responses
- Logs detailed error information
- Masks sensitive data in production

#### Error Response Format
```json
{
  "requestId": "abc123",
  "message": "File size exceeds the limit of 5MB",
  "timestamp": "2025-05-24T12:34:56Z"
}
```

### Monitoring

#### Health Check Endpoint
- `GET /health` - Returns service health status
- Monitors storage accessibility
- Used by load balancers and orchestration systems

#### Metrics (Planned)
- Request rates
- Error rates
- File operations
- Storage usage

### Debugging Tips

1. **Correlating Logs**
   - Use the `RequestId` to track a request through the system
   - Example: `grep "abc123" logs.txt`

2. **Common Issues**
   - **Missing logs?** Check if running in Production mode (logs are less verbose)
   - **Can't find logs?** Ensure you're looking at the correct terminal/container
   - **No RequestId?** The request might not be reaching the service

3. **Testing Logging**
   ```bash
   # Test health check logging
   curl http://localhost:5250/health
   
   # Test error logging
   curl -X POST http://localhost:5250/api/upload -H "Content-Type: multipart/form-data" -F "file=@test.txt"
   ```

## ⚠️ Known Limitations

- No built-in rate limiting
- File storage is local to the service instance
- No built-in file size limits (handled by clients)
- No image processing/resizing

## 🔮 Future Improvements

- Add image processing (resize, compress)
- Implement CDN distribution
- Add file size limits in configuration
- Implement rate limiting
- Add health check endpoint
- Support for private files with access control
