# CoreServer

Центральный ASP.NET Core backend: приём метрик, реестр событий, марковский анализ, пересчёт длительности и SignalR.

## Запуск

1. Поднять PostgreSQL и создать БД `core_server_db`.
2. Проверить строку подключения в [appsettings.json](/home/shiva/VsCodeProjects/EventDrivenLabourSubsystem/solution/CoreServer/src/CoreServer.API/appsettings.json:1).
3. Убедиться, что `MLService` доступен на `http://localhost:8000`.
4. Запустить:
   `dotnet run --project src/CoreServer.API`
5. Сервис будет доступен на `http://localhost:5000`.

## Основные endpoint'ы

- `POST /api/metrics`
- `POST /api/events/patterns`
- `GET /api/events/patterns`
- `POST /api/events/register-unknown`
- `GET /api/projects/{id}/timeline`
- `GET /api/projects/{id}/works`
- `GET /api/projects/{id}/events`
- SignalR hub: `/project-updates`
