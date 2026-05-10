# DigitalTwin

ASP.NET Core сервис цифрового двойника, который генерирует синтетические метрики проектных работ и отправляет их в `CoreServer`.

## Запуск

1. Поднять PostgreSQL и создать БД `digital_twin_db`.
2. Проверить строку подключения в [appsettings.json](/home/shiva/VsCodeProjects/EventDrivenLabourSubsystem/solution/DigitalTwin/src/DigitalTwin.API/appsettings.json:1).
3. Запустить:
   `dotnet run --project src/DigitalTwin.API`
4. Сервис будет доступен на `http://localhost:5001`.

## API

- `POST /projects`
- `GET /projects`
- `GET /projects/{id}`
- `POST /simulation/start/{projectId}`
- `POST /simulation/stop/{projectId}`
- `GET /simulation/state/{projectId}`

## Поведение

- при старте создаётся demo-project из 3 работ
- background service двигает виртуальное время по тикам
- на каждом тике генерируются `WorkMetricSnapshot`
- метрики отправляются в `CoreServer` на `POST /api/metrics`
