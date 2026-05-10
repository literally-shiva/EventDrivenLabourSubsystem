using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreServer.API.Controllers;

[ApiController]
[Route("api/metrics")]
public class MetricsController(IMetricsProcessingService metricsProcessingService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] MetricsBatchRequest request, CancellationToken cancellationToken)
    {
        await metricsProcessingService.ProcessMetricsAsync(request, cancellationToken);
        return Accepted();
    }
}
