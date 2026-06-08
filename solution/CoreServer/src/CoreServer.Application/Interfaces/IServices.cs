using CoreServer.Application.DTOs;

namespace CoreServer.Application.Interfaces;

public interface IMetricsProcessingService
{
    Task ProcessMetricsAsync(MetricsBatchRequest request, CancellationToken cancellationToken = default);
}

public interface IEventRegistryService
{
    Task<EventPatternDto> CreatePatternAsync(CreateEventPatternRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EventPatternDto>> GetPatternsAsync(CancellationToken cancellationToken = default);
    Task RegisterUnknownEventAsync(RegisterUnknownEventRequest request, CancellationToken cancellationToken = default);
}

public interface IProjectQueryService
{
    Task<ProjectTimelineDto> GetTimelineAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<WorkTimelineDto>> GetWorksAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DetectedEventDto>> GetEventsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MetricHistoryDto>> GetMetricsAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface IMarkovStateEngine
{
    Task<string> ApplyEventAsync(Guid workId, string eventType, CancellationToken cancellationToken = default);
}

public interface IDurationRecalculationEngine
{
    Task<double> RecalculateAsync(Guid workId, Guid eventId, string eventType, CancellationToken cancellationToken = default);
}

public interface IMlServiceClient
{
    Task<MlClusterResponse> ClusterAsync(MlClusterRequest request, CancellationToken cancellationToken = default);
    Task<MlClassifyResponse> ClassifyAsync(MlClassifyRequest request, CancellationToken cancellationToken = default);
    Task TrainAsync(MlTrainRequest request, CancellationToken cancellationToken = default);
}

public interface IRealtimeNotifier
{
    Task WorkUpdatedAsync(WorkTimelineDto work);
    Task EventDetectedAsync(DetectedEventDto detectedEvent);
    Task DurationChangedAsync(Guid workId, double newDuration);
    Task UnknownEventDetectedAsync(DetectedEventDto detectedEvent);
}

public interface IDigitalTwinClient
{
    Task SyncWorkDatesAsync(Guid projectId, IEnumerable<WorkDateUpdateDto> updates, CancellationToken cancellationToken = default);
}
