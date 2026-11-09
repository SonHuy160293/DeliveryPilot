using System.Globalization;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.AspNetCore.Options;
using DeliveryPilot.Observability;
using DeliveryPilot.Projects.API.Health;
using DeliveryPilot.Projects.Application.DependencyInjection;
using DeliveryPilot.Projects.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting DeliveryPilot.Projects API");

    var builder = WebApplication.CreateBuilder(args);

    builder.AddConfiguredSerilog();
    builder.AddDeliveryPilotObservability();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddCurrentUserProvider();
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiResponseEnvelopeFilter>();
    });
    builder.Services.AddConfiguredCors(builder.Configuration);

    // ADR-001 D-02: validate Identity-issued HS256 tokens locally; IdentitySessionValidator then
    // confirms the session with Identity on every request. Projects never issues tokens.
    builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddConfiguredAuthorization();
    builder.Services.AddTrustedForwarding(builder.Configuration);
    builder.Services.AddConfiguredApiVersioning();
    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

    builder.Services.AddConfiguredSwagger();

    var app = builder.Build();

    Log.Information(
        "DeliveryPilot.Projects API configured for {Environment} and listening on {Urls}",
        app.Environment.EnvironmentName,
        app.Configuration["ASPNETCORE_URLS"] ?? "(default)");

    app.UseForwardedHeaders();
    app.UseCorrelationId();
    app.UseGlobalExceptionHandling();
    app.UseApiStatusCodeEnvelope();
    app.UseSerilogRequestLogging();

    app.UseConfiguredSwagger();

    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }

    app.UseCors(CorsOptions.PolicyName);
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = WriteHealthCheckResponseAsync,
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthCheckResponseAsync,
    });

    app.MapControllers();

    await app.RunAsync();

    Log.Information("DeliveryPilot.Projects API stopped cleanly");
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "DeliveryPilot.Projects API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static Task WriteHealthCheckResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.TotalMilliseconds,
        }),
        totalDuration = report.TotalDuration.TotalMilliseconds,
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(response));
}

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program
{
}
