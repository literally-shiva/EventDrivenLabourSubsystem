using CoreServer.Application.Interfaces;
using CoreServer.Application.Mapping;
using CoreServer.Infrastructure.Integrations;
using CoreServer.Infrastructure.Persistence;
using CoreServer.Infrastructure.Persistence.Repositories;
using CoreServer.Infrastructure.Realtime;
using CoreServer.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(_ => { }, typeof(CoreServerProfile).Assembly);
        services.AddDbContext<CoreServerDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CoreServerDb")));

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IWorkRepository, WorkRepository>();
        services.AddScoped<IMetricRepository, MetricRepository>();
        services.AddScoped<IEventPatternRepository, EventPatternRepository>();
        services.AddScoped<IDetectedEventRepository, DetectedEventRepository>();
        services.AddScoped<IWorkMarkovStateRepository, WorkMarkovStateRepository>();
        services.AddScoped<IDurationHistoryRepository, DurationHistoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMetricsProcessingService, MetricsProcessingService>();
        services.AddScoped<IEventRegistryService, EventRegistryService>();
        services.AddScoped<IProjectQueryService, ProjectQueryService>();
        services.AddScoped<IMarkovStateEngine, MarkovStateEngine>();
        services.AddScoped<IDurationRecalculationEngine, DurationRecalculationEngine>();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

        services.AddSignalR();
        services.AddHttpClient<IMlServiceClient, MlServiceClient>(client =>
            client.BaseAddress = new Uri(configuration["Integration:MLServiceBaseUrl"] ?? "http://localhost:8000"));

        return services;
    }
}
