using System.Net.Http.Json;
using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;

namespace CoreServer.Infrastructure.Integrations;

public class DigitalTwinClient(HttpClient httpClient) : IDigitalTwinClient
{
    public async Task SyncWorkDatesAsync(Guid projectId, IEnumerable<WorkDateUpdateDto> updates, CancellationToken cancellationToken = default)
    {
        try
        {
            await httpClient.PostAsJsonAsync($"/projects/{projectId}/sync-dates", updates, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Degrade gracefully if DigitalTwin is unavailable
            // Log the error but don't fail the operation
        }
    }
}
