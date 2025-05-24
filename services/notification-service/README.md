# Notification Service

A microservice for handling real-time and offline notifications in a distributed system.

## Features

- Store and manage user notifications
- Real-time WebSocket notifications for online users
- Persistent storage of unread notifications
- RESTful API for notification management
- JWT authentication and authorization
- Swagger/OpenAPI documentation

## Prerequisites

- .NET 9.0 SDK
- PostgreSQL 13+
- Node.js (for database migrations)

## Getting Started

### 1. Database Setup

1. Create a new PostgreSQL database:
   ```sql
   CREATE DATABASE notification_service_dev;
   ```

### 2. Configuration

1. Copy `appsettings.Development.json` to `appsettings.Production.json` for production settings:
   ```bash
   cp appsettings.Development.json appsettings.Production.json
   ```

2. Update the connection strings and JWT settings in the appropriate `appsettings.*.json` file.

### 3. Database Migrations

The application will automatically apply migrations on startup. For manual migrations:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Running the Service

```bash
dotnet run --environment Development
```

The service will be available at `https://localhost:5001` (or `http://localhost:5000` for HTTP).

## API Documentation

Once running, access the Swagger UI at:
- `https://localhost:5001/swagger`

## API Endpoints

### Notifications

- `GET /api/notifications` - Get unread notifications
- `POST /api/notifications` - Create a new notification (internal use)
- `POST /api/notifications/mark-read/{id}` - Mark a notification as read
- `POST /api/notifications/mark-all-read` - Mark all notifications as read
- `GET /api/notifications/ws` - WebSocket endpoint for real-time notifications

## WebSocket Integration

To receive real-time notifications, connect to the WebSocket endpoint:

```javascript
const socket = new WebSocket('wss://your-domain.com/api/notifications/ws');

socket.onmessage = (event) => {
  const message = JSON.parse(event.data);
  if (message.type === 'notification') {
    console.log('New notification:', message.data);
    // Display notification to user
  }
};

// Don't forget to handle authentication
socket.onopen = () => {
  socket.send(JSON.stringify({
    type: 'authenticate',
    token: 'your-jwt-token'
  }));
};
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `ConnectionStrings__DefaultConnection` | Database connection string | - |
| `Jwt__Key` | JWT signing key | - |
| `Jwt__Issuer` | JWT issuer | - |
| `Jwt__Audience` | JWT audience | - |
| `InternalApiKey` | API key for internal service communication | - |

## Development

### Testing

Run tests with:

```bash
dotnet test
```

### Linting and Code Style

This project uses EditorConfig for consistent code style. Most IDEs will automatically apply these settings.

## Deployment

### Docker

Build the Docker image:

```bash
docker build -t notification-service .
```

Run the container:

```bash
docker run -d -p 5000:80 \
  -e ConnectionStrings__DefaultConnection="Host=db;Database=notification_service;Username=postgres;Password=yourpassword" \
  -e Jwt__Key="your-secure-key" \
  -e Jwt__Issuer="your-issuer" \
  -e Jwt__Audience="your-audience" \
  notification-service
```

## License

[Your License Here]
