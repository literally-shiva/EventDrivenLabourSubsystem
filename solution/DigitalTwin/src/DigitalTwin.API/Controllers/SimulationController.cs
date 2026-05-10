using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.API.Controllers;

[ApiController]
[Route("simulation")]
public class SimulationController(ISimulationService simulationService) : ControllerBase
{
    [HttpPost("start/{projectId:guid}")]
    public async Task<IActionResult> Start(Guid projectId, CancellationToken cancellationToken)
    {
        await simulationService.StartSimulationAsync(projectId, cancellationToken);
        return Accepted();
    }

    [HttpPost("stop/{projectId:guid}")]
    public async Task<IActionResult> Stop(Guid projectId, CancellationToken cancellationToken)
    {
        await simulationService.StopSimulationAsync(projectId, cancellationToken);
        return Accepted();
    }

    [HttpGet("state/{projectId:guid}")]
    public async Task<ActionResult<SimulationStateDto>> GetState(Guid projectId, CancellationToken cancellationToken)
    {
        var state = await simulationService.GetStateAsync(projectId, cancellationToken);
        return state is null ? NotFound() : Ok(state);
    }
}
