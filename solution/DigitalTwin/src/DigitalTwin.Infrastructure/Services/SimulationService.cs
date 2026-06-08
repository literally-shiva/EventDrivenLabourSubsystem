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

        // Add dependencies separately to avoid EF duplicate-tracking of WorkDependency via both navigation collections
        var deps = BuildDependencyList(project, request.Dependencies);
        if (deps.Count > 0)
        {
            dbContext.WorkDependencies.AddRange(deps);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Reload the project with all relationships populated
        dbContext.ChangeTracker.Clear();
        var final = await projectRepository.GetByIdAsync(project.Id, cancellationToken)
                    ?? throw new InvalidOperationException("Project not found after save.");
        return final.ToDto();
    }

    public async Task<ProjectDto> UpdateProjectAsync(Guid projectId, CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);

        // Если проект не существует, создаём новый с заданным ID (импорт из клиента)
        if (project == null)
        {
            return await CreateProjectWithIdAsync(projectId, request, cancellationToken);
        }

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

    public async Task SyncWorkDatesAsync(Guid projectId, IEnumerable<WorkDateUpdateDto> updates, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return;
        }

        foreach (var update in updates)
        {
            var work = project.Works.FirstOrDefault(w => w.Id == update.WorkId);
            if (work is not null)
            {
                work.StartDate = update.StartDate;
                work.EndDate = update.EndDate;
                // Update CurrentDuration based on new EndDate
                work.CurrentDuration = Math.Max(0, (int)Math.Round((update.EndDate - work.StartDate).TotalDays));
            }
        }

        // After updating dates, cascade to dependent works
        UpdateDependentWorkDatesInMemory(project);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void UpdateDependentWorkDatesInMemory(Project project)
    {
        // Topological sort to process works in dependency order
        var visited = new HashSet<Guid>();
        var sorted = new List<Work>();

        void Visit(Work work)
        {
            if (visited.Contains(work.Id))
                return;

            visited.Add(work.Id);

            // Visit parent works first
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

        // Update dates in topological order
        foreach (var work in sorted)
        {
            var parentEndDates = work.ChildDependencies
                .Select(dep => project.Works.FirstOrDefault(w => w.Id == dep.ParentWorkId)?.EndDate)
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .ToList();

            if (parentEndDates.Any())
            {
                var maxParentEndDate = parentEndDates.Max();

                if (work.StartDate < maxParentEndDate)
                {
                    work.StartDate = maxParentEndDate;
                    if (!work.IsCompleted)
                    {
                        work.EndDate = work.StartDate.AddDays(work.CurrentDuration);
                    }
                }
            }
        }
    }

    private async Task<ProjectDto> CreateProjectWithIdAsync(Guid projectId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = BuildProjectWithId(projectId, request);
        await projectRepository.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Add dependencies separately to avoid EF duplicate-tracking
        var deps = BuildDependencyList(project, request.Dependencies);
        if (deps.Count > 0)
        {
            dbContext.WorkDependencies.AddRange(deps);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Reload the project with all relationships populated
        dbContext.ChangeTracker.Clear();
        var final = await projectRepository.GetByIdAsync(project.Id, cancellationToken)
                    ?? throw new InvalidOperationException("Project not found after save.");
        return final.ToDto();
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

        // Dependencies are NOT added here to avoid EF duplicate-tracking.
        // They are added separately after the project graph is saved (in CreateProjectAsync).
        return project;
    }

    private static Project BuildProjectWithId(Guid projectId, CreateProjectRequest request)
    {
        var project = new Project
        {
            Id = projectId,
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

    private static List<WorkDependency> BuildDependencyList(Project project, IReadOnlyCollection<CreateWorkDependencyRequest>? dependencies)
    {
        var worksById = project.Works.ToDictionary(work => work.Id);
        var requestedDependencies = dependencies ?? BuildDefaultDependencies(project.Works);
        var result = new List<WorkDependency>();

        foreach (var dependency in requestedDependencies
                     .Where(link => link.SourceWorkId != link.TargetWorkId)
                     .DistinctBy(link => new { link.SourceWorkId, link.TargetWorkId }))
        {
            if (!worksById.ContainsKey(dependency.SourceWorkId) ||
                !worksById.ContainsKey(dependency.TargetWorkId))
            {
                continue;
            }

            result.Add(new WorkDependency
            {
                ParentWorkId = dependency.SourceWorkId,
                ChildWorkId = dependency.TargetWorkId
            });
        }

        return result;
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
