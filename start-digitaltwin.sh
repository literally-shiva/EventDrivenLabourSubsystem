#!/bin/bash

echo "=========================================="
echo "Starting DigitalTwin on port 5001"
echo "=========================================="

cd "$(dirname "$0")/solution/DigitalTwin/src/DigitalTwin.API"

echo "Building project..."
dotnet build --nologo -v q

echo "Starting DigitalTwin..."
dotnet run --no-build
