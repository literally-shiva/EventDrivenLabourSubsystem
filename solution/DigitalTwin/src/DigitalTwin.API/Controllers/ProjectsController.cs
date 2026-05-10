using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.API.Controllers;

[ApiController]
[Route("projects")]
public class ProjectsController(ISimulationService simulationService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectRequest request, CancellationToken cancellationToken) =>
        Ok(await simulationService.CreateProjectAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(Guid id, [FromBody] CreateProjectRequest request, CancellationToken cancellationToken) =>
        Ok(await simulationService.UpdateProjectAsync(id, request, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectDto>>> GetProjects(CancellationToken cancellationToken) =>
        Ok(await simulationService.GetProjectsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await simulationService.GetProjectAsync(id, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }
}
