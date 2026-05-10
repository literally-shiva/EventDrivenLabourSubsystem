using System.Net.Http.Json;
using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;

namespace CoreServer.Infrastructure.Integrations;

public class MlServiceClient(HttpClient httpClient) : IMlServiceClient
{
    public async Task<MlClusterResponse> ClusterAsync(MlClusterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/cluster", request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MlClusterResponse>(cancellationToken) ?? new MlClusterResponse(0, [], []);
    }

    public async Task<MlClassifyResponse> ClassifyAsync(MlClassifyRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/classify", request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MlClassifyResponse>(cancellationToken) ?? new MlClassifyResponse(false, "Unknown", 0);
    }

    public async Task TrainAsync(MlTrainRequest request, CancellationToken cancellationToken = default)
    {
        await httpClient.PostAsJsonAsync("/train", request, cancellationToken);
    }
}
