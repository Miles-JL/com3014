#!/bin/bash
set -e

echo "Starting API Gateway..."
dotnet api-gateway.dll
