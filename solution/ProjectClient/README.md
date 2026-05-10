# ProjectClient

Angular SPA для визуализации работ, событий и длительностей.

## Запуск

1. Установить Node.js 20+.
2. Выполнить `npm install`.
3. Запустить `npm start`.
4. Клиент будет доступен на `http://localhost:4200`.

## Основные возможности

- загрузка project timeline из `CoreServer`
- визуализация работ через `DHTMLX Gantt`
- import/export JSON
- подписка на SignalR hub `/project-updates`
- модальное окно для регистрации unknown events
