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

You should see `auth_db`, `user_db`, and `chatroom_db` created after the services initialize and run migrations.

---

## Step 2: Restore & Link Shared Projects

Each microservice references shared logic (e.g., models, JWT auth). These links were created using:

```bash
dotnet add auth-service reference ../shared/Shared.Models/Shared.Models.csproj
dotnet add auth-service reference ../shared/Shared.Auth/Shared.Auth.csproj

dotnet add user-service reference ../shared/Shared.Models/Shared.Models.csproj
dotnet add user-service reference ../shared/Shared.Auth/Shared.Auth.csproj

dotnet add chatroom-service reference ../shared/Shared.Models/Shared.Models.csproj
dotnet add chatroom-service reference ../shared/Shared.Auth/Shared.Auth.csproj
```

> You do not need to run these again unless folder structure changes at all.

---

## Step 3: Migrate Databases (Only Needed Once per Dev Machine)

Run the following in each service directory **once**:

```bash
cd auth-service
dotnet ef database update

cd ../user-service
dotnet ef database update

cd ../chatroom-service
dotnet ef database update
```

> Make sure `dotnet ef` tools are installed:  
> `dotnet tool install --global dotnet-ef`

---

## Step 4: Start All Microservices

Run this PowerShell script from the root directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\start-all-microservices.ps1
```

This launches all three microservices with predefined ports:

- `auth-service`: http://localhost:5106  
- `user-service`: http://localhost:5117  
- `chatroom-service`: http://localhost:5262

Each exposes Swagger UI for testing.

---

## JWT Auth & Inter-Service Sync

- When a user logs in/registers via `auth-service`, a JWT is returned.
- You must pass this JWT (with `Bearer` prefix) to protected routes in `user-service` and `chatroom-service`.

Inter-service user syncing is already wired — `auth-service` makes an HTTP POST to `user-service/api/User/sync` to propagate the user after registration.

---

## Reset Migrations

If you ever want to reset the DB:

```bash
dotnet ef migrations remove
dotnet ef migrations add Init
dotnet ef database update
```


---
