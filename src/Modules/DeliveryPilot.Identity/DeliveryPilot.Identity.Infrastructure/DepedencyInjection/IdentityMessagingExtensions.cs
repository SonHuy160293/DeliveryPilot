using DeliveryPilot.Identity.Application.Abstractions.Messaging;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.Identity.Infrastructure.Persistence.Outbox;
using DeliveryPilot.Messaging.Configuration;
using DeliveryPilot.Messaging.Outbox;
using DeliveryPilot.Messaging.MassTransit;
using DeliveryPilot.Notification.Contracts;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
public static class IdentityMessagingExtensions
{
    public static IServiceCollection AddIdentityMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var broker = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new();
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(60);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IVerificationEmailOutbox, IdentityEmailOutbox>();
        services.AddScoped<IEmailDeliveryReceiptStore, EmailDeliveryReceiptStore>();
        services.AddScoped<IOutboxDispatchStore, IdentityOutboxDispatchStore>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();
        services.AddSingleton(new IntegrationEventRegistration(EmailDeliveryRequestedV1.EventType, typeof(EmailDeliveryRequestedV1), value => ((EmailDeliveryRequestedV1)value).Validate()));
        services.AddSingleton(new IntegrationEventRegistration(OrganizationInvitationEmailRequestedV1.EventType, typeof(OrganizationInvitationEmailRequestedV1), value => ((OrganizationInvitationEmailRequestedV1)value).Validate()));
        services.AddMassTransit(x =>
        {
            x.AddConsumer<EmailDeliveryRecordedConsumer>();
            x.AddConsumer<InvitationDeliveryStatusConsumer>();
            x.UsingRabbitMq((context, bus) =>
            {
                RabbitConfiguration.ConfigureHost(bus, broker);
                bus.OverrideDefaultBusEndpointQueueName("deliverypilot.identity.worker.bus." + Guid.NewGuid().ToString("N"));
                bus.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName("deliverypilot.notification.email-requested.v1"));
                bus.Message<OrganizationInvitationEmailRequestedV1>(m => m.SetEntityName("deliverypilot.notification.organization-invitation-email-requested.v1"));
                bus.Message<EmailDeliveryRecordedV1>(m => m.SetEntityName("deliverypilot.notification.email-recorded.v1"));
                bus.Message<OrganizationInvitationEmailStatusV1>(m => m.SetEntityName("deliverypilot.notification.organization-invitation-email-status.v1"));
                bus.ReceiveEndpoint("deliverypilot.identity.email-recorded.v1", endpoint =>
                {
                    RabbitConfiguration.ConfigureQueue(endpoint, broker);
                    endpoint.ConfigureConsumer<EmailDeliveryRecordedConsumer>(context);
                });
                bus.ReceiveEndpoint("deliverypilot.identity.organization-invitation-email-status.v1", endpoint =>
                {
                    RabbitConfiguration.ConfigureQueue(endpoint, broker);
                    endpoint.ConfigureConsumer<InvitationDeliveryStatusConsumer>(context);
                });
            });
        });
        services.AddHostedService<OutboxRelay>();
        services.AddHostedService<IdentityOutboxMaintenanceWorker>();
        return services;
    }
}
