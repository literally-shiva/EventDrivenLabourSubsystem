using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreServer.API.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(IProjectQueryService projectQueryService, IWorkRepository workRepository) : ControllerBase
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

    [HttpGet("{id:guid}/metrics")]
    public async Task<ActionResult<IReadOnlyCollection<MetricHistoryDto>>> GetMetrics(Guid id, CancellationToken cancellationToken) =>
        Ok(await projectQueryService.GetMetricsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/sync-dates")]
    public async Task<IActionResult> SyncWorkDates(Guid id, [FromBody] IEnumerable<WorkDateUpdateDto> updates, CancellationToken cancellationToken)
    {
        foreach (var update in updates)
        {
            var work = await workRepository.GetAsync(update.WorkId, cancellationToken);
            if (work != null)
            {
                work.StartDate = update.StartDate;
                work.EndDate = update.EndDate;
                await workRepository.AddOrUpdateAsync(work, cancellationToken);
            }
        }
        return Ok();
    }
}
