## Prerequisites

Ensure the following tools are installed:

- [NGINX for Windows](https://nginx.org/en/download.html) (install to `C:\nginx`)
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [pgAdmin 4](https://www.pgadmin.org/download/) (optional for viewing the DB)
- Git / PowerShell (for script execution)

---

## Step 1: Install NGINX

1. Download the Windows build of **NGINX** from the official site: https://nginx.org/en/download.html
2. Extract it directly to `C:\nginx` — this is required for consistency with our project scripts.
3. You do not need to set up anything else manually. The config file is already provided in `services/load-balancer/nginx.conf`.

---

## Step 2: Start the PostgreSQL Database via Docker

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

## Step 3: Reset Migrations

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

## Step 4: Start All Microservices

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
- `api-gateway` (Instance 1): http://localhost:5247  
- `api-gateway` (Instance 2): http://localhost:5248  
- `api-gateway` (Instance 3): http://localhost:5249  

Each exposes Swagger UI for testing (if applicable).

---

## Step 5: Start the Frontend

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

**Important:** You should access the frontend through NGINX at:

- `http://localhost/`

This ensures that API requests are properly routed through the load balancer.

---

## Step 6: Start the Load Balancer (NGINX)

After the frontend and API Gateway services are running, open a new PowerShell window and run:

```powershell
cd C:\nginx
.\nginx.exe -c "C:\Path\To\Your\com3014\load-balancer\nginx.conf"
```

> Replace the path with the actual location of your `nginx.conf` (Right click `nginx.conf` and Copy Path).

This starts NGINX with our custom config. It proxies:
- Frontend requests at `/` to `localhost:3000`
- API requests at `/api/` to the round-robin upstream (5247, 5248, 5249)

### Optional: Managing NGINX (Do these from a new terminal)

```powershell
# Reload the config
cd C:\nginx
.\nginx.exe -s reload

# Gracefully quit
cd C:\nginx
.\nginx.exe -s quit

# Force shutdown all NGINX processes
taskkill /F /IM nginx.exe
```

---

## Reset Migrations (Optional)

If you ever want to reset the DB manually:

```bash
dotnet ef migrations remove
dotnet ef migrations add Init
dotnet ef database update
```
