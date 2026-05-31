using AutoMapper;
using CoreServer.Application.DTOs;
using CoreServer.Domain.Entities;

namespace CoreServer.Application.Mapping;

public class CoreServerProfile : Profile
{
    public CoreServerProfile()
    {
        CreateMap<EventPattern, EventPatternDto>()
            .ForCtorParam(nameof(EventPatternDto.EventType), opt => opt.MapFrom(src => src.EventType.ToString()));

        CreateMap<DetectedEvent, DetectedEventDto>()
            .ForCtorParam(nameof(DetectedEventDto.EventType), opt => opt.MapFrom(src => src.EventType.ToString()))
            .ForCtorParam(nameof(DetectedEventDto.FeatureVector), opt => opt.MapFrom(src =>
                string.IsNullOrEmpty(src.FeatureVector)
                    ? new double[0]
                    : System.Text.Json.JsonSerializer.Deserialize<double[]>(src.FeatureVector) ?? new double[0]));
    }
}
