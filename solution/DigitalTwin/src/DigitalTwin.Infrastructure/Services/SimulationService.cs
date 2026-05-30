using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
using DigitalTwin.Domain.Entities;
using DigitalTwin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        // Update project metadata
        project.Name = request.Name;
        project.StartDate = ToUtc(request.StartDate);
        project.EndDate = ToUtc(request.EndDate);
        if (project.CurrentSimulationTime < project.StartDate || project.CurrentSimulationTime > project.EndDate)
        {
            project.CurrentSimulationTime = project.StartDate;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Get all old work IDs
        var oldWorkIds = await dbContext.Works
            .Where(work => work.ProjectId == projectId)
            .Select(work => work.Id)
            .ToListAsync(cancellationToken);

        // Delete old dependencies
        var oldDependencies = await dbContext.WorkDependencies
            .Where(dependency => oldWorkIds.Contains(dependency.ParentWorkId) || oldWorkIds.Contains(dependency.ChildWorkId))
            .ToListAsync(cancellationToken);
        if (oldDependencies.Count > 0)
        {
            dbContext.WorkDependencies.RemoveRange(oldDependencies);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Delete old metrics
        var oldMetrics = await dbContext.WorkMetricSnapshots
            .Where(snapshot => oldWorkIds.Contains(snapshot.WorkId))
            .ToListAsync(cancellationToken);
        if (oldMetrics.Count > 0)
        {
            dbContext.WorkMetricSnapshots.RemoveRange(oldMetrics);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Delete old works
        var oldWorks = await dbContext.Works
            .Where(work => work.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        if (oldWorks.Count > 0)
        {
            dbContext.Works.RemoveRange(oldWorks);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Clear tracker completely
        dbContext.ChangeTracker.Clear();

        // Create new works directly in database
        var newWorks = BuildWorks(projectId, request.Works);
        dbContext.Works.AddRange(newWorks);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Create new dependencies
        if (request.Dependencies != null && request.Dependencies.Count > 0)
        {
            var workIds = newWorks.Select(w => w.Id).ToHashSet();
            var validDependencies = request.Dependencies
                .Where(d => d.SourceWorkId != d.TargetWorkId && workIds.Contains(d.SourceWorkId) && workIds.Contains(d.TargetWorkId))
                .DistinctBy(d => new { d.SourceWorkId, d.TargetWorkId })
                .Select(d => new WorkDependency
                {
                    ParentWorkId = d.SourceWorkId,
                    ChildWorkId = d.TargetWorkId
                })
                .ToList();

            if (validDependencies.Count > 0)
            {
                dbContext.WorkDependencies.AddRange(validDependencies);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // Final reload with all relationships
        dbContext.ChangeTracker.Clear();
        var finalProject = await projectRepository.GetByIdAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project not found after save.");

        return finalProject.ToDto();
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project not found.");

        var workDependencies = project.Works
            .SelectMany(work => work.ChildDependencies)
            .Concat(project.Works.SelectMany(work => work.ParentDependencies))
            .GroupBy(dependency => new { dependency.ParentWorkId, dependency.ChildWorkId })
            .Select(group => group.First())
            .ToList();

        if (workDependencies.Count > 0)
        {
            dbContext.WorkDependencies.RemoveRange(workDependencies);
        }

        var workIds = project.Works.Select(work => work.Id).ToList();
        var metricSnapshots = dbContext.WorkMetricSnapshots.Where(snapshot => workIds.Contains(snapshot.WorkId));
        dbContext.WorkMetricSnapshots.RemoveRange(metricSnapshots);

        dbContext.Works.RemoveRange(project.Works);
        dbContext.Projects.Remove(project);

        await unitOfWork.SaveChangesAsync(cancellationToken);
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
            StartDate = ToUtc(request.StartDate),
            EndDate = ToUtc(request.EndDate),
            CurrentSimulationTime = ToUtc(request.StartDate),
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
            StartDate = ToUtc(source.StartDate),
            EndDate = ToUtc(source.EndDate),
            PlannedDuration = source.PlannedDuration,
            CurrentDuration = source.CurrentDuration,
            PercentComplete = source.PercentComplete
        }).ToList();

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

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
