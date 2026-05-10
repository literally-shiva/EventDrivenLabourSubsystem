using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mapping;
using DigitalTwin.Infrastructure.Integrations;
using DigitalTwin.Infrastructure.Persistence;
using DigitalTwin.Infrastructure.Persistence.Repositories;
using DigitalTwin.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(_ => { }, typeof(DigitalTwinProfile).Assembly);
        services.AddDbContext<DigitalTwinDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DigitalTwinDb")));

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IWorkMetricRepository, WorkMetricRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISimulationService, SimulationService>();
        services.AddHostedService<SimulationBackgroundService>();

        services.AddHttpClient<ICoreServerClient, CoreServerClient>(client =>
            client.BaseAddress = new Uri(configuration["Integration:CoreServerBaseUrl"] ?? "http://localhost:5000"));

        return services;
    }
}
