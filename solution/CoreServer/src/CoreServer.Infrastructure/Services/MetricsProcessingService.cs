using System.Text.Json;
using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;
using Microsoft.Extensions.Logging;

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
    IRealtimeNotifier realtimeNotifier,
    IDigitalTwinClient digitalTwinClient,
    ILogger<MetricsProcessingService> logger) : IMetricsProcessingService
{
    public async Task ProcessMetricsAsync(MetricsBatchRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Upsert project
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

        // 2. Persist metrics
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

        // 3. Upsert works
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
            // On first tick CurrentDuration is 0 — initialise to PlannedDuration so event impacts
            // produce visible changes (e.g. 5 → 6.2 → 7.8) rather than starting from 1.
            if (work.CurrentDuration <= 0)
                work.CurrentDuration = work.PlannedDuration;
            await workRepository.AddOrUpdateAsync(work, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Broadcast real-time progress for EVERY work on every tick, not only event candidates.
        // This drives the continuous Gantt progress bar update (~3 s cadence).
        foreach (var item in request.Metrics)
        {
            var w = await workRepository.GetAsync(item.WorkId, cancellationToken);
            if (w is null) continue;
            await realtimeNotifier.WorkUpdatedAsync(new WorkTimelineDto(
                w.Id, w.Name, w.StartDate, w.EndDate,
                w.PlannedDuration, w.CurrentDuration,
                w.PercentComplete, w.CurrentState.ToString()));
        }

        // 4. ML clustering — degrade gracefully if MLService is unavailable
        MlClusterResponse clusterResponse;
        try
        {
            clusterResponse = await mlServiceClient.ClusterAsync(new MlClusterRequest(
                request.Metrics.Select(item =>
                    new MlMetricPointDto(item.WorkId, item.WorkersCount, item.ModelDataVolume,
                        item.ChangesCount, item.CollisionCount, item.ApprovalDelayDays, item.ReworkCount)).ToArray(),
                request.ProjectId.ToString()),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MLService /cluster unavailable — skipping event detection for batch {ProjectId}", request.ProjectId);
            return;
        }

        // 5. Process each event candidate
        foreach (var candidate in clusterResponse.Events)
        {
            // 5a. Classify
            MlClassifyResponse classifyResponse;
            try
            {
                classifyResponse = await mlServiceClient.ClassifyAsync(
                    new MlClassifyRequest(candidate.Vector), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MLService /classify failed for work {WorkId}", candidate.WorkId);
                classifyResponse = new MlClassifyResponse(false, nameof(EventType.Unknown), 0);
            }

            var eventType = classifyResponse.IsKnown ? classifyResponse.EventType : nameof(EventType.Unknown);

            // 5b. Persist detected event (with feature vector)
            var detectedEvent = new DetectedEvent
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                WorkId = candidate.WorkId,
                Name = eventType,
                EventType = Enum.TryParse<EventType>(eventType, true, out var parsed) ? parsed : EventType.Unknown,
                IsKnown = classifyResponse.IsKnown,
                Confidence = classifyResponse.Confidence,
                Timestamp = request.Timestamp,
                FeatureVector = JsonSerializer.Serialize(candidate.Vector)
            };

            await detectedEventRepository.AddAsync(detectedEvent, cancellationToken);

            // 5c. Markov transition
            var state = await markovStateEngine.ApplyEventAsync(candidate.WorkId, eventType, cancellationToken);

            // 5d. Duration recalculation
            var duration = await durationRecalculationEngine.RecalculateAsync(
                candidate.WorkId, detectedEvent.Id, eventType, cancellationToken);

            // 5e. Update work stability state
            var work = await workRepository.GetAsync(candidate.WorkId, cancellationToken);
            if (work is not null)
            {
                work.CurrentState = Enum.TryParse<WorkStabilityState>(state, out var parsedState)
                    ? parsedState
                    : WorkStabilityState.S0Stable;
                await workRepository.AddOrUpdateAsync(work, cancellationToken);

                await realtimeNotifier.WorkUpdatedAsync(new WorkTimelineDto(
                    work.Id, work.Name, work.StartDate, work.EndDate,
                    work.PlannedDuration, work.CurrentDuration,
                    work.PercentComplete, work.CurrentState.ToString()));
            }

            // 5f. Build DTO for SignalR (includes feature vector so Angular can reuse it)
            var detectedDto = new DetectedEventDto(
                detectedEvent.Id, detectedEvent.ProjectId, detectedEvent.WorkId,
                detectedEvent.Name, detectedEvent.EventType.ToString(),
                detectedEvent.IsKnown, detectedEvent.Confidence, detectedEvent.Timestamp,
                candidate.Vector);

            await realtimeNotifier.EventDetectedAsync(detectedDto);
            await realtimeNotifier.DurationChangedAsync(candidate.WorkId, duration);

            if (!detectedEvent.IsKnown)
            {
                await realtimeNotifier.UnknownEventDetectedAsync(detectedDto);
            }
        }

        // 6. Single save for all event-related state changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Sync changed work dates back to DigitalTwin so dependent works can be recalculated
        var changedWorks = new List<WorkDateUpdateDto>();
        foreach (var candidate in clusterResponse.Events)
        {
            var work = await workRepository.GetAsync(candidate.WorkId, cancellationToken);
            if (work is not null)
            {
                changedWorks.Add(new WorkDateUpdateDto(work.Id, work.StartDate, work.EndDate));
            }
        }

        if (changedWorks.Count > 0)
        {
            await digitalTwinClient.SyncWorkDatesAsync(request.ProjectId, changedWorks, cancellationToken);
        }
    }
}
