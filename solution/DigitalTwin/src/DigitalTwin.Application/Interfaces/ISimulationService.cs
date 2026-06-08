using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface ISimulationService
{
    Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<ProjectDto> UpdateProjectAsync(Guid projectId, CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<ProjectDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task StartSimulationAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task StopSimulationAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<SimulationStateDto?> GetStateAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task SyncWorkDatesAsync(Guid projectId, IEnumerable<WorkDateUpdateDto> updates, CancellationToken cancellationToken = default);
}
