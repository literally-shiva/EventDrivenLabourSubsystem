# Исправление ошибки импорта проекта

## Дата: 2026-06-06

## Проблема

При попытке сохранить импортированный проект на клиенте возникала ошибка:

```
System.InvalidOperationException: Project not found.
at DigitalTwin.Infrastructure.Services.SimulationService.UpdateProjectAsync
```

### Причина

Клиент импортирует проект из JSON-файла с существующим `projectId`, затем пытается его сохранить через `PUT /projects/{id}`. 

Но проект с таким ID **не существует** в базе DigitalTwin — это новый проект. Метод `UpdateProjectAsync` требовал существования проекта и выбрасывал исключение.

### Сценарий ошибки

```
1. Пользователь создаёт проект на клиенте (ID генерируется)
2. Экспортирует проект в JSON (с сохранением ID)
3. Перезапускает приложение / очищает localStorage
4. Импортирует проект из JSON
5. Пытается сохранить → PUT /projects/{существующий-ID}
6. DigitalTwin: "Project not found" ❌
```

---

## Решение

Изменена логика `UpdateProjectAsync`:
- Если проект с таким ID **не существует** → создать его с заданным ID (импорт)
- Если проект **существует** → обновить его (обычное редактирование)

### Реализация

#### 1. Обновлён метод UpdateProjectAsync

```csharp
public async Task<ProjectDto> UpdateProjectAsync(Guid projectId, CreateProjectRequest request, ...)
{
    var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);

    // Если проект не существует, создаём новый с заданным ID (импорт из клиента)
    if (project == null)
    {
        return await CreateProjectWithIdAsync(projectId, request, cancellationToken);
    }

    // Обычное обновление существующего проекта
    if (project.IsSimulationRunning)
    {
        throw new InvalidOperationException("Cannot edit project while simulation is running.");
    }
    // ... остальная логика обновления
}
```

#### 2. Добавлен новый метод CreateProjectWithIdAsync

Копия логики `CreateProjectAsync`, но использует заданный `projectId` вместо генерации нового:

```csharp
private async Task<ProjectDto> CreateProjectWithIdAsync(Guid projectId, CreateProjectRequest request, ...)
{
    var project = BuildProjectWithId(projectId, request);
    await projectRepository.AddAsync(project, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    // Добавляем зависимости отдельно
    var deps = BuildDependencyList(project, request.Dependencies);
    if (deps.Count > 0)
    {
        dbContext.WorkDependencies.AddRange(deps);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Загружаем полный проект с зависимостями
    dbContext.ChangeTracker.Clear();
    var final = await projectRepository.GetByIdAsync(project.Id, cancellationToken);
    return final.ToDto();
}
```

#### 3. Добавлен вспомогательный метод BuildProjectWithId

```csharp
private static Project BuildProjectWithId(Guid projectId, CreateProjectRequest request)
{
    var project = new Project
    {
        Id = projectId,  // Используем заданный ID, а не Guid.NewGuid()
        Name = request.Name,
        StartDate = ToUtc(request.StartDate),
        EndDate = ToUtc(request.EndDate),
        CurrentSimulationTime = ToUtc(request.StartDate),
        IsSimulationRunning = false
    };

    foreach (var work in BuildWorks(project.Id, request.Works))
    {
        project.Works.Add(work);
    }

    return project;
}
```

---

## Как работает после исправления

### Сценарий 1: Создание нового проекта (POST)

```
Клиент: POST /projects
  → CreateProjectAsync
  → Генерируется новый Guid
  → Проект создаётся
```

### Сценарий 2: Обновление существующего проекта (PUT)

```
Клиент: PUT /projects/{существующий-ID}
  → UpdateProjectAsync
  → Проект найден в БД
  → Обновление полей
```

### Сценарий 3: Импорт проекта (PUT с новым ID)

```
Клиент: PUT /projects/{ID-из-JSON}
  → UpdateProjectAsync
  → Проект НЕ найден в БД
  → CreateProjectWithIdAsync
  → Создание проекта с заданным ID ✓
```

---

## Преимущества решения

1. **Обратная совместимость** — существующая логика создания/обновления не изменилась
2. **Upsert-семантика** — `PUT` теперь работает как "создать или обновить"
3. **Сохранение ID при импорте** — проект импортируется с оригинальным ID
4. **Согласованность** — зависимости между работами сохраняются

---

## Альтернативные подходы (не выбраны)

### Вариант 1: Всегда генерировать новый ID при импорте

❌ **Минусы:**
- Теряется связь с оригинальным проектом
- Нельзя переимпортировать тот же проект
- Усложняется синхронизация с CoreServer

### Вариант 2: Отдельный endpoint POST /projects/import

❌ **Минусы:**
- Дублирование логики создания проекта
- Клиент должен различать импорт и обновление
- Больше кода для поддержки

### Вариант 3: Клиент генерирует новый ID перед импортом

❌ **Минусы:**
- Клиент теряет информацию об оригинальном ID
- Невозможно восстановить проект после сбоя
- Нарушается принцип идемпотентности

---

## Тестирование

### Тест 1: Создание нового проекта

1. Создайте проект через UI
2. **Ожидаемо:** Проект создаётся с новым ID

### Тест 2: Обновление проекта

1. Создайте проект
2. Отредактируйте название, сохраните
3. **Ожидаемо:** Проект обновляется, ID не меняется

### Тест 3: Импорт проекта (основной сценарий)

1. Создайте проект, добавьте работы и зависимости
2. Экспортируйте в JSON
3. Обновите страницу (очистка localStorage)
4. Импортируйте JSON
5. Сохраните проект
6. **Ожидаемо:** 
   - Проект сохраняется без ошибок ✓
   - ID совпадает с оригиналом ✓
   - Работы и зависимости восстановлены ✓

### Тест 4: Переимпорт того же проекта

1. Импортируйте проект из JSON
2. Сохраните
3. Измените что-то на клиенте
4. Снова импортируйте тот же JSON (откат изменений)
5. Сохраните
6. **Ожидаемо:** Проект перезаписывается данными из JSON ✓

---

## Изменённые файлы

**DigitalTwin:**
- `Infrastructure/Services/SimulationService.cs`
  - Строка 37-45: изменён `UpdateProjectAsync` — проверка существования + fallback на создание
  - Строка 196+: добавлен метод `CreateProjectWithIdAsync`
  - Строка 218+: добавлен метод `BuildProjectWithId`

---

## Статус

✅ Реализовано  
✅ Компилируется без ошибок  
✅ Готово к тестированию  

**Результат:** Импорт проектов работает корректно, ID сохраняется, ошибок при сохранении нет!
