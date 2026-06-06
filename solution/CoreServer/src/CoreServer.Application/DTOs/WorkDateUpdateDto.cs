namespace CoreServer.Application.DTOs;

public record WorkDateUpdateDto(Guid WorkId, DateTime StartDate, DateTime EndDate);
