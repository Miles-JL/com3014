## Prerequisites

Ensure the following tools are installed:

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [pgAdmin 4](https://www.pgadmin.org/download/) (optional for viewing the DB)
- Git / PowerShell (for script execution)

---

## Step 1: Start the PostgreSQL Database via Docker

Run the following command from the root project directory:

```powershell
docker compose up -d
```

This spins up a `postgres` container and makes PostgreSQL available at:

```
Host:     localhost
Port:     5432
Username: postgres
Password: postgres
```

You should see `auth_db`, `user_db`, `chatroom_db`, and `message_db` created after the services initialize and run migrations.

---

## Step 2: Reset Migrations

Run the following PowerShell script from the root directory to reset and reinitialize migrations for all services:

```powershell
powershell -ExecutionPolicy Bypass -File .\reset-migrations.ps1
```

This script will:
- Drop existing databases.
- Remove old migrations.
- Add new `Init` migrations.
- Update the databases.

---

## Step 3: Start All Microservices

Run this PowerShell script from the root directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\start-all-microservices.ps1
```

This launches all the microservices with predefined ports:

- `auth-service`: http://localhost:5106  
- `user-service`: http://localhost:5117  
- `chatroom-service`: http://localhost:5262  
- `message-service`: http://localhost:5199  
- `realtime-service`: http://localhost:5200  
- `notification-service`: http://localhost:5201  
- `api-gateway`: http://localhost:5247  

Each exposes Swagger UI for testing (if applicable).

---

## Step 4: Start the Frontend

Run this PowerShell script from the root directory to start the frontend application:

```powershell
powershell -ExecutionPolicy Bypass -File .\start-frontend.ps1
```

This script will:
- Navigate to the `frontend` directory.
- Install all required dependencies using `npm install`.
- Start the development server using `npm start`.

The frontend will be available at:

- `http://localhost:3000` (default React development server port).

---

## Reset Migrations (Optional)

If you ever want to reset the DB manually:

```bash
dotnet ef migrations remove
dotnet ef migrations add Init
dotnet ef database update
```
