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
        var knownType = Enum.TryParse<EventType>(request.EventType, true, out var eventType)
            ? eventType
            : EventType.Unknown;

        var pattern = new EventPattern
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Vector = JsonSerializer.Serialize(request.Vector),
            EventType = knownType,
            EventTypeName = string.IsNullOrWhiteSpace(request.EventType) ? knownType.ToString() : request.EventType,
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
        // The user-provided name becomes the SVM training label exactly as typed.
        // This allows users to define new event types beyond the fixed enum.
        // EventType enum is set to the matching known value when the name matches one,
        // otherwise Unknown — but the SVM uses EventTypeName, not the enum.
        var knownType = Enum.TryParse<EventType>(request.Name, ignoreCase: true, out var parsed)
            ? parsed
            : EventType.Unknown;

        var pattern = new EventPattern
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Vector = JsonSerializer.Serialize(request.Vector),
            EventType = knownType,
            EventTypeName = request.Name,
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
        {
            // Use EventTypeName when available (preserves user-defined labels).
            // Fall back to EventType.ToString() for legacy/seeded patterns that
            // were created before EventTypeName was introduced.
            var label = string.IsNullOrWhiteSpace(pattern.EventTypeName)
                ? pattern.EventType.ToString()
                : pattern.EventTypeName;
            return new TrainingEventDto(label, JsonSerializer.Deserialize<double[]>(pattern.Vector) ?? []);
        }).ToArray());
        await mlServiceClient.TrainAsync(request, cancellationToken);
    }

    private static EventPatternDto ToDto(EventPattern pattern) =>
        new(pattern.Id, pattern.Name, pattern.Vector,
            string.IsNullOrWhiteSpace(pattern.EventTypeName) ? pattern.EventType.ToString() : pattern.EventTypeName,
            pattern.AverageDelayImpact, pattern.CreatedAt);
}
