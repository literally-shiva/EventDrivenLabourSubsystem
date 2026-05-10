using System.Text.Json;
using CoreServer.Application.DTOs;
using CoreServer.Application.Interfaces;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;

namespace CoreServer.Infrastructure.Services;

public class EventRegistryService(IEventPatternRepository eventPatternRepository, IMlServiceClient mlServiceClient, IUnitOfWork unitOfWork) : IEventRegistryService
{
    public async Task<EventPatternDto> CreatePatternAsync(CreateEventPatternRequest request, CancellationToken cancellationToken = default)
    {
        var pattern = new EventPattern
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Vector = JsonSerializer.Serialize(request.Vector),
            EventType = Enum.TryParse<EventType>(request.EventType, true, out var eventType) ? eventType : EventType.Unknown,
            AverageDelayImpact = request.AverageDelayImpact,
            CreatedAt = DateTime.UtcNow
        };

        await eventPatternRepository.AddAsync(pattern, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RetrainAsync(cancellationToken);
        return ToDto(pattern);
    }

    public async Task<IReadOnlyCollection<EventPatternDto>> GetPatternsAsync(CancellationToken cancellationToken = default) =>
        (await eventPatternRepository.GetAllAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task RegisterUnknownEventAsync(RegisterUnknownEventRequest request, CancellationToken cancellationToken = default)
    {
        var pattern = new EventPattern
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Vector = JsonSerializer.Serialize(request.Vector),
            EventType = EventType.Unknown,
            AverageDelayImpact = 1.15,
            CreatedAt = DateTime.UtcNow
        };

        await eventPatternRepository.AddAsync(pattern, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RetrainAsync(cancellationToken);
    }

    private async Task RetrainAsync(CancellationToken cancellationToken)
    {
        var patterns = await eventPatternRepository.GetAllAsync(cancellationToken);
        var request = new MlTrainRequest(patterns.Select(pattern =>
            new TrainingEventDto(pattern.EventType.ToString(), JsonSerializer.Deserialize<double[]>(pattern.Vector) ?? [])).ToArray());
        await mlServiceClient.TrainAsync(request, cancellationToken);
    }

    private static EventPatternDto ToDto(EventPattern pattern) =>
        new(pattern.Id, pattern.Name, pattern.Vector, pattern.EventType.ToString(), pattern.AverageDelayImpact, pattern.CreatedAt);
}
