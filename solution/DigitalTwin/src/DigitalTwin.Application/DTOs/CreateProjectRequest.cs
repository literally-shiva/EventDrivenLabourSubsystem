namespace DigitalTwin.Application.DTOs;

public record CreateProjectRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    List<CreateWorkRequest> Works,
    List<CreateWorkDependencyRequest>? Dependencies = null);

public record CreateWorkRequest(
    Guid? Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    int PlannedDuration,
    int CurrentDuration,
    double PercentComplete = 0);

public record CreateWorkDependencyRequest(Guid SourceWorkId, Guid TargetWorkId);
