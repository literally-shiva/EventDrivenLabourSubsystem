using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Entities;
using DigitalTwin.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DigitalTwin.Infrastructure.Services;

public class SimulationBackgroundService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    // Состояние работы для отслеживания накопительных метрик
    private readonly Dictionary<Guid, WorkSimulationState> _workStates = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var projectRepository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var metricRepository = scope.ServiceProvider.GetRequiredService<IWorkMetricRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var coreServerClient = scope.ServiceProvider.GetRequiredService<ICoreServerClient>();

            var projects = (await projectRepository.GetAllAsync(stoppingToken)).Where(x => x.IsSimulationRunning).ToList();

            foreach (var project in projects)
            {
                var metrics = TickProject(project);
                if (metrics.Count == 0)
                {
                    continue;
                }

                await metricRepository.AddRangeAsync(metrics, stoppingToken);
                await unitOfWork.SaveChangesAsync(stoppingToken);

                var batch = new CoreMetricBatchDto(
                    project.Id,
                    project.CurrentSimulationTime,
                    metrics.Select(metric =>
                    {
                        var work = project.Works.First(x => x.Id == metric.WorkId);
                        return new CoreMetricDto(
                            metric.WorkId,
                            work.Name,
                            metric.WorkersCount,
                            metric.ModelDataVolume,
                            metric.ChangesCount,
                            metric.CollisionCount,
                            metric.ApprovalCount,
                            metric.ApprovalDelayDays,
                            metric.DocumentationVersionCount,
                            metric.ReworkCount,
                            metric.ProgressPercent,
                            metric.SimulatedEventType.ToString());
                    }).ToArray());

                await coreServerClient.PushMetricsAsync(batch, stoppingToken);

                // Синхронизация обновлённых дат работ с CoreServer
                var dateUpdates = project.Works
                    .Select(w => new WorkDateUpdateDto(w.Id, w.StartDate, w.EndDate))
                    .ToArray();
                await coreServerClient.SyncWorkDatesAsync(project.Id, dateUpdates, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private List<WorkMetricSnapshot> TickProject(Project project)
    {
        project.CurrentSimulationTime = project.CurrentSimulationTime.AddDays(1);
        var completed = new HashSet<Guid>(project.Works.Where(x => x.IsCompleted).Select(x => x.Id));
        var result = new List<WorkMetricSnapshot>();

        foreach (var work in project.Works.OrderBy(x => x.StartDate))
        {
            var prerequisites = work.ChildDependencies.Select(x => x.ParentWorkId).ToArray();
            var canStart = prerequisites.Length == 0 || prerequisites.All(completed.Contains);
            if (!canStart || work.IsCompleted)
            {
                continue;
            }

            // Инициализация состояния работы при первом тике
            if (!_workStates.ContainsKey(work.Id))
            {
                _workStates[work.Id] = new WorkSimulationState
                {
                    BaseWorkersCount = Random.Shared.Next(4, 8),
                    BaseCollisions = 0,
                    BaseChanges = 0,
                    BaseRework = 0,
                    BaseApprovals = Random.Shared.Next(1, 3),
                    BaseDocVersions = Random.Shared.Next(2, 4),
                    DaysSinceLastEvent = 0
                };
            }

            var state = _workStates[work.Id];
            state.DaysSinceLastEvent++;

            // Определение фазы работы для реалистичных трендов
            var progress = work.PercentComplete / 100.0;
            var phase = progress switch
            {
                < 0.2 => ProjectPhase.Initial,      // 0-20%: начало, мало коллизий
                < 0.5 => ProjectPhase.Development,  // 20-50%: активная разработка, пик коллизий
                < 0.8 => ProjectPhase.Integration,  // 50-80%: интеграция, много согласований
                _ => ProjectPhase.Completion        // 80-100%: завершение, доработки
            };

            // Генерация события с учётом фазы и времени с последнего события
            var eventType = GenerateEvent(state, phase);

            // Обновление накопительных метрик в зависимости от события
            UpdateAccumulativeMetrics(state, eventType, phase);

            // Прогресс работы
            var progressDelta = eventType == SimulatedEventType.None
                ? Random.Shared.NextDouble() * 12 + 8   // Нормальный прогресс: 8-20%
                : Random.Shared.NextDouble() * 5 + 2;   // При событии прогресс замедляется: 2-7%

            work.PercentComplete = Math.Min(100, work.PercentComplete + progressDelta);
            work.CurrentDuration += 1;

            // Обновление EndDate на основе текущей длительности
            work.EndDate = work.StartDate.AddDays(work.CurrentDuration);

            work.IsCompleted = work.PercentComplete >= 100;

            if (work.IsCompleted)
            {
                completed.Add(work.Id);
                work.EndDate = project.CurrentSimulationTime;
            }

            // Генерация метрик с реалистичными значениями
            result.Add(new WorkMetricSnapshot
            {
                Id = Guid.NewGuid(),
                WorkId = work.Id,
                Timestamp = project.CurrentSimulationTime,
                WorkersCount = GenerateWorkersCount(state, eventType),
                ModelDataVolume = GenerateModelDataVolume(progress),
                ChangesCount = state.BaseChanges,
                CollisionCount = state.BaseCollisions,
                ApprovalCount = state.BaseApprovals,
                ApprovalDelayDays = GenerateApprovalDelay(state, eventType),
                DocumentationVersionCount = state.BaseDocVersions,
                ReworkCount = state.BaseRework,
                ProgressPercent = work.PercentComplete,
                SimulatedEventType = eventType
            });

            if (eventType != SimulatedEventType.None)
            {
                state.DaysSinceLastEvent = 0;
            }
        }

        // Каскадное обновление дат зависимых работ
        UpdateDependentWorkDates(project);

        if (project.Works.All(x => x.IsCompleted))
        {
            project.IsSimulationRunning = false;
            project.EndDate = project.CurrentSimulationTime;
        }

        return result;
    }

    private static void UpdateDependentWorkDates(Project project)
    {
        // Обходим работы в топологическом порядке (от родителей к детям)
        var visited = new HashSet<Guid>();
        var sorted = new List<Work>();

        void Visit(Work work)
        {
            if (visited.Contains(work.Id))
                return;

            visited.Add(work.Id);

            // Сначала обрабатываем родительские работы
            foreach (var dep in work.ChildDependencies)
            {
                var parent = project.Works.FirstOrDefault(w => w.Id == dep.ParentWorkId);
                if (parent != null)
                {
                    Visit(parent);
                }
            }

            sorted.Add(work);
        }

        foreach (var work in project.Works)
        {
            Visit(work);
        }

        // Обновляем даты в топологическом порядке
        foreach (var work in sorted)
        {
            // Находим максимальную дату окончания среди родительских работ
            var parentEndDates = work.ChildDependencies
                .Select(dep => project.Works.FirstOrDefault(w => w.Id == dep.ParentWorkId)?.EndDate)
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .ToList();

            if (parentEndDates.Any())
            {
                var maxParentEndDate = parentEndDates.Max();

                // Если текущая StartDate раньше, чем должна быть - обновляем
                if (work.StartDate < maxParentEndDate)
                {
                    work.StartDate = maxParentEndDate;
                    // Пересчитываем EndDate на основе новой StartDate и текущей длительности
                    if (!work.IsCompleted)
                    {
                        work.EndDate = work.StartDate.AddDays(work.CurrentDuration);
                    }
                }
            }
        }
    }

    private static SimulatedEventType GenerateEvent(WorkSimulationState state, ProjectPhase phase)
    {
        // События происходят реже, если недавно было событие
        if (state.DaysSinceLastEvent < 2)
        {
            return SimulatedEventType.None;
        }

        // Вероятность события зависит от фазы проекта
        var eventProbability = phase switch
        {
            ProjectPhase.Initial => 0.20,      // 20% в начале
            ProjectPhase.Development => 0.30,  // 30% в активной фазе
            ProjectPhase.Integration => 0.35,  // 35% при интеграции
            ProjectPhase.Completion => 0.25,   // 25% при завершении
            _ => 0.20
        };

        var roll = Random.Shared.NextDouble();
        if (roll > eventProbability)
        {
            return SimulatedEventType.None;
        }

        // Распределение типов событий в зависимости от фазы
        return phase switch
        {
            ProjectPhase.Initial => Random.Shared.NextDouble() switch
            {
                < 0.3 => SimulatedEventType.ResourceShortage,
                < 0.6 => SimulatedEventType.DesignRequirementChanged,
                < 0.8 => SimulatedEventType.ApprovalDelayed,
                _ => SimulatedEventType.CollisionDetected
            },
            ProjectPhase.Development => Random.Shared.NextDouble() switch
            {
                < 0.4 => SimulatedEventType.CollisionDetected,
                < 0.7 => SimulatedEventType.DesignRequirementChanged,
                < 0.9 => SimulatedEventType.ResourceShortage,
                _ => SimulatedEventType.ApprovalDelayed
            },
            ProjectPhase.Integration => Random.Shared.NextDouble() switch
            {
                < 0.3 => SimulatedEventType.ApprovalDelayed,
                < 0.6 => SimulatedEventType.DocumentationReturned,
                < 0.8 => SimulatedEventType.ExpertReviewFailed,
                _ => SimulatedEventType.CollisionDetected
            },
            ProjectPhase.Completion => Random.Shared.NextDouble() switch
            {
                < 0.4 => SimulatedEventType.DocumentationReturned,
                < 0.7 => SimulatedEventType.ExpertReviewFailed,
                < 0.9 => SimulatedEventType.ApprovalDelayed,
                _ => SimulatedEventType.CollisionDetected
            },
            _ => SimulatedEventType.None
        };
    }

    private static void UpdateAccumulativeMetrics(WorkSimulationState state, SimulatedEventType eventType, ProjectPhase phase)
    {
        // Естественный рост метрик со временем (без событий)
        if (eventType == SimulatedEventType.None)
        {
            // Коллизии растут в фазе разработки
            if (phase == ProjectPhase.Development && Random.Shared.NextDouble() < 0.3)
            {
                state.BaseCollisions += Random.Shared.Next(0, 2);
            }

            // Документы накапливаются
            if (Random.Shared.NextDouble() < 0.2)
            {
                state.BaseDocVersions = Math.Min(8, state.BaseDocVersions + 1);
            }

            return;
        }

        // Скачки метрик при событиях
        switch (eventType)
        {
            case SimulatedEventType.CollisionDetected:
                // Основной эффект: скачок коллизий +4-8
                state.BaseCollisions += Random.Shared.Next(4, 9);
                // Каскадные эффекты:
                state.BaseRework += Random.Shared.Next(1, 3);           // коллизии требуют переделки
                state.BaseDocVersions += 1;                             // обновление чертежей
                break;

            case SimulatedEventType.DesignRequirementChanged:
                // Основной эффект: скачок изменений ТЗ +3-6
                state.BaseChanges += Random.Shared.Next(3, 7);
                // Каскадные эффекты:
                state.BaseRework += Random.Shared.Next(2, 5);           // переделка готовых работ
                state.BaseCollisions += Random.Shared.Next(0, 3);       // новые требования → новые коллизии
                state.BaseDocVersions += Random.Shared.Next(1, 3);      // обновление документации
                break;

            case SimulatedEventType.DocumentationReturned:
                // Основные эффекты
                state.BaseRework += Random.Shared.Next(2, 5);
                state.BaseDocVersions += Random.Shared.Next(2, 4);
                // Каскадный эффект: возврат = задержка
                state.TemporaryApprovalDelay = Random.Shared.Next(2, 5);
                break;

            case SimulatedEventType.ExpertReviewFailed:
                // САМОЕ ТЯЖЁЛОЕ СОБЫТИЕ - усиленные эффекты
                // Основные эффекты (в 1.5-2 раза больше DocumentationReturned)
                state.BaseRework += Random.Shared.Next(4, 8);           // почти удвоен
                state.BaseDocVersions += Random.Shared.Next(3, 6);      // увеличен на 50%
                // Каскадные эффекты:
                state.BaseChanges += Random.Shared.Next(1, 3);          // может потребовать изменения решений
                state.BaseCollisions += Random.Shared.Next(0, 2);       // переделка может создать коллизии
                state.BaseApprovals += Random.Shared.Next(1, 3);        // дополнительные согласующие
                state.TemporaryApprovalDelay = Random.Shared.Next(7, 13); // критическая задержка
                break;

            case SimulatedEventType.ApprovalDelayed:
                // ИСПРАВЛЕНИЕ: убран рост DocumentationVersionCount (семантическая ошибка)
                // Задержка не меняет количество версий!
                state.BaseApprovals += Random.Shared.Next(1, 4);        // могут добавляться согласующие
                // Задержка обрабатывается в GenerateApprovalDelay
                break;

            case SimulatedEventType.ResourceShortage:
                // ИСПРАВЛЕНИЕ: убран бессмысленный рост ApprovalCount
                // Снижение работников обрабатывается в GenerateWorkersCount
                // Каскадный эффект: нехватка людей → задержка
                state.TemporaryApprovalDelay = Random.Shared.Next(2, 6);
                break;
        }

        // Ограничение роста метрик
        state.BaseCollisions = Math.Min(15, state.BaseCollisions);
        state.BaseChanges = Math.Min(12, state.BaseChanges);
        state.BaseRework = Math.Min(10, state.BaseRework);
        state.BaseApprovals = Math.Min(8, state.BaseApprovals);
        state.BaseDocVersions = Math.Min(12, state.BaseDocVersions);
    }

    private static int GenerateWorkersCount(WorkSimulationState state, SimulatedEventType eventType)
    {
        var baseValue = state.BaseWorkersCount;

        // При нехватке ресурсов временное снижение
        if (eventType == SimulatedEventType.ResourceShortage)
        {
            return Math.Max(1, baseValue - Random.Shared.Next(2, 4));
        }

        // Небольшие колебания ±1
        var fluctuation = Random.Shared.Next(-1, 2);
        return Math.Max(2, Math.Min(10, baseValue + fluctuation));
    }

    private static double GenerateModelDataVolume(double progress)
    {
        // Объём модели растёт с прогрессом
        var baseVolume = 150 + progress * 450; // От 150 до 600
        var noise = Random.Shared.NextDouble() * 30 - 15; // ±15
        return Math.Round(baseVolume + noise, 2);
    }

    private static int GenerateApprovalDelay(WorkSimulationState state, SimulatedEventType eventType)
    {
        // Проверяем событие текущего тика
        if (eventType == SimulatedEventType.ApprovalDelayed)
        {
            // Значительная задержка: 5-12 дней
            return Random.Shared.Next(5, 13);
        }

        // Проверяем временную задержку от предыдущих событий
        if (state.TemporaryApprovalDelay > 0)
        {
            var delay = state.TemporaryApprovalDelay;
            state.TemporaryApprovalDelay = 0;  // сбрасываем после применения
            return delay;
        }

        // Обычные небольшие задержки: 0-2 дня
        return Random.Shared.Next(0, 3);
    }

    private class WorkSimulationState
    {
        public int BaseWorkersCount { get; set; }
        public int BaseCollisions { get; set; }
        public int BaseChanges { get; set; }
        public int BaseRework { get; set; }
        public int BaseApprovals { get; set; }
        public int BaseDocVersions { get; set; }
        public int DaysSinceLastEvent { get; set; }
        public int TemporaryApprovalDelay { get; set; }
    }

    private enum ProjectPhase
    {
        Initial,
        Development,
        Integration,
        Completion
    }
}
