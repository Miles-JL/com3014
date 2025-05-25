## Prerequisites

Ensure the following tools are installed:

- [NGINX for Windows](https://nginx.org/en/download.html)
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [pgAdmin 4](https://www.pgadmin.org/download/) (optional for viewing the DB)
- Git / PowerShell (for script execution)

---

## Step 1: Run docker

Open docker desktop on windows

Run the following command from the root folder

docker-compose up --build

This spins up each microservice, the frontend, nginx and the postgresql server