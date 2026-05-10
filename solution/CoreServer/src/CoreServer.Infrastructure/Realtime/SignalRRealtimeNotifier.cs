using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CoreServer.Infrastructure.Realtime;

public class SignalRRealtimeNotifier(IHubContext<ProjectUpdatesHub> hubContext) : IRealtimeNotifier
{
    public Task WorkUpdatedAsync(WorkTimelineDto work) => hubContext.Clients.All.SendAsync("workUpdated", work);

    public Task EventDetectedAsync(DetectedEventDto detectedEvent) => hubContext.Clients.All.SendAsync("eventDetected", detectedEvent);

    public Task DurationChangedAsync(Guid workId, double newDuration) =>
        hubContext.Clients.All.SendAsync("durationChanged", new { workId, newDuration });

    public Task UnknownEventDetectedAsync(DetectedEventDto detectedEvent) =>
        hubContext.Clients.All.SendAsync("unknownEventDetected", detectedEvent);
}
