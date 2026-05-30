#!/bin/bash

echo "=========================================="
echo "Starting CoreServer on port 5000"
echo "=========================================="

cd "$(dirname "$0")/solution/CoreServer/src/CoreServer.API"

echo "Building project..."
dotnet build --nologo -v q

echo "Starting CoreServer..."
dotnet run --no-build
