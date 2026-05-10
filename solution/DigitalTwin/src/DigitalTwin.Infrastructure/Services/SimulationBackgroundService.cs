using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Entities;
using DigitalTwin.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DigitalTwin.Infrastructure.Services;

public class SimulationBackgroundService(IServiceScopeFactory scopeFactory) : BackgroundService
{
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
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private static List<WorkMetricSnapshot> TickProject(Project project)
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

            var eventType = Random.Shared.NextDouble() switch
            {
                < 0.08 => SimulatedEventType.ResourceShortage,
                < 0.12 => SimulatedEventType.ApprovalDelayed,
                < 0.16 => SimulatedEventType.CollisionDetected,
                < 0.19 => SimulatedEventType.DocumentationReturned,
                < 0.21 => SimulatedEventType.DesignRequirementChanged,
                < 0.23 => SimulatedEventType.ExpertReviewFailed,
                _ => SimulatedEventType.None
            };

            var progressDelta = eventType == SimulatedEventType.None ? Random.Shared.NextDouble() * 15 + 7 : Random.Shared.NextDouble() * 8 + 1;
            work.PercentComplete = Math.Min(100, work.PercentComplete + progressDelta);
            work.CurrentDuration += 1;
            work.IsCompleted = work.PercentComplete >= 100;
            if (work.IsCompleted)
            {
                completed.Add(work.Id);
                work.EndDate = project.CurrentSimulationTime;
            }

            result.Add(new WorkMetricSnapshot
            {
                Id = Guid.NewGuid(),
                WorkId = work.Id,
                Timestamp = project.CurrentSimulationTime,
                WorkersCount = Math.Max(1, Random.Shared.Next(3, 11) - (eventType == SimulatedEventType.ResourceShortage ? 2 : 0)),
                ModelDataVolume = Math.Round(150 + work.PercentComplete * 4.5 + Random.Shared.NextDouble() * 50, 2),
                ChangesCount = Random.Shared.Next(0, 5) + (eventType == SimulatedEventType.DesignRequirementChanged ? 4 : 0),
                CollisionCount = Random.Shared.Next(0, 3) + (eventType == SimulatedEventType.CollisionDetected ? 5 : 0),
                ApprovalCount = Random.Shared.Next(0, 4),
                ApprovalDelayDays = eventType == SimulatedEventType.ApprovalDelayed ? Random.Shared.Next(3, 10) : Random.Shared.Next(0, 2),
                DocumentationVersionCount = Random.Shared.Next(1, 7),
                ReworkCount = Random.Shared.Next(0, 2) + (eventType is SimulatedEventType.DocumentationReturned or SimulatedEventType.ExpertReviewFailed ? 3 : 0),
                ProgressPercent = work.PercentComplete,
                SimulatedEventType = eventType
            });
        }

        if (project.Works.All(x => x.IsCompleted))
        {
            project.IsSimulationRunning = false;
            project.EndDate = project.CurrentSimulationTime;
        }

        return result;
    }
}
