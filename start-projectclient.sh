#!/bin/bash

echo "=========================================="
echo "Starting ProjectClient on port 4200"
echo "=========================================="

cd "$(dirname "$0")/solution/ProjectClient"

if [ ! -d "node_modules" ]; then
    echo "Installing npm dependencies..."
    npm install
fi

echo "Starting Angular dev server..."
npm start
