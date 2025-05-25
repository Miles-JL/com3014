#!/bin/bash
set -e

# Wait for PostgreSQL to be ready
echo "Waiting for PostgreSQL to be ready..."
max_retries=30
retry_count=0

until PGPASSWORD=postgres psql -h "postgres" -U "postgres" -c '\q' 2>/dev/null; do
  retry_count=$((retry_count+1))
  >&2 echo "PostgreSQL is unavailable - sleeping (attempt $retry_count/$max_retries)"
  sleep 2
  
  if [ $retry_count -ge $max_retries ]; then
    >&2 echo "Failed to connect to PostgreSQL after $max_retries attempts. Exiting."
    exit 1
  fi
done

echo "PostgreSQL is up - starting realtime service..."
exec dotnet realtime-service.dll
