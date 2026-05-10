namespace CoreServer.Application.DTOs;

public record MlClusterRequest(IReadOnlyCollection<MlMetricPointDto> Metrics);

public record MlMetricPointDto(
    Guid WorkId,
    int WorkersCount,
    double ModelDataVolume,
    int ChangesCount,
    int CollisionCount,
    int ApprovalDelayDays,
    int ReworkCount);

public record MlClusterResponse(int NormalClusterId, IReadOnlyCollection<int> EventClusters, IReadOnlyCollection<MlEventCandidateDto> Events);

public record MlEventCandidateDto(Guid WorkId, int ClusterId, double[] Vector);

public record MlClassifyRequest(double[] Vector);

public record MlClassifyResponse(bool IsKnown, string EventType, double Confidence);

public record MlTrainRequest(IReadOnlyCollection<TrainingEventDto> Events);

public record TrainingEventDto(string EventType, double[] Vector);
