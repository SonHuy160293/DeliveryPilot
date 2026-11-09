using System.Globalization;
using DeliveryPilot.Observability;
using DeliveryPilot.Projects.Infrastructure.DependencyInjection;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

IHost? host = null;

try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Production,
        ContentRootPath = AppContext.BaseDirectory,
    });
    builder.Configuration
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
    builder.Logging.ClearProviders();
    builder.AddDeliveryPilotObservability();
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddSerilog(
        (services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "DeliveryPilot.Projects.MigrationRunner"),
        writeToProviders: true);

    host = builder.Build();
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await host.StartAsync(lifetime.ApplicationStopping);

    await using var scope = host.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
    Log.Information("Applying Projects database migrations");
    await context.Database.MigrateAsync(lifetime.ApplicationStopping);
    Log.Information("Projects database is up to date");
}
catch (Exception exception)
{
    // Configuration/provider exceptions can contain connection strings or credentials.
    Log.Fatal("Projects migration failed ({ExceptionType})", exception.GetType().Name);
    Environment.ExitCode = 1;
}
finally
{
    if (host is not null)
    {
        try
        {
            await host.StopAsync();
        }
        finally
        {
            host.Dispose();
        }
    }

    await Log.CloseAndFlushAsync();
}
