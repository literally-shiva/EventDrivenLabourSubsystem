# Убрано модальное окно классификации событий

## Дата: 2026-06-06

## Проблема

При появлении неизвестного события система **автоматически открывала модальное окно** с предложением его классифицировать. Это мешало работе и прерывало просмотр графиков и работы с проектом.

### Сценарий проблемы

```
1. Пользователь работает с проектом
2. Симуляция генерирует неизвестное событие
3. Автоматически появляется модальное окно ❌
4. Пользователь вынужден либо назвать событие, либо закрыть окно
5. Процесс прерван, фокус потерян
```

---

## Решение

Реализовано **инлайн-редактирование** имени события прямо в панели событий:
- Убрано модальное окно
- Убран автоматический показ диалога при появлении неизвестного события
- Добавлена возможность кликнуть на неизвестное событие в списке и отредактировать его имя
- Редактирование происходит inline с автофокусом на input

### Новый UX

```
1. Неизвестные события отображаются в списке с подчёркнутым именем (синий цвет)
2. При клике на событие появляется input для ввода имени
3. Enter → сохранить, Escape → отменить
4. После сохранения событие становится "известным" (isKnown = true)
5. Никаких прерываний работы!
```

---

## Реализация

### 1. Добавлены новые свойства в AppComponent

```typescript
editingEventId?: string;      // ID редактируемого события
editingEventName = '';        // Временное имя при редактировании
```

### 2. Добавлены методы для инлайн-редактирования

#### startEditingEvent(event: DetectedEvent)
Начинает редактирование события:
```typescript
startEditingEvent(event: DetectedEvent): void {
  this.editingEventId = event.id;
  this.editingEventName = event.name || '';
}
```

#### saveEventName(event: DetectedEvent)
Сохраняет новое имя события:
```typescript
saveEventName(event: DetectedEvent): void {
  if (!this.editingEventName.trim()) {
    this.cancelEventEdit();
    return;
  }

  this.api.registerUnknownEvent({
    workId: event.workId,
    projectId: event.projectId,
    name: this.editingEventName.trim(),
    vector: event.featureVector ?? []
  }).subscribe(() => {
    // Обновляем событие локально
    event.name = this.editingEventName.trim();
    event.isKnown = true;
    this.editingEventId = undefined;
    this.editingEventName = '';
  });
}
```

#### cancelEventEdit()
Отменяет редактирование:
```typescript
cancelEventEdit(): void {
  this.editingEventId = undefined;
  this.editingEventName = '';
}
```

### 3. Обновлён шаблон панели событий

```html
<div class="event-item" *ngFor="let event of timeline.events">
  <div class="event-item-header">
    <span class="event-dot event-marker event-marker--{{ getEventMarkerClass(event.eventType) }}"></span>
    
    <!-- Режим редактирования -->
    <strong *ngIf="!event.isKnown && editingEventId === event.id">
      <input
        [(ngModel)]="editingEventName"
        (keyup.enter)="saveEventName(event)"
        (keyup.escape)="cancelEventEdit()"
        placeholder="Event name"
        class="event-name-input"
        autofocus />
    </strong>
    
    <!-- Обычный режим -->
    <strong
      *ngIf="event.isKnown || editingEventId !== event.id"
      (click)="!event.isKnown && startEditingEvent(event)"
      [class.editable]="!event.isKnown"
      [title]="!event.isKnown ? 'Click to name this event' : ''">
      {{ event.name || event.eventType }}
    </strong>
    
    <span class="event-confidence">{{ (event.confidence * 100) | number:'1.0-0' }}%</span>
  </div>
  <span class="muted">{{ getWorkName(event.workId) }}</span>
  <span class="muted">{{ event.timestamp | date:'dd.MM HH:mm' }}</span>
</div>
```

### 4. Убрано модальное окно

**Удалено из шаблона:**
```html
<div class="modal-backdrop" *ngIf="unknownEvent">
  <div class="modal">
    <h3>Unknown event detected</h3>
    <!-- ... -->
  </div>
</div>
```

### 5. Убрана автоматическая подписка на unknownEventDetected

**Было:**
```typescript
this.signalr.unknownEventDetected$.subscribe(eventItem => {
  if (this.activeProject?.id !== eventItem.projectId) {
    return;
  }
  this.unknownEvent = eventItem;
  this.unknownEventName = '';
});
```

**Стало:**
```typescript
// Автоматическое модальное окно убрано - пользователь именует события в списке
```

### 6. Добавлены стили для инлайн-редактирования

```css
/* Редактируемое событие - подчёркнутое синее */
.event-item-header strong.editable {
  cursor: pointer;
  color: #1d4ed8;
  text-decoration: underline dotted;
}

.event-item-header strong.editable:hover {
  color: #1e40af;
}

/* Input для редактирования */
.event-name-input {
  width: 100%;
  padding: 4px 8px;
  border: 1px solid #1d4ed8;
  border-radius: 4px;
  font-size: 13px;
  font-weight: 600;
  outline: none;
}

.event-name-input:focus {
  border-color: #1e40af;
  box-shadow: 0 0 0 3px rgba(29, 78, 216, 0.1);
}
```

---

## Визуальное отличие событий

### Известное событие (isKnown = true)
```
🔴 CollisionDetected          95%
   Work 1
   06.06 14:30
```
- Обычный текст, чёрный цвет
- Не кликабельно

### Неизвестное событие (isKnown = false)
```
🟠 Unknown                     78%
   ˜˜˜˜˜˜˜  (подчёркнутое синее)
   Work 2
   06.06 14:35
```
- Подчёркнутый синий текст
- Курсор pointer
- Tooltip: "Click to name this event"

### Событие в режиме редактирования
```
🟠 [_________________]         78%
     (input с фокусом)
   Work 2
   06.06 14:35
```
- Input field с автофокусом
- Enter — сохранить
- Escape — отменить

---

## Преимущества решения

### До (модальное окно)
❌ Прерывает работу  
❌ Блокирует интерфейс  
❌ Навязывает немедленную классификацию  
❌ Нельзя отложить именование  
❌ Нельзя видеть другие события

### После (инлайн-редактирование)
✅ Не прерывает работу  
✅ Не блокирует интерфейс  
✅ Можно отложить именование  
✅ Можно именовать события в любой момент  
✅ Видны все события в контексте  
✅ Быстрое редактирование (клик → ввод → Enter)

---

## Пользовательский сценарий

### Сценарий 1: Именование сразу

1. В панели появляется новое событие (синее подчёркнутое)
2. Пользователь кликает на него
3. Появляется input с автофокусом
4. Вводит имя: "Задержка согласования"
5. Нажимает Enter
6. Событие сохраняется и становится обычным

### Сценарий 2: Именование позже

1. В панели появляется несколько неизвестных событий
2. Пользователь продолжает работу (события не мешают)
3. Позже просматривает список
4. Кликает на интересное событие
5. Именует его
6. Продолжает работу

### Сценарий 3: Отмена редактирования

1. Пользователь кликает на событие
2. Появляется input
3. Начинает вводить имя
4. Передумал — нажимает Escape
5. Input исчезает, событие остаётся неизвестным

---

## Изменённые файлы

### Frontend (ProjectClient)
- `src/app/app.component.ts`
  - Строка 33-34: добавлены `editingEventId` и `editingEventName`
  - Строка 84-90: убрана подписка на `unknownEventDetected$`
  - Строка 420+: добавлены методы `startEditingEvent`, `saveEventName`, `cancelEventEdit`
  
- `src/app/app.component.html`
  - Строка 120-143: обновлён шаблон события с инлайн-редактированием
  - Строка 196-206: удалено модальное окно
  
- `src/app/app.component.css`
  - Строка 164+: добавлены стили `.editable` и `.event-name-input`

---

## Тестирование

### Тест 1: Именование неизвестного события

1. Запустите симуляцию
2. Дождитесь появления неизвестного события (синее подчёркнутое)
3. Кликните на него
4. Введите имя: "Тестовое событие"
5. Нажмите Enter
6. **Ожидаемо:** Событие сохраняется, становится обычным (чёрный текст)

### Тест 2: Отмена редактирования

1. Кликните на неизвестное событие
2. Начните вводить имя
3. Нажмите Escape
4. **Ожидаемо:** Input исчезает, событие остаётся неизвестным

### Тест 3: Множественные события

1. Дождитесь нескольких неизвестных событий
2. Именуйте их по одному
3. **Ожидаемо:** Каждое событие сохраняется независимо, модальное окно не появляется

### Тест 4: Работа без прерываний

1. Запустите симуляцию
2. Работайте с графиком Gantt
3. В фоне генерируются неизвестные события
4. **Ожидаемо:** Никаких модальных окон, работа не прерывается

---

## Известные ограничения

1. **Нет индикации новых событий** — пользователь должен сам заметить новое событие в списке
2. **Нет автосохранения** — если пользователь вводит имя и кликает вне input (не нажав Enter), изменения теряются
3. **Нет валидации имени** — можно ввести пустое имя или очень длинное

---

## Возможные улучшения (не реализовано)

### 1. Тихое уведомление
Добавить небольшой badge с количеством неименованных событий:
```
Detected events [2]  ← badge
```

### 2. Автосохранение при потере фокуса
```typescript
<input (blur)="saveEventName(event)" />
```

### 3. Предложения имён
Показывать список часто используемых имён событий:
```
┌────────────────────────┐
│ Задержка согласования  │
│ Изменение ТЗ           │
│ Нехватка ресурсов      │
└────────────────────────┘
```

### 4. Bulk naming
Возможность выбрать несколько событий и дать им одно имя.

---

## Статус

✅ Модальное окно убрано  
✅ Инлайн-редактирование реализовано  
✅ Стили добавлены  
✅ Автоматический показ диалога отключён  
✅ Готово к тестированию  

**Результат:** Пользователь больше не прерывается модальными окнами, может именовать события в удобное время прямо в списке!
