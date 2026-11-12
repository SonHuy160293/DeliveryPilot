using DeliveryPilot.Messaging.Configuration;
using DeliveryPilot.Notification.Contracts;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace DeliveryPilot.Notification.Infrastructure.Messaging;
public static class NotificationTopology
{
    public static async Task DeployAsync(IConfiguration configuration, CancellationToken ct)
    {
        var broker = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new();
        var bus = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, broker);
            config.DeployTopologyOnly = true;
            config.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName("deliverypilot.notification.email-requested.v1"));
            config.Message<EmailDeliveryRecordedV1>(m => m.SetEntityName("deliverypilot.notification.email-recorded.v1"));
            config.Message<OrganizationInvitationEmailRequestedV1>(m => m.SetEntityName("deliverypilot.notification.organization-invitation-email-requested.v1"));
            config.Message<OrganizationInvitationEmailStatusV1>(m => m.SetEntityName("deliverypilot.notification.organization-invitation-email-status.v1"));
            config.Message<ProjectGuestInvitationEmailRequestedV1>(m => m.SetEntityName("deliverypilot.notification.project-guest-invitation-email-requested.v1"));
            config.Message<ProjectGuestInvitationEmailStatusV1>(m => m.SetEntityName("deliverypilot.notification.project-guest-invitation-email-status.v1"));
            config.ReceiveEndpoint("deliverypilot.notification.email-requested.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<EmailDeliveryRequestedV1>();
            });
            config.ReceiveEndpoint("deliverypilot.identity.email-recorded.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<EmailDeliveryRecordedV1>();
            });
            config.ReceiveEndpoint("deliverypilot.notification.organization-invitation-email-requested.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<OrganizationInvitationEmailRequestedV1>();
            });
            config.ReceiveEndpoint("deliverypilot.notification.project-guest-invitation-email-requested.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<ProjectGuestInvitationEmailRequestedV1>();
            });
            config.ReceiveEndpoint("deliverypilot.projects.project-guest-invitation-email-status.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<ProjectGuestInvitationEmailStatusV1>();
            });
            config.ReceiveEndpoint("deliverypilot.identity.organization-invitation-email-status.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<OrganizationInvitationEmailStatusV1>();
            });
            foreach (string queue in new[]
            {
                "deliverypilot.notification.email-requested.v1_error",
                "deliverypilot.notification.email-requested.v1_skipped",
                "deliverypilot.identity.email-recorded.v1_error",
                "deliverypilot.identity.email-recorded.v1_skipped",
                "deliverypilot.notification.organization-invitation-email-requested.v1_error",
                "deliverypilot.notification.organization-invitation-email-requested.v1_skipped",
                "deliverypilot.notification.project-guest-invitation-email-requested.v1_error",
                "deliverypilot.notification.project-guest-invitation-email-requested.v1_skipped",
                "deliverypilot.projects.project-guest-invitation-email-status.v1_error",
                "deliverypilot.projects.project-guest-invitation-email-status.v1_skipped",
                "deliverypilot.identity.organization-invitation-email-status.v1_error",
                "deliverypilot.identity.organization-invitation-email-status.v1_skipped",
                "deliverypilot.messaging.unrouted"
            }

            )
            {
                config.ReceiveEndpoint(queue, endpoint =>
                {
                    RabbitConfiguration.ConfigureQueue(endpoint, broker);
                    endpoint.ConfigureConsumeTopology = false;
                });
            }
        });
        try
        {
            await bus.StartAsync(ct);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }
}
