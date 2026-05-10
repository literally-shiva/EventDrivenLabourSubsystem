using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Entities;

namespace DigitalTwin.Application.Services;

public static class ProjectMapper
{
    public static ProjectDto ToDto(this Project project) =>
        new(
            project.Id,
            project.Name,
            project.StartDate,
            project.EndDate,
            project.CurrentSimulationTime,
            project.IsSimulationRunning,
            project.Works.Select(ToDto).ToArray(),
            project.Works
                .SelectMany(work => work.ChildDependencies)
                .Select(dependency => new WorkDependencyDto(dependency.ParentWorkId, dependency.ChildWorkId))
                .Distinct()
                .ToArray());

    public static WorkDto ToDto(this Work work) =>
        new(work.Id, work.ProjectId, work.Name, work.StartDate, work.EndDate, work.PlannedDuration, work.CurrentDuration, work.PercentComplete, work.IsCompleted);

    public static WorkMetricDto ToDto(this WorkMetricSnapshot metric) =>
        new(
            metric.WorkId,
            metric.Timestamp,
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
}
