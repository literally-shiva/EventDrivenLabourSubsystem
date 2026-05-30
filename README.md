# EventDrivenLabourSubsystem

Fullstack prototype для магистерской работы:
**Подсистема управления трудозатратами на стадии проектирования на основе обработки событий**

## Архитектура

Система состоит из 4 независимых проектов:

1. **DigitalTwin** (.NET) - генератор синтетических данных цифрового двойника
2. **CoreServer** (.NET) - центральный сервер обработки событий и управления
3. **MLService** (Python) - сервис машинного обучения (DBSCAN + SVM)
4. **ProjectClient** (Angular) - веб-клиент с визуализацией Gantt

## Требования

- .NET 10.0 SDK
- PostgreSQL 14+ (запущен на localhost:5432)
- Python 3.10+ с venv
- Node.js 18+ и npm
- Angular CLI 18+

## Быстрый старт

### 1. Запуск MLService (Python)

```bash
cd solution/MLService
source .venv/bin/activate
pip install -r requirements.txt  # если еще не установлено
uvicorn app:app --host 0.0.0.0 --port 8000
```

### 2. Запуск CoreServer (.NET)

```bash
cd solution/CoreServer/src/CoreServer.API
dotnet run
```

### 3. Запуск DigitalTwin (.NET)

```bash
cd solution/DigitalTwin/src/DigitalTwin.API
dotnet run
```

### 4. Запуск ProjectClient (Angular)

```bash
cd solution/ProjectClient
npm install  # только при первом запуске
npm start
```

## Порты

- **CoreServer**: `http://localhost:5000`
- **MLService**: `http://localhost:8000`
- **ProjectClient**: `http://localhost:4200`
- **DigitalTwin**: `http://localhost:5001`

## Порядок запуска

**Важно:** Запускайте сервисы строго в этом порядке:

1. PostgreSQL (должен быть уже запущен)
2. MLService (порт 8000)
3. CoreServer (порт 5000) - зависит от MLService
4. DigitalTwin (порт 5001) - зависит от CoreServer
5. ProjectClient (порт 4200) - зависит от CoreServer и DigitalTwin

## Использование

1. Откройте браузер: `http://localhost:4200`
2. Создайте новый проект или выберите существующий
3. Добавьте работы и настройте зависимости в Gantt диаграмме
4. Сохраните проект
5. Запустите симуляцию (Start Simulation)
6. Наблюдайте за обработкой событий в реальном времени через SignalR

## Математические методы

- **DBSCAN** - кластеризация метрик для выявления событий
- **SVM (RBF kernel)** - классификация известных/неизвестных событий
- **Марковские цепи** - моделирование состояний устойчивости работ (S0-S4)

## Подробная документация

- [solution/DigitalTwin/README.md](solution/DigitalTwin/README.md)
- [solution/CoreServer/README.md](solution/CoreServer/README.md)
- [solution/MLService/README.md](solution/MLService/README.md)
- [solution/ProjectClient/README.md](solution/ProjectClient/README.md)

## База данных

PostgreSQL создает две базы автоматически при первом запуске:
- `digital_twin_db` - для DigitalTwin
- `core_server_db` - для CoreServer

Credentials: `postgres/postgres`
