namespace CoreServer.Domain.Enums;

public enum EventType
{
    Unknown = 0,
    DesignRequirementChanged = 1,
    DocumentationReturned = 2,
    CollisionDetected = 3,
    ApprovalDelayed = 4,
    ResourceShortage = 5,
    ExpertReviewFailed = 6
}
