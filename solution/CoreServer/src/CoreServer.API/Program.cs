using CoreServer.Application.Interfaces;
using CoreServer.Infrastructure;
using CoreServer.Infrastructure.Persistence;
using CoreServer.Infrastructure.Persistence.Seed;
using CoreServer.Infrastructure.Realtime;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("client", policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithOrigins("http://localhost:4200")
        .AllowCredentials());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CoreServerDbContext>();
    dbContext.Database.EnsureCreated();
    await CoreServerSeed.SeedAsync(dbContext);

    // Warm up SVM: retrain on all existing patterns so classification works from the first tick.
    var eventRegistry = scope.ServiceProvider.GetRequiredService<IEventRegistryService>();
    var patterns = await eventRegistry.GetPatternsAsync();
    if (patterns.Count >= 2)
    {
        try
        {
            var mlClient = scope.ServiceProvider.GetRequiredService<IMlServiceClient>();
            var trainingData = patterns
                .Select(p => new CoreServer.Application.DTOs.TrainingEventDto(
                    p.EventType,
                    System.Text.Json.JsonSerializer.Deserialize<double[]>(p.Vector) ?? []))
                .ToArray();
            await mlClient.TrainAsync(new CoreServer.Application.DTOs.MlTrainRequest(trainingData));
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not pre-warm SVM on startup — MLService may not be ready yet");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("client");
app.MapControllers();
app.MapHub<ProjectUpdatesHub>("/project-updates");
app.Run("http://localhost:5000");
