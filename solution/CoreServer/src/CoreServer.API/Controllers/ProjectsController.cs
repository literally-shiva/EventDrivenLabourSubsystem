using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreServer.API.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(IProjectQueryService projectQueryService) : ControllerBase
{
    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<ProjectTimelineDto>> GetTimeline(Guid id, CancellationToken cancellationToken) =>
        Ok(await projectQueryService.GetTimelineAsync(id, cancellationToken));

    [HttpGet("{id:guid}/works")]
    public async Task<ActionResult<IReadOnlyCollection<WorkTimelineDto>>> GetWorks(Guid id, CancellationToken cancellationToken) =>
        Ok(await projectQueryService.GetWorksAsync(id, cancellationToken));

    [HttpGet("{id:guid}/events")]
    public async Task<ActionResult<IReadOnlyCollection<DetectedEventDto>>> GetEvents(Guid id, CancellationToken cancellationToken) =>
        Ok(await projectQueryService.GetEventsAsync(id, cancellationToken));
}
