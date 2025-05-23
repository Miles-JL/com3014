# API Gateway Load Balancer

This project provides a load balancer setup using NGINX to distribute traffic across multiple instances of an API Gateway service.

## Overview

The load balancer is configured to distribute requests across three API Gateway instances running on different ports:

- 5247  
- 5248  
- 5249

It uses a round-robin algorithm and closes each connection after serving one request to ensure even distribution.

## Installation

1. Download and install **NGINX** (version 1.27.5 or later).
2. Place the provided `nginx.conf` file inside your project folder (e.g., `services/load-balancer/`).

## Configuration

The load balancer includes:

- Round-robin distribution
- No persistent connections
- Short keepalive and timeout settings
- Correlation ID forwarding via headers

## Running the Load Balancer (PowerShell Example)

1. Launch your three API Gateway services on ports 5247, 5248, and 5249.
2. Open **PowerShell** and run the following:

```powershell
# Navigate to the NGINX directory (adjust the path as needed)
cd "C:\Users\your-name\Downloads\nginx-1.27.5\nginx-1.27.5"

# Start NGINX with the custom config (adjust the config path as needed)
.\nginx.exe -c "C:\Users\your-name\path\to\your-project\load-balancer\nginx.conf"
```

## Testing Load Distribution

To test how the load is distributed:

```powershell
# Send 100 requests to the load balancer and view which service responds
for ($i = 0; $i -lt 100; $i++) { curl http://localhost:80/ }
```

Each API Gateway instance should include its port number in the response, confirming that NGINX is properly distributing requests across all three.

## Management Commands

To manage NGINX via PowerShell:

```powershell
# Reload config (without stopping the service)
.\nginx.exe -s reload

# Stop NGINX immediately
.\nginx.exe -s stop

# Gracefully shut down NGINX
.\nginx.exe -s quit
```

## Notes

- Confirm your API Gateway services are running on ports 5247–5249 before testing.
- Ensure your `nginx.conf` points to the correct upstream ports.
- Test with browser, curl, or Postman for basic checks; use `wrk` or `ab` for performance.
