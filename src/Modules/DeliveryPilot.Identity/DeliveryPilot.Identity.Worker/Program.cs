using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Messaging.Outbox;
using DeliveryPilot.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    var builder = Host.CreateApplicationBuilder(args.Length > 0 && args[0] == "ops" ? [] : args);
    builder.AddDeliveryPilotObservability();
    // Transport exceptions may include message data. Export only explicit sanitized application events.
    builder.Logging.AddFilter("MassTransit", LogLevel.None);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
    builder.Services.AddEmailReceiptApplication().AddPersistence(builder.Configuration).AddUnitOfWork();
    builder.Services.AddScoped<DeliveryPilot.Identity.Application.Abstractions.Messaging.IEmailDeliveryReceiptStore,
        DeliveryPilot.Identity.Infrastructure.Messaging.EmailDeliveryReceiptStore>();
    if (args.Length > 0 && args[0] == "ops")
    {
        using var controlHost = builder.Build();
        using var scope = controlHost.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (args[1] == "inspect")
        {
            var rows = await db.Set<OutboxMessage>().AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(50).Select(x => new { x.MessageId, x.Status, x.Attempts, x.FailureCode, x.NextAttemptAt }).ToListAsync();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(rows));
        }
        else
        {
            await OutboxAdministration.ExecuteAsync(db, args[1], args[2], args[3], CancellationToken.None);
        }

        return;
    }

    builder.Services.AddIdentityMessaging(builder.Configuration);
    using var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Identity worker stopped ({ex.GetType().Name}).");
    Environment.ExitCode = 1;
}
