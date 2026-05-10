using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;

namespace CoreServer.Infrastructure.Services;

public class MetricsProcessingService(
    IProjectRepository projectRepository,
    IMetricRepository metricRepository,
    IWorkRepository workRepository,
    IDetectedEventRepository detectedEventRepository,
    IUnitOfWork unitOfWork,
    IMlServiceClient mlServiceClient,
    IMarkovStateEngine markovStateEngine,
    IDurationRecalculationEngine durationRecalculationEngine,
    IRealtimeNotifier realtimeNotifier) : IMetricsProcessingService
{
    public async Task ProcessMetricsAsync(MetricsBatchRequest request, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            project = new Project
            {
                Id = request.ProjectId,
                Name = $"Imported Project {request.ProjectId.ToString()[..8]}",
                StartDate = request.Timestamp.Date,
                EndDate = request.Timestamp.Date.AddDays(30)
            };
            await projectRepository.AddAsync(project, cancellationToken);
        }

        var metrics = request.Metrics.Select(item => new MetricHistory
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            WorkId = item.WorkId,
            WorkName = item.WorkName,
            Timestamp = request.Timestamp,
            WorkersCount = item.WorkersCount,
            ModelDataVolume = item.ModelDataVolume,
            ChangesCount = item.ChangesCount,
            CollisionCount = item.CollisionCount,
            ApprovalCount = item.ApprovalCount,
            ApprovalDelayDays = item.ApprovalDelayDays,
            DocumentationVersionCount = item.DocumentationVersionCount,
            ReworkCount = item.ReworkCount,
            ProgressPercent = item.ProgressPercent,
            SimulatedEventType = item.SimulatedEventType
        }).ToArray();

        await metricRepository.AddRangeAsync(metrics, cancellationToken);

        foreach (var item in request.Metrics)
        {
            var work = await workRepository.GetAsync(item.WorkId, cancellationToken) ?? new Work
            {
                Id = item.WorkId,
                ProjectId = request.ProjectId,
                Name = item.WorkName,
                StartDate = request.Timestamp.Date,
                EndDate = request.Timestamp.Date.AddDays(5),
                PlannedDuration = 5
            };

            work.PercentComplete = item.ProgressPercent;
            work.CurrentDuration = Math.Max(work.CurrentDuration, 1);
            await workRepository.AddOrUpdateAsync(work, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var clusterResponse = await mlServiceClient.ClusterAsync(new MlClusterRequest(request.Metrics.Select(item =>
            new MlMetricPointDto(item.WorkId, item.WorkersCount, item.ModelDataVolume, item.ChangesCount, item.CollisionCount, item.ApprovalDelayDays, item.ReworkCount)).ToArray()), cancellationToken);

        foreach (var candidate in clusterResponse.Events)
        {
            var classifyResponse = await mlServiceClient.ClassifyAsync(new MlClassifyRequest(candidate.Vector), cancellationToken);
            var eventType = classifyResponse.IsKnown ? classifyResponse.EventType : nameof(EventType.Unknown);

            var detectedEvent = new DetectedEvent
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                WorkId = candidate.WorkId,
                Name = eventType,
                EventType = Enum.TryParse<EventType>(eventType, true, out var parsed) ? parsed : EventType.Unknown,
                IsKnown = classifyResponse.IsKnown,
                Confidence = classifyResponse.Confidence,
                Timestamp = request.Timestamp
            };

            await detectedEventRepository.AddAsync(detectedEvent, cancellationToken);
            var state = await markovStateEngine.ApplyEventAsync(candidate.WorkId, eventType, cancellationToken);
            var duration = await durationRecalculationEngine.RecalculateAsync(candidate.WorkId, detectedEvent.Id, eventType, cancellationToken);

            var work = await workRepository.GetAsync(candidate.WorkId, cancellationToken);
            if (work is not null)
            {
                work.CurrentState = Enum.TryParse<WorkStabilityState>(state, out var parsedState) ? parsedState : WorkStabilityState.S0Stable;
                await workRepository.AddOrUpdateAsync(work, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                await realtimeNotifier.WorkUpdatedAsync(new WorkTimelineDto(
                    work.Id,
                    work.Name,
                    work.StartDate,
                    work.EndDate,
                    work.PlannedDuration,
                    work.CurrentDuration,
                    work.PercentComplete,
                    work.CurrentState.ToString()));
            }

            var detectedDto = new DetectedEventDto(detectedEvent.Id, detectedEvent.ProjectId, detectedEvent.WorkId, detectedEvent.Name, detectedEvent.EventType.ToString(), detectedEvent.IsKnown, detectedEvent.Confidence, detectedEvent.Timestamp);
            await realtimeNotifier.EventDetectedAsync(detectedDto);
            await realtimeNotifier.DurationChangedAsync(candidate.WorkId, duration);
            if (!detectedEvent.IsKnown)
            {
                await realtimeNotifier.UnknownEventDetectedAsync(detectedDto);
            }
        }
    }
}
