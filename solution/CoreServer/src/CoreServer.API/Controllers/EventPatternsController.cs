using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreServer.API.Controllers;

[ApiController]
[Route("api/events")]
public class EventPatternsController(IEventRegistryService eventRegistryService) : ControllerBase
{
    [HttpPost("patterns")]
    public async Task<ActionResult<EventPatternDto>> CreatePattern([FromBody] CreateEventPatternRequest request, CancellationToken cancellationToken) =>
        Ok(await eventRegistryService.CreatePatternAsync(request, cancellationToken));

    [HttpGet("patterns")]
    public async Task<ActionResult<IReadOnlyCollection<EventPatternDto>>> GetPatterns(CancellationToken cancellationToken) =>
        Ok(await eventRegistryService.GetPatternsAsync(cancellationToken));

    [HttpPost("register-unknown")]
    public async Task<IActionResult> RegisterUnknown([FromBody] RegisterUnknownEventRequest request, CancellationToken cancellationToken)
    {
        await eventRegistryService.RegisterUnknownEventAsync(request, cancellationToken);
        return Accepted();
    }
}
