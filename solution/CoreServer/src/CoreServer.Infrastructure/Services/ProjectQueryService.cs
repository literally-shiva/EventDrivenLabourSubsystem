using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;

namespace CoreServer.Infrastructure.Services;

public class ProjectQueryService(IWorkRepository workRepository, IDetectedEventRepository detectedEventRepository) : IProjectQueryService
{
    public async Task<ProjectTimelineDto> GetTimelineAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var works = await GetWorksAsync(projectId, cancellationToken);
        var events = await GetEventsAsync(projectId, cancellationToken);
        return new ProjectTimelineDto(projectId, works, events);
    }

    public async Task<IReadOnlyCollection<WorkTimelineDto>> GetWorksAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        (await workRepository.GetByProjectAsync(projectId, cancellationToken))
        .Select(x => new WorkTimelineDto(x.Id, x.Name, x.StartDate, x.EndDate, x.PlannedDuration, x.CurrentDuration, x.PercentComplete, x.CurrentState.ToString()))
        .ToArray();

    public async Task<IReadOnlyCollection<DetectedEventDto>> GetEventsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        (await detectedEventRepository.GetByProjectAsync(projectId, cancellationToken))
        .Select(x => new DetectedEventDto(
            x.Id, x.ProjectId, x.WorkId, x.Name,
            x.EventType.ToString(), x.IsKnown, x.Confidence, x.Timestamp,
            string.IsNullOrEmpty(x.FeatureVector)
                ? []
                : System.Text.Json.JsonSerializer.Deserialize<double[]>(x.FeatureVector) ?? []))
        .ToArray();
}
