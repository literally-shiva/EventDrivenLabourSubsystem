#!/bin/bash

# Event-Driven Labour Subsystem - Start All Services
# Запускает все 4 сервиса в отдельных терминалах

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=========================================="
echo "Event-Driven Labour Subsystem"
echo "Starting all services..."
echo "=========================================="

# Проверка PostgreSQL
echo "Checking PostgreSQL..."
if ! ss -tuln | grep -q 5432; then
    echo "ERROR: PostgreSQL is not running on port 5432"
    echo "Please start PostgreSQL first"
    exit 1
fi
echo "✓ PostgreSQL is running"

# Функция для запуска в новом терминале
start_service() {
    local name=$1
    local script=$2
    local port=$3

    echo "Starting $name on port $port..."

    # Попробуем разные терминалы
    if command -v gnome-terminal &> /dev/null; then
        gnome-terminal --title="$name" -- bash -c "cd '$PROJECT_DIR' && ./$script; exec bash"
    elif command -v xterm &> /dev/null; then
        xterm -T "$name" -e "cd '$PROJECT_DIR' && ./$script; bash" &
    elif command -v konsole &> /dev/null; then
        konsole --title "$name" -e bash -c "cd '$PROJECT_DIR' && ./$script; exec bash" &
    else
        echo "WARNING: No terminal emulator found. Starting in background..."
        cd "$PROJECT_DIR" && ./$script > "logs/$name.log" 2>&1 &
        echo "  Logs: logs/$name.log"
    fi

    sleep 2
}

# Создаем директорию для логов
mkdir -p "$PROJECT_DIR/logs"

# Запускаем сервисы в правильном порядке
echo ""
echo "1/4 Starting MLService..."
start_service "MLService" "start-mlservice.sh" "8000"

echo "2/4 Starting CoreServer..."
start_service "CoreServer" "start-coreserver.sh" "5000"

echo "3/4 Starting DigitalTwin..."
start_service "DigitalTwin" "start-digitaltwin.sh" "5001"

echo "4/4 Starting ProjectClient..."
start_service "ProjectClient" "start-projectclient.sh" "4200"

echo ""
echo "=========================================="
echo "All services started!"
echo "=========================================="
echo ""
echo "Services:"
echo "  MLService:      http://localhost:8000"
echo "  CoreServer:     http://localhost:5000"
echo "  DigitalTwin:    http://localhost:5001"
echo "  ProjectClient:  http://localhost:4200"
echo ""
echo "Open your browser: http://localhost:4200"
echo ""
echo "To stop all services, close the terminal windows or press Ctrl+C in each"
echo "=========================================="
