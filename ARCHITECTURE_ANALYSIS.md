# ARCHITECTURE_ANALYSIS.md
# Полный аудит EventDrivenLabourSubsystem

> Дата аудита: 2026-06-06  
> Роль: Principal Software Architect + Senior ML Engineer  
> База: PROJECT_MEMORY.md + полное чтение исходного кода + научное описание модели

---

## 1. Общая оценка архитектуры

**Общий вердикт: частичное соответствие научной модели.**

Архитектурный каркас правильный: DBSCAN → SVM → цепь Маркова → пересчёт трудозатрат — последовательность соблюдена. Реализованы все ключевые сервисы, поток данных замкнут, система функционирует end-to-end.

Однако существуют **3 критических несоответствия** научной модели, **4 алгоритмические проблемы** и **5 ошибок реализации**, которые влияют на корректность результатов и степень соответствия диссертационной концепции.

---

## 2. Критические ошибки

### КО-1. Бустинг Маркова применяется к константной матрице без её обновления — нарушение научной модели

**Критичность: ВЫСОКАЯ**

**Описание.** В научной модели описано: *«Изначально коэффициенты задаются экспертно. По мере накопления исторических данных коэффициенты корректируются автоматически.»*

В реализации матрица переходов `Matrix` в [MarkovStateEngine.cs:9-16](solution/CoreServer/src/CoreServer.Infrastructure/Services/MarkovStateEngine.cs#L9) объявлена как `static readonly double[,]` и никогда не обновляется. Нет никакого кода, который читал бы историю состояний и перестраивал матрицу.

**Последствия.** Марковская модель навсегда остаётся экспертной оценкой. Диссертационный тезис об адаптивности системы не подтверждается кодом.

**Рекомендация.** Добавить метод `UpdateMatrixAsync` в `IMarkovStateEngine`, который вычисляет эмпирическую матрицу переходов из истории `WorkMarkovState` по правилу:

```csharp
// Псевдокод: подсчёт переходов из записей WorkMarkovState
// P[i,j] = count(переходов из Si в Sj) / count(всех переходов из Si)
// Если count(из Si) == 0 — использовать prior (текущую строку матрицы)
```

Хранить матрицу в БД (новая таблица `MarkovTransitionMatrix` или JSON-поле в настройках) и загружать при старте.

---

### КО-2. Коэффициенты состояний в DurationRecalculationEngine не соответствуют значениям научной модели

**Критичность: ВЫСОКАЯ**

**Описание.** В научной модели коэффициенты влияния состояний задаются как:
```
S0 = 1.0, S1 = 1.1, S2 = 1.3, S3 = 1.6, S4 = 2.0
```

В реализации [DurationRecalculationEngine.cs:17-24](solution/CoreServer/src/CoreServer.Infrastructure/Services/DurationRecalculationEngine.cs#L17) объявлены принципиально другие значения:
```
S1 = 1.05, S2 = 1.12, S3 = 1.22, S4 = 1.35
```

**Последствия.** Реальный прирост трудозатрат в критических состояниях занижен в 1.5–2 раза относительно модели. При S4 модель даёт ×2.0 (удвоение длительности), реализация — ×1.35. Это меняет поведение системы принципиально: реальные нарушения сроков будут систематически недооцениваться.

**Рекомендация.** Исправить коэффициенты в соответствии с научной моделью:

```csharp
// DurationRecalculationEngine.cs
var stateImpact = state?.CurrentState switch
{
    WorkStabilityState.S1LowSensitive    => 1.1,
    WorkStabilityState.S2MediumSensitive => 1.3,
    WorkStabilityState.S3HighSensitive   => 1.6,
    WorkStabilityState.S4Critical        => 2.0,
    _                                    => 1.0
};
```

Также согласно описанию «коэффициенты корректируются автоматически» — требуется механизм накопления и обновления. Минимальная реализация: хранить коэффициенты в таблице `StateImpactCoefficients` и обновлять их, например, по среднему фактическому отношению `ActualDuration / PlannedDuration` в каждом состоянии.

---

### КО-3. Формула расчёта длительности нарушает физический смысл модели

**Критичность: ВЫСОКАЯ**

**Описание.** В научной модели описан поток:  
`Событие → изменение состояния → состояние определяет степень влияния`  

Это означает: событие сначала меняет состояние через цепь Маркова, затем **новое состояние** определяет коэффициент. В реализации:

1. [MetricsProcessingService.cs:145](solution/CoreServer/src/CoreServer.Infrastructure/Services/MetricsProcessingService.cs#L145) — `ApplyEventAsync` — Марков применяет переход, сохраняет новое состояние  
2. [MetricsProcessingService.cs:148](solution/CoreServer/src/CoreServer.Infrastructure/Services/MetricsProcessingService.cs#L148) — `RecalculateAsync` — читает состояние из БД

Формула: `newDuration = previousDuration × eventImpact × stateImpact`

Проблема двойного умножения: при каждом событии duration умножается **дважды** — на `eventImpact` (влияние типа события) **и** на `stateImpact` (коэффициент состояния). В научной модели это описано как два **независимых** канала влияния, но они применяются одновременно в одной операции. Смысловая конфликт: если состояние S4 и событие ExpertReviewFailed, прирост составит ×1.35×1.20 = ×1.62 (по текущим коэффициентам) или ×2.0×1.20 = ×2.4 по научной модели. При двух событиях подряд: ×2.4² = ×5.76 от исходной длительности — бесконечный рост без верхней границы.

**Дополнительная проблема:** нет ограничения сверху (`MaxDuration`). После нескольких событий в состоянии S4 длительность работы может превысить реалистичные значения.

**Рекомендация.** Разделить два вектора влияния:

```csharp
// Вариант 1: состояние определяет максимальный потенциал задержки,
// тип события — долю реализации этого потенциала
var maxImpact   = GetStateImpact(state);    // S4 = 2.0 — верхняя граница
var eventShare  = GetEventShare(eventType); // доля от [1.0, maxImpact]
var newDuration = Math.Min(
    previousDuration * (1 + (maxImpact - 1) * eventShare),
    work.PlannedDuration * maxImpact        // жёсткий потолок
);

// Вариант 2 (проще, соответствует описанию): только stateImpact
// а тип события определяет вероятность перехода в худшее состояние,
// но НЕ отдельный числовой коэффициент
var newDuration = previousDuration * stateImpact;
```

Также необходим `MaxDuration = PlannedDuration × 3.0` как жёсткая верхняя граница.

---

## 3. Архитектурные недостатки

### АН-1. Признаковое пространство реализации уже описанного в научном контексте

**Критичность: СРЕДНЯЯ**

**Описание.** В научной модели описаны показатели:
- количество коллизий ✓  
- количество замечаний экспертизы ✓ (reworkCount — частично)  
- количество изменений ТЗ ✓ (changesCount)  
- количество возвратов на доработку ✓ (reworkCount)  
- количество несогласованных решений — **отсутствует**  
- количество документов на согласовании — **отсутствует**  
- количество нормативных несоответствий — **отсутствует**  

В реализации вектор: `[workersCount, modelDataVolume, changesCount, collisionCount, approvalDelayDays, reworkCount]`  

Признак `workersCount` используется для косвенного обнаружения `ResourceShortage` (в DigitalTwin при событии ResourceShortage `workersCount` снижается на 2). Это **не** описанный показатель проектного процесса — это организационная характеристика, не производная цифрового двойника BIM.  

`modelDataVolume` — синтетический признак, вычисляемый как `150 + progress*4.5 + rand*50`. Он коррелирует с прогрессом, но не несёт событийной информации — он фактически является скрытой дубликацией `ProgressPercent`.

**Последствия.** Часть признаков не несут уникальной событийной информации (modelDataVolume), часть важных признаков отсутствуют.

**Рекомендация.** По возможности расширить вектор признаков и исключить `modelDataVolume` или явно обосновать его семантику в диссертации.

---

### АН-2. MLService не имеет персистентности — состояние SVM теряется при рестарте

**Критичность: СРЕДНЯЯ**

**Описание.** Обученная SVM хранится только в памяти Python-процесса (`ModelRegistry` в [app.py:65-74](solution/MLService/app.py#L65)). CoreServer пересобирает её при каждом старте из seed-данных, что означает: если CoreServer перезапущен без MLService, стартует без модели; если MLService перезапущен, теряет все пользовательски обученные паттерны до рестарта CoreServer.

**Рекомендация.** Добавить `joblib.dump/load` для персистентности модели на диск. Это одна строка кода:

```python
import joblib, pathlib
MODEL_PATH = pathlib.Path("./model.joblib")

# После train:
joblib.dump(registry.classifier, MODEL_PATH)

# При старте:
if MODEL_PATH.exists():
    registry.classifier = joblib.load(MODEL_PATH)
```

---

### АН-3. Синхронная цепочка HTTP-вызовов создаёт узкое место под нагрузкой

**Критичность: НИЗКАЯ (для прототипа)**

**Описание.** На каждый тик (каждые 3с) CoreServer выполняет:  
`1× POST /cluster → N× POST /classify` где N — число кандидатов.  
При росте числа кандидатов задержка растёт линейно и блокирует обработку следующего батча.

**Рекомендация.** Для прототипа допустимо. В продакшн-версии: батчевый `/classify-all` или параллельные вызовы через `Task.WhenAll`.

---

## 4. Алгоритмические недостатки

### АА-1. DBSCAN получает только N точек текущего батча — кластеризация на 2–3 точках математически некорректна

**Критичность: ВЫСОКАЯ**

**Описание.** DBSCAN вызывается в [MetricsProcessingService.cs:99-108](solution/CoreServer/src/CoreServer.Infrastructure/Services/MetricsProcessingService.cs#L99) с массивом `request.Metrics`, который содержит **только точки текущего батча** — по одной точке на каждую активную работу. В демо-проекте это 3 точки (Work1, Work2, Work3).

DBSCAN с `min_samples=2` на 3 точках:
- Если все 3 точки близки → один кластер = норма, кандидатов нет  
- Если одна точка отдалена → 2 точки в кластере (норма), 1 шум (кандидат)  

**Математическая проблема № 1:** DBSCAN на 3 точках не обнаруживает структуру — он обнаруживает изоляцию. Это не кластеризация в статистическом смысле, а поиск точки, непохожей на две другие.

**Математическая проблема № 2:** DBSCAN не сравнивает текущее состояние с историческими нормальными состояниями. Если на протяжении нескольких тиков все работы имеют аномальные метрики — DBSCAN объявит их нормальными, потому что они близки друг к другу.

**Корректная реализация согласно научной модели:** передавать в DBSCAN **скользящее окно исторических точек** за K последних тиков для каждой работы, чтобы алгоритм отличал текущее состояние от типичного исторического.

```csharp
// Вместо только текущего батча — последние K снапшотов из MetricHistory
var historicalPoints = await metricRepository
    .GetRecentByWorkAsync(workId, windowSize: 30, cancellationToken);
var allPoints = historicalPoints.Append(currentPoint).ToArray();
// Затем передать allPoints в /cluster
// Текущая точка = кандидат, если попала не в largest cluster
```

---

### АА-2. Параметры DBSCAN (eps=1.2, min_samples=2) не масштабированы и не обоснованы

**Критичность: СРЕДНЯЯ**

**Описание.** В [app.py:101](solution/MLService/app.py#L101): `DBSCAN(eps=1.2, min_samples=2)`.

`StandardScaler` применяется корректно перед DBSCAN. Однако `eps=1.2` в нормализованном пространстве означает фиксированный радиус 1.2 стандартного отклонения. В 6-мерном пространстве при манхэттенском смысле расстояния (евклидово по умолчанию) это покрывает около 78% объёма — слишком широко для обнаружения аномалий.

В практических приложениях `eps` для DBSCAN подбирается через k-NN distance plot (расстояние до k-го ближайшего соседа, "локоть" на графике). Текущее значение — произвольное.

**Рекомендация.** Добавить эндпоинт `/suggest-eps` или автоматическую оценку eps через 4-NN plot:

```python
from sklearn.neighbors import NearestNeighbors
nbrs = NearestNeighbors(n_neighbors=4).fit(normalized)
distances, _ = nbrs.kneighbors(normalized)
# eps = значение "локтя" на sorted distances[:, 3]
```

Для диссертации достаточно зафиксировать обоснование выбора eps в тексте.

---

### АА-3. SVM переобучается на слабо-диверсифицированной выборке (12 шаблонов по 2 на класс)

**Критичность: СРЕДНЯЯ**

**Описание.** Начальная обучающая выборка — 12 точек, по 2 на каждый из 6 классов. SVM с RBF-ядром на 2 точках каждого класса **детально запоминает** обе точки и не обобщает. Это классическое переобучение.

Дополнительная проблема: в заготовленных векторах `DocumentationReturned` и `ExpertReviewFailed` искусственно похожи:
```
DocumentationReturned Pattern A: [6, 500, 2, 0, 2, 4]
ExpertReviewFailed Pattern A:    [6, 510, 1, 0, 3, 4]
```
Разница в 10 единицах modelDataVolume и 1 единице approvalDelayDays — после StandardScaler они неразличимы при таком объёме выборки.

**Последствия.** Классификатор будет систематически путать `DocumentationReturned` и `ExpertReviewFailed`.

**Рекомендация.**
1. Увеличить начальное число шаблонов до 5–10 на класс с разнообразными значениями.
2. Добавить `class_weight='balanced'` в `SVC(...)` для устойчивости к дисбалансу.
3. Добавить кросс-валидацию при обучении и логировать метрику качества:

```python
from sklearn.model_selection import cross_val_score
scores = cross_val_score(pipeline, x, y, cv=min(5, min_class_count), scoring='f1_weighted')
```

---

### АА-4. Порог isKnown = 0.35 слишком низкий, порождает ложно-известные события

**Критичность: СРЕДНЯЯ**

**Описание.** В [app.py:139](solution/MLService/app.py#L139): `is_known = confidence >= 0.35`.

SVM с `probability=True` использует Platt scaling, который на малых датасетах даёт плохо откалиброванные вероятности. При 6 классах случайный классификатор даёт `max_proba ≈ 0.17`. Порог 0.35 превышает случайный уровень лишь в 2 раза — это очень нестрогий критерий.

**Последствие.** Многие неизвестные события будут ошибочно маркированы как известные, пользователь не получит запрос на разметку, обучающая выборка не пополняется.

**Рекомендация.** Поднять порог до 0.55–0.65 и предоставить его как конфигурируемый параметр. Также логировать `confidence` распределение для калибровки.

---

## 5. Ошибки реализации

### ОР-1. RegisterUnknownEventAsync не принимает произвольную метку от пользователя — принимает только enum-совпадения

**Критичность: СРЕДНЯЯ**

**Описание.** В [EventRegistryService.cs:36-38](solution/CoreServer/src/CoreServer.Infrastructure/Services/EventRegistryService.cs#L36):

```csharp
var eventType = Enum.TryParse<EventType>(request.Name, ignoreCase: true, out var parsed)
    ? parsed
    : EventType.Unknown;
```

Если пользователь вводит новое имя события (например, "НормативноеНесоответствие"), которое не совпадает с `EventType` enum — оно сохраняется как `EventType.Unknown`. Затем SVM обучается с меткой `"Unknown"`, а не с пользовательской меткой.

Согласно научной модели, пользователь должен создавать **новые типы событий**. Текущая реализация закрывает эту возможность — вся логика зациклена на фиксированном enum из 6 типов.

**Рекомендация.** Хранить `EventType` как строку, а не enum. Заменить enum на `string EventTypeName` в `EventPattern` и `DetectedEvent`. SVM работает со строковыми метками нативно, никаких изменений в Python не требуется.

---

### ОР-2. Markov SaveChanges вызывается внутри ApplyEventAsync — двойной save в MetricsProcessingService

**Критичность: НИЗКАЯ**

**Описание.** `MarkovStateEngine.ApplyEventAsync` [MarkovStateEngine.cs:33](solution/CoreServer/src/CoreServer.Infrastructure/Services/MarkovStateEngine.cs#L33) вызывает `unitOfWork.SaveChangesAsync`. Затем `DurationRecalculationEngine.RecalculateAsync` [DurationRecalculationEngine.cs:50](solution/CoreServer/src/CoreServer.Infrastructure/Services/DurationRecalculationEngine.cs#L50) тоже вызывает `SaveChangesAsync`. Затем `MetricsProcessingService` в строке 183 снова вызывает `SaveChangesAsync`. Итого 2N+1 вызовов SaveChanges на батч (где N — число кандидатов).

**Последствие.** Лишние round-trip'ы к БД; частичная работа с незавершёнными транзакциями.

**Рекомендация.** Убрать `SaveChangesAsync` из движков, оставить один вызов в `MetricsProcessingService` в конце батча.

---

### ОР-3. Seed-данные создаются с одинаковыми Guid между CoreServer и DigitalTwin — нет гарантии совпадения WorkId

**Критичность: СРЕДНЯЯ**

**Описание.** При первом запуске CoreServer создаёт `Work` с новыми `Guid.NewGuid()`. DigitalTwin создаёт свои `Work` с другими `Guid.NewGuid()`. Поскольку UUID генерируются независимо, `WorkId` в двух БД **никогда не совпадут**.

Это означает: CoreServer при получении первого батча от DigitalTwin не находит работу по `WorkId` и создаёт новую (`"Imported Project ..."` вместо `"Demo BIM Design Project"`). Seed-данные CoreServer с корректными именами работ остаются мёртвыми.

**Рекомендация.** При инициализации проекта ProjectClient должен:
1. Создать проект в DigitalTwin → получить WorkId из ответа
2. Передать эти WorkId в CoreServer для предварительной регистрации

ИЛИ: убрать seed-данные у CoreServer, дать CoreServer создавать проекты только по данным от DigitalTwin.

---

### ОР-4. Бустинг в MarkovStateEngine применяется к ВСЕМ состояниям выше текущего, не только к смежным

**Критичность: НИЗКАЯ**

**Описание.** В [MarkovStateEngine.cs:43-44](solution/CoreServer/src/CoreServer.Infrastructure/Services/MarkovStateEngine.cs#L43):

```csharp
probs[i] = Matrix[row, i] + (i > row ? severityBoost / 4 : 0);
```

`severityBoost / 4` распределяется **поровно** между всеми состояниями выше текущего. При состоянии S0 и событии "Failed" (+0.12) каждое из четырёх состояний S1, S2, S3, S4 получает +0.03. Это математически означает, что тяжёлое событие в S0 прибавляет одинаковую вероятность перехода в S1 и в S4, что физически нереалистично.

**Рекомендация.** Распределять boost с убыванием: ближайшее следующее состояние получает больший прирост:

```csharp
// Экспоненциальное убывание
double totalBoost = severityBoost;
for (int i = row + 1; i < 5; i++)
    probs[i] += totalBoost * Math.Pow(0.5, i - row - 1);
// Нормализация как прежде
```

---

### ОР-5. DesignRequirementChanged отсутствует в перечислении eventImpact

**Критичность: НИЗКАЯ**

**Описание.** В [DurationRecalculationEngine.cs:26-34](solution/CoreServer/src/CoreServer.Infrastructure/Services/DurationRecalculationEngine.cs#L26):

```csharp
var eventImpact = eventType switch
{
    nameof(EventType.ApprovalDelayed)     => 1.18,
    nameof(EventType.DocumentationReturned) => 1.15,
    nameof(EventType.CollisionDetected)   => 1.12,
    nameof(EventType.ResourceShortage)    => 1.14,
    nameof(EventType.ExpertReviewFailed)  => 1.20,
    _ => 1.08   // ← DesignRequirementChanged и Unknown попадают сюда
};
```

`DesignRequirementChanged` явно не перечислен и получает значение 1.08 по умолчанию. Это незадокументированное поведение. Если в диссертации `DesignRequirementChanged` имеет свой коэффициент — добавить явно.

---

## 6. Предлагаемые исправления

### Приоритет 1 — Соответствие научной модели

| № | Файл | Изменение |
|---|---|---|
| П1.1 | `DurationRecalculationEngine.cs` | Исправить коэффициенты состояний: S1=1.1, S2=1.3, S3=1.6, S4=2.0 |
| П1.2 | `DurationRecalculationEngine.cs` | Добавить ограничение `MaxDuration = PlannedDuration × 3.0` |
| П1.3 | `MarkovStateEngine.cs` | Добавить метод `UpdateMatrixFromHistoryAsync` + хранение матрицы в БД |
| П1.4 | `EventRegistryService.cs` | Хранить метку события как строку, не как enum |

### Приоритет 2 — Корректность алгоритмов

| № | Файл | Изменение |
|---|---|---|
| П2.1 | `MetricsProcessingService.cs` + `MLService/app.py` | Передавать скользящее окно исторических точек в DBSCAN |
| П2.2 | `MLService/app.py` | Поднять порог isKnown до 0.6, сделать конфигурируемым |
| П2.3 | `CoreServerSeed.cs` | Увеличить обучающую выборку, исправить похожие векторы `ExpertReviewFailed` / `DocumentationReturned` |
| П2.4 | `MLService/app.py` | Добавить `joblib` персистентность модели |

### Приоритет 3 — Качество реализации

| № | Файл | Изменение |
|---|---|---|
| П3.1 | `MarkovStateEngine.cs`, `DurationRecalculationEngine.cs` | Убрать внутренние `SaveChangesAsync`, оставить один в `MetricsProcessingService` |
| П3.2 | `MarkovStateEngine.cs:43` | Исправить распределение boost с убыванием |
| П3.3 | `DurationRecalculationEngine.cs` | Добавить явный кейс `DesignRequirementChanged` |
| П3.4 | `CoreServerSeed.cs` | Удалить seed-проект из CoreServer ИЛИ синхронизировать WorkId |

---

## 7. Итоговая рекомендуемая архитектура (с поправками)

Текущая архитектура правильная. Пересматривать её не нужно. Требуются точечные доработки в существующих компонентах.

```
DigitalTwin (3s tick)
  → WorkMetricSnapshot → batch → POST /api/metrics

CoreServer ProcessMetricsAsync:
  1. Upsert Project / Work
  2. Persist MetricHistory

  3. Подготовить точки для DBSCAN:
     ИЗМЕНИТЬ: скользящее окно последних K точек из MetricHistory
     + текущая точка (помечена как "current")

  4. POST /cluster → MLService
     DBSCAN выделяет "current"-точки вне нормального кластера

  5. Для каждого кандидата:
     a. POST /classify → SVM
        - если confidence >= 0.6 → известный тип
        - иначе → Unknown → SignalR → пользователь разметит
     b. Persist DetectedEvent
     c. MarkovStateEngine.ApplyEventAsync
        ИЗМЕНИТЬ: читает матрицу из БД (не const)
     d. DurationRecalculationEngine.RecalculateAsync
        ИЗМЕНИТЬ: коэффициенты S1=1.1, S2=1.3, S3=1.6, S4=2.0
        ДОБАВИТЬ: cap на MaxDuration
     e. SignalR push

  6. Фоновая задача (раз в час или по запросу):
     ДОБАВИТЬ: MarkovMatrixUpdater — пересчёт матрицы из истории состояний
     ДОБАВИТЬ: CoefficientCalibrator — пересчёт stateImpact из history

MLService:
  ДОБАВИТЬ: joblib персистентность
  ИЗМЕНИТЬ: порог isKnown → 0.6

EventPattern:
  ИЗМЕНИТЬ: EventTypeName -> string (вместо enum)
```

---

## 8. Список конкретных изменений в коде

### 8.1 DurationRecalculationEngine.cs — исправление коэффициентов (КО-2)

```csharp
// Строки 17-24: заменить
var stateImpact = state?.CurrentState switch
{
    WorkStabilityState.S1LowSensitive    => 1.1,
    WorkStabilityState.S2MediumSensitive => 1.3,
    WorkStabilityState.S3HighSensitive   => 1.6,
    WorkStabilityState.S4Critical        => 2.0,
    _                                    => 1.0
};

// Строка 29: добавить DesignRequirementChanged
var eventImpact = eventType switch
{
    nameof(EventType.ApprovalDelayed)           => 1.18,
    nameof(EventType.DocumentationReturned)     => 1.15,
    nameof(EventType.CollisionDetected)         => 1.12,
    nameof(EventType.ResourceShortage)          => 1.14,
    nameof(EventType.ExpertReviewFailed)        => 1.20,
    nameof(EventType.DesignRequirementChanged)  => 1.10,
    _                                           => 1.08
};

// Строка 37: добавить cap
var raw = Math.Round(previousDuration * eventImpact * stateImpact, 2);
var maxAllowed = work.PlannedDuration * 3.0;
var newDuration = Math.Min(raw, maxAllowed);
```

### 8.2 MLService/app.py — порог и персистентность (АА-4, АН-2)

```python
# Строка 139: повысить порог
CONFIDENCE_THRESHOLD = float(os.getenv("CONFIDENCE_THRESHOLD", "0.6"))
is_known = confidence >= CONFIDENCE_THRESHOLD

# После fit в /train:
import joblib
joblib.dump(registry.classifier, "./model.joblib")

# При старте приложения:
if pathlib.Path("./model.joblib").exists():
    registry.classifier = joblib.load("./model.joblib")
```

### 8.3 MarkovStateEngine.cs — исправление boost (ОР-4)

```csharp
// Строки 43-44: заменить на экспоненциальное убывание
var probs = new double[5];
for (var i = 0; i < 5; i++)
    probs[i] = Matrix[row, i];

// Распределяем boost с убыванием по следующим состояниям
var boostRemaining = severityBoost;
for (var i = row + 1; i < 5 && boostRemaining > 0; i++)
{
    var share = boostRemaining * 0.6; // 60% идёт в следующее состояние
    probs[i] += share;
    boostRemaining -= share;
}
// Остаток — в самое худшее состояние
if (boostRemaining > 0) probs[4] += boostRemaining;
```

---

## 9. Приоритетный план доработок

| Приоритет | Задача | Файл(ы) | Сложность |
|---|---|---|---|
| **P0** | Исправить коэффициенты stateImpact | DurationRecalculationEngine.cs | Тривиально |
| **P0** | Добавить MaxDuration cap | DurationRecalculationEngine.cs | Тривиально |
| **P1** | Передавать скользящее окно в DBSCAN | MetricsProcessingService.cs + метод в IMetricRepository | Средняя |
| **P1** | Поднять порог isKnown до 0.6 | app.py | Тривиально |
| **P1** | Добавить joblib персистентность SVM | app.py | Малая |
| **P2** | Механизм обновления матрицы Маркова из истории | MarkovStateEngine.cs + новая таблица | Сложная |
| **P2** | Исправить boost-распределение в Маркове | MarkovStateEngine.cs | Малая |
| **P2** | Расширить и разделить обучающую выборку | CoreServerSeed.cs | Малая |
| **P3** | Хранить EventType как строку | EventPattern, DetectedEvent, enum → string | Средняя |
| **P3** | Синхронизировать WorkId seed между сервисами | Seed-файлы | Малая |
| **P3** | Убрать лишние SaveChanges из движков | MarkovStateEngine.cs, DurationRecalculationEngine.cs | Малая |

---

## 10. Ответы на вопросы из PROJECT_MEMORY.md

1. **Матрица Маркова** — в научной модели описана как начально-экспертная с последующей автоматической корректировкой. В реализации — только экспертная, без корректировки. ❌

2. **Коэффициенты задержек** — в научной модели явно заданы другие значения (S4=2.0). В реализации S4=1.35. ❌

3. **Механизм восстановления** — в научной модели не упомянут явно. Матрица Маркова допускает переход из S4 в S0 (вероятность 0.02), то есть восстановление существует как стохастическая возможность. ✓ частично.

4. **DBSCAN принципиален** — да, является принципиальным алгоритмом модели. Eps не обоснован, но это допустимо для прототипа при наличии пояснения в диссертации.

5. **Оценка качества** — `SimulatedEventType` записывается в `MetricHistory` (ground truth), но нигде не сравнивается с `DetectedEvent.EventType`. Механизм оценки качества детекции в коде **отсутствует**. Это важный пробел для диссертации с научной точки зрения.

6. **Online-обучение** — является частью описанной научной модели (петля пользовательской разметки). Реализовано правильно по структуре, но ограничено фиксированным enum типов событий.
