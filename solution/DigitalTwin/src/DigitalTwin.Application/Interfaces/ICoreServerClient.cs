using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface ICoreServerClient
{
    Task PushMetricsAsync(CoreMetricBatchDto batch, CancellationToken cancellationToken = default);
}
