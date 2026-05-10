using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
using DigitalTwin.Domain.Entities;
using DigitalTwin.Infrastructure.Persistence;

namespace DigitalTwin.Infrastructure.Services;

public class SimulationService(
    IProjectRepository projectRepository,
    IWorkMetricRepository metricRepository,
    IUnitOfWork unitOfWork,
    DigitalTwinDbContext dbContext) : ISimulationService
{
    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = BuildProject(request);
        await projectRepository.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToDto();
    }

    public async Task<ProjectDto> UpdateProjectAsync(Guid projectId, CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project not found.");
        if (project.IsSimulationRunning)
        {
            throw new InvalidOperationException("Cannot edit project while simulation is running.");
        }

        project.Name = request.Name;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        if (project.CurrentSimulationTime < request.StartDate || project.CurrentSimulationTime > request.EndDate)
        {
            project.CurrentSimulationTime = request.StartDate;
        }

        dbContext.WorkDependencies.RemoveRange(project.Works.SelectMany(work => work.ChildDependencies).ToArray());
        dbContext.Works.RemoveRange(project.Works.ToArray());
        project.Works.Clear();

        foreach (var work in BuildWorks(project.Id, request.Works))
        {
            project.Works.Add(work);
        }

        ApplyDependencies(project, request.Dependencies);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToDto();
    }

    public async Task<IReadOnlyCollection<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default) =>
        (await projectRepository.GetAllAsync(cancellationToken)).Select(x => x.ToDto()).ToArray();

    public async Task<ProjectDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        return project?.ToDto();
    }

    public async Task StartSimulationAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project not found.");
        project.IsSimulationRunning = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task StopSimulationAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project not found.");
        project.IsSimulationRunning = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SimulationStateDto?> GetStateAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var latestMetrics = await metricRepository.GetLatestByProjectAsync(projectId, cancellationToken);
        return new SimulationStateDto(project.Id, project.IsSimulationRunning, project.CurrentSimulationTime, latestMetrics.Select(x => x.ToDto()).ToArray());
    }

    private static Project BuildProject(CreateProjectRequest request)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CurrentSimulationTime = request.StartDate,
            IsSimulationRunning = false
        };

        foreach (var work in BuildWorks(project.Id, request.Works))
        {
            project.Works.Add(work);
        }

        ApplyDependencies(project, request.Dependencies);
        return project;
    }

    private static List<Work> BuildWorks(Guid projectId, IEnumerable<CreateWorkRequest> works) =>
        works.Select(source => new Work
        {
            Id = source.Id ?? Guid.NewGuid(),
            ProjectId = projectId,
            Name = source.Name,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            PlannedDuration = source.PlannedDuration,
            CurrentDuration = source.CurrentDuration,
            PercentComplete = source.PercentComplete
        }).ToList();

    private static void ApplyDependencies(Project project, IReadOnlyCollection<CreateWorkDependencyRequest>? dependencies)
    {
        var worksById = project.Works.ToDictionary(work => work.Id);
        var requestedDependencies = dependencies ?? BuildDefaultDependencies(project.Works);

        foreach (var dependency in requestedDependencies
                     .Where(link => link.SourceWorkId != link.TargetWorkId)
                     .DistinctBy(link => new { link.SourceWorkId, link.TargetWorkId }))
        {
            if (!worksById.TryGetValue(dependency.SourceWorkId, out var parent) ||
                !worksById.TryGetValue(dependency.TargetWorkId, out var child))
            {
                continue;
            }

            parent.ChildDependencies.Add(new WorkDependency
            {
                ParentWorkId = parent.Id,
                ChildWorkId = child.Id
            });
        }
    }

    private static List<CreateWorkDependencyRequest> BuildDefaultDependencies(IEnumerable<Work> works)
    {
        var ordered = works.OrderBy(work => work.StartDate).ToList();
        if (ordered.Count < 3)
        {
            return [];
        }

        return
        [
            new CreateWorkDependencyRequest(ordered[0].Id, ordered[1].Id),
            new CreateWorkDependencyRequest(ordered[0].Id, ordered[2].Id)
        ];
    }
}
