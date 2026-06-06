# Диагностика проблем с графиком метрик

## Проблема: Линии графика не отображаются

### Возможные причины и решения

#### 1. Нет данных в MetricHistory

**Проверка:**
- Откройте консоль браузера (F12 → Console)
- Переключитесь на вкладку Metrics
- Проверьте логи:
  ```
  Loaded metrics: X records
  Sample metric: {...}
  ```

**Если `Loaded metrics: 0 records`:**
- Убедитесь, что симуляция запущена и работает
- Проверьте, что DigitalTwin отправляет данные: `http://localhost:5001/projects`
- Проверьте, что CoreServer получает метрики: `http://localhost:5000/api/projects/{projectId}/metrics`

#### 2. Поля метрик имеют значения null или undefined

**Проверка в консоли:**
```javascript
// Проверьте структуру данных
console.log('Sample metric:', metricsHistory[0]);
```

**Ожидаемая структура:**
```json
{
  "id": "...",
  "projectId": "...",
  "workId": "...",
  "workName": "Work 1",
  "timestamp": "2026-06-06T10:30:00Z",
  "collisionCount": 2,
  "changesCount": 1,
  "reworkCount": 1,
  "approvalCount": 3,
  "approvalDelayDays": 0,
  "documentationVersionCount": 2,
  "progressPercent": 15.5
}
```

**Если поля отсутствуют или null:**
- Проверьте, что CoreServer обновлён и пересобран
- Проверьте, что DigitalTwin генерирует эти поля в MetricsBatchRequest

#### 3. Все значения метрик равны 0

**Проверка в консоли:**
```javascript
// Должны быть ненулевые значения
Max values: {
  maxCollisions: 5,
  maxChanges: 5,
  maxRework: 5,
  maxApprovalDelays: 5,
  maxDocVersions: 5,
  maxApprovals: 5,
  maxValue: 10
}
```

**Если все max = 5 (минимальное значение):**
- Симуляция ещё не сгенерировала события
- Подождите 1-2 минуты после запуска симуляции
- Проверьте список событий (Detected events) — должны появиться события

#### 4. Canvas не инициализирован

**Проверка:**
- В консоли не должно быть ошибок `Cannot read property 'getContext' of null`
- Проверьте, что элемент `<canvas #metricsChart>` существует в DOM

**Решение:**
- Переключитесь на вкладку Gantt, затем обратно на Metrics
- Обновите страницу (F5)

## Пошаговая диагностика

### Шаг 1: Проверка сервисов

```bash
# CoreServer должен быть запущен на порту 5000
curl http://localhost:5000/health

# DigitalTwin должен быть запущен на порту 5001
curl http://localhost:5001/projects

# MLService должен быть запущен на порту 5002
curl http://localhost:5002/health
```

### Шаг 2: Проверка данных проекта

```bash
# Получить ID проекта из DigitalTwin
curl http://localhost:5001/projects

# Проверить метрики проекта (замените {projectId})
curl http://localhost:5000/api/projects/{projectId}/metrics

# Проверить события проекта
curl http://localhost:5000/api/projects/{projectId}/events
```

### Шаг 3: Проверка симуляции

```bash
# Запустить симуляцию (замените {projectId})
curl -X POST http://localhost:5001/simulation/start/{projectId}

# Подождать 30 секунд, затем проверить метрики снова
sleep 30
curl http://localhost:5000/api/projects/{projectId}/metrics
```

### Шаг 4: Проверка в браузере

1. Откройте http://localhost:4200
2. Откройте консоль браузера (F12)
3. Выберите проект
4. Запустите симуляцию
5. Подождите 1-2 минуты
6. Переключитесь на вкладку Metrics
7. Проверьте логи в консоли:
   - `Loaded metrics: X records` (X должен быть > 0)
   - `Sample metric: {...}` (поля должны быть заполнены)
   - `Max values: {...}` (хотя бы одно значение > 5)

## Частые ошибки

### Ошибка: "Cannot read property 'collisionCount' of undefined"

**Причина:** Данные не загружены или имеют неправильную структуру.

**Решение:**
1. Проверьте сетевые запросы в DevTools → Network
2. Найдите запрос к `/api/projects/{id}/metrics`
3. Проверьте ответ сервера — должен быть массив объектов

### Ошибка: "Failed to load metrics: 404"

**Причина:** CoreServer не запущен или эндпоинт не найден.

**Решение:**
1. Перезапустите CoreServer
2. Убедитесь, что используется обновлённая версия с новым эндпоинтом

### Ошибка: Canvas пустой, события отображаются

**Причина:** Все метрики имеют значение 0 или поля отсутствуют.

**Решение:**
1. Проверьте структуру данных в консоли
2. Убедитесь, что симуляция запущена и генерирует события
3. Подождите накопления данных (1-2 минуты)

## Проверка версий компонентов

### CoreServer

Файл должен содержать новые поля:
```bash
grep -A 5 "MetricHistoryDto" solution/CoreServer/src/CoreServer.Application/DTOs/MetricsDtos.cs
```

Должны присутствовать:
- `ApprovalCount`
- `DocumentationVersionCount`

### DigitalTwin

Проверьте, что генерируются все поля метрик:
```bash
grep -A 20 "def generate_metrics" solution/DigitalTwin/app.py
```

### ProjectClient

Проверьте TypeScript интерфейс:
```bash
grep -A 10 "interface MetricHistory" solution/ProjectClient/src/app/models.ts
```

Должны присутствовать все 6 метрик.

## Успешная конфигурация

При правильной работе вы должны видеть:

1. **В консоли браузера:**
   ```
   Loaded metrics: 15 records
   Sample metric: {collisionCount: 2, changesCount: 1, ...}
   Max values: {maxCollisions: 5, maxChanges: 3, maxValue: 10}
   ```

2. **На графике:**
   - 6 разноцветных линий
   - Маркеры событий (точки внизу)
   - Даты по горизонтали
   - Значения по вертикали

3. **В легенде:**
   - 6 элементов с цветными индикаторами
   - Русские названия метрик

## Контакты для отладки

Если проблема не решается:
1. Проверьте логи CoreServer
2. Проверьте логи DigitalTwin
3. Сохраните снимок экрана консоли браузера
4. Сохраните ответ API `/api/projects/{id}/metrics`
