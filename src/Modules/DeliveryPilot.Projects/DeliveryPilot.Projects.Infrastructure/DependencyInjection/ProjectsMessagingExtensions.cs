using DeliveryPilot.Messaging.Configuration;
using DeliveryPilot.Messaging.MassTransit;
using DeliveryPilot.Messaging.Outbox;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryPilot.Projects.Infrastructure.DependencyInjection;

public static class ProjectsMessagingExtensions
{
    /// <summary>
    /// Registers the Projects outbox relay and broker connection (ADR-001 D-10). Event registrations
    /// and consumers are added when the first consumer exists; until then the relay has nothing to publish.
    /// </summary>
    public static IServiceCollection AddProjectsMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var broker = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new();
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(60);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IOutboxDispatchStore, EfOutboxDispatchStore<ProjectsDbContext>>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, bus) =>
            {
                RabbitConfiguration.ConfigureHost(bus, broker);
                bus.OverrideDefaultBusEndpointQueueName("deliverypilot.projects.worker.bus." + Guid.NewGuid().ToString("N"));
            });
        });
        services.AddHostedService<OutboxRelay>();
        return services;
    }
}
