using AutoMapper;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Entities;

namespace DigitalTwin.Application.Mapping;

public class DigitalTwinProfile : Profile
{
    public DigitalTwinProfile()
    {
        CreateMap<Project, ProjectDto>();
        CreateMap<Work, WorkDto>();
        CreateMap<WorkMetricSnapshot, WorkMetricDto>()
            .ForCtorParam(nameof(WorkMetricDto.SimulatedEventType), opt => opt.MapFrom(src => src.SimulatedEventType.ToString()));
    }
}
