using System.Net.Http.Json;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Infrastructure.Integrations;

public class CoreServerClient(HttpClient httpClient) : ICoreServerClient
{
    public async Task PushMetricsAsync(CoreMetricBatchDto batch, CancellationToken cancellationToken = default)
    {
        await httpClient.PostAsJsonAsync("/api/metrics", batch, cancellationToken);
    }
}
