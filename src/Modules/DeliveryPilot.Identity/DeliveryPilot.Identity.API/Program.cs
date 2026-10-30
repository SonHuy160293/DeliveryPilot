using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.AspNetCore.Options;
using DeliveryPilot.AspNetCore.Security;
using DeliveryPilot.Identity.API.Configuration;
using DeliveryPilot.Identity.API.Health;
using DeliveryPilot.Identity.API.Security;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Observability;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Globalization;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting DeliveryPilot.Identity API");

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddIdentitySecretFiles();

    builder.AddConfiguredSerilog();
    builder.AddDeliveryPilotObservability();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddAuthSessions(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiResponseEnvelopeFilter>();
    });
    builder.Services.AddConfiguredCors(builder.Configuration);
    builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddConfiguredAuthorization();
    builder.Services.AddIdentityHttpsRedirection(builder.Configuration);
    builder.Services.AddTrustedForwarding(builder.Configuration);
    builder.Services.AddConfiguredApiVersioning();
    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

    builder.Services.AddConfiguredSwagger();

    var app = builder.Build();

    Log.Information(
        "DeliveryPilot.Identity API configured for {Environment} and listening on {Urls}",
        app.Environment.EnvironmentName,
        app.Configuration["ASPNETCORE_URLS"] ?? "(default)");

    app.UseForwardedHeaders();
    app.UseCorrelationId();
    app.UseGlobalExceptionHandling();
    app.UseMiddleware<InvitationRateLimitMiddleware>();
    app.UseApiStatusCodeEnvelope();
    app.UseSerilogRequestLogging();

    app.UseConfiguredSwagger();

    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }

    app.UseIdentityHttpsRedirection();

    app.UseCors(CorsOptions.PolicyName);
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = WriteHealthCheckResponseAsync
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthCheckResponseAsync
    });

    app.MapControllers();

    await app.RunAsync();

    Log.Information("DeliveryPilot.Identity API stopped cleanly");
}
catch (Exception exception)
{
    Log.Fatal(exception, "DeliveryPilot.Identity API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
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
            duration = entry.Value.Duration.TotalMilliseconds
        }),
        totalDuration = report.TotalDuration.TotalMilliseconds
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(response));
}

