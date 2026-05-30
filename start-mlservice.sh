#!/bin/bash

echo "=========================================="
echo "Starting MLService on port 8000"
echo "=========================================="

cd "$(dirname "$0")/solution/MLService"

if [ ! -d ".venv" ]; then
    echo "Creating virtual environment..."
    python3 -m venv .venv
fi

source .venv/bin/activate

echo "Installing dependencies..."
pip install -q -r requirements.txt

echo "Starting uvicorn server..."
uvicorn app:app --host 0.0.0.0 --port 8000 --reload
