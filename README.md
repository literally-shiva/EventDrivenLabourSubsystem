# EventDrivenLabourSubsystem

Fullstack prototype для магистерской работы:
`Подсистема управления трудозатратами на стадии проектирования на основе обработки событий`.

## Структура

- [solution/DigitalTwin/README.md](/home/shiva/VsCodeProjects/EventDrivenLabourSubsystem/solution/DigitalTwin/README.md:1)
- [solution/CoreServer/README.md](/home/shiva/VsCodeProjects/EventDrivenLabourSubsystem/solution/CoreServer/README.md:1)
- [solution/MLService/README.md](/home/shiva/VsCodeProjects/EventDrivenLabourSubsystem/solution/MLService/README.md:1)
- [solution/ProjectClient/README.md](/home/shiva/VsCodeProjects/EventDrivenLabourSubsystem/solution/ProjectClient/README.md:1)

## Порты

- `CoreServer`: `http://localhost:5000`
- `MLService`: `http://localhost:8000`
- `ProjectClient`: `http://localhost:4200`
- `DigitalTwin`: `http://localhost:5001`

## Порядок запуска

1. Запустить PostgreSQL.
2. Запустить `MLService`.
3. Запустить `CoreServer`.
4. Запустить `DigitalTwin`.
5. Запустить `ProjectClient`.
