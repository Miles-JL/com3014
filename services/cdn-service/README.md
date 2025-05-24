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

## 📝 Logging and Exception Handling

The service implements comprehensive logging and exception handling to aid in debugging and monitoring.

### Logging
- **Request Tracking**: Each request is assigned a unique request ID for correlation
- **Structured Logging**: Logs include structured data for better querying
- **Log Levels**:
  - `Information`: Normal operations (file uploads, deletes, etc.)
  - `Warning`: Non-critical issues (invalid file types, missing files)
  - `Error`: Critical failures (file system errors, etc.)

### Exception Handling
- **Global Exception Middleware**: Catches all unhandled exceptions
- **Consistent Error Responses**: Returns standardized error responses
- **Request IDs**: Each error includes a unique request ID for tracking
- **Security**: Stack traces are only shown in non-production environments

### Example Log Entry
```
[12:34:56 INF] [RequestId: abc123] Starting file upload
[12:34:57 INF] [RequestId: abc123] Saving file example.jpg as 1a2b3c4d.jpg
[12:34:57 INF] [RequestId: abc123] File uploaded successfully: http://cdn.example.com/u/1a2b3c4d.jpg
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
