using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Messaging.Outbox;
using DeliveryPilot.Observability;
using DeliveryPilot.Projects.Infrastructure.DependencyInjection;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    bool isOps = args.Length > 0 && args[0] == "ops";
    var builder = Host.CreateApplicationBuilder(isOps ? [] : args);
    builder.AddDeliveryPilotObservability();

    // Transport exceptions may include message data. Export only explicit sanitized application events.
    builder.Logging.AddFilter("MassTransit", LogLevel.None);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);

    // The worker never receives the JWT signing key (ADR-001 D-02).
    builder.Services.AddPersistence(builder.Configuration).AddUnitOfWork();
    if (isOps)
    {
        using var controlHost = builder.Build();
        using var scope = controlHost.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
        if (args.Length > 1 && args[1] == "inspect")
        {
            var rows = await db.Set<OutboxMessage>().AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(50).Select(x => new { x.MessageId, x.Status, x.Attempts, x.FailureCode, x.NextAttemptAt }).ToListAsync();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(rows));
        }
        else if (args.Length > 3)
        {
            await OutboxAdministration.ExecuteAsync(db, args[1], args[2], args[3], CancellationToken.None);
        }
        else
        {
            Console.Error.WriteLine("Usage: ops inspect | ops <command> <arg1> <arg2>");
            Environment.ExitCode = 2;
        }

        return;
    }

    builder.Services.AddProjectsMessaging(builder.Configuration);
    using var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.Error.WriteLine($"Projects worker stopped ({ex.GetType().Name}).");
    Environment.ExitCode = 1;
}
