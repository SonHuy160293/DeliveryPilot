using System.Text.Json;
using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Notification.Domain.Models;
using DeliveryPilot.Notification.Infrastructure.Email.Templates;
using DeliveryPilot.Notification.Infrastructure.Persistence;
using DeliveryPilot.Notification.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryPilot.Notification.Infrastructure.Email.Workers;

internal static class InvitationDeliveryStrategy
{
    public static byte[] Decrypt(EmailDelivery job, EmailPayloadProtection protection) => job.RequestKind switch
    {
        "organization-invitation" => protection.Decrypt(JsonSerializer.Deserialize<OrganizationInvitationEmailRequestedV1>(job.RequestJson) ?? throw new ArgumentException("Invalid invitation request.")),
        "project-guest-invitation" => protection.Decrypt(JsonSerializer.Deserialize<ProjectGuestInvitationEmailRequestedV1>(job.RequestJson) ?? throw new ArgumentException("Invalid invitation request.")),
        _ => throw new ArgumentException("Unknown invitation request kind."),
    };

    public static RenderedEmail Render(EmailDelivery job, ReadOnlySpan<byte> data, EmailDeliveryOptions options) => job.RequestKind switch
    {
        "organization-invitation" => OrganizationInvitationTemplate.Render(JsonSerializer.Deserialize<OrganizationInvitationEmailRequestedV1>(job.RequestJson) ?? throw new ArgumentException("Invalid invitation request."), data, options),
        "project-guest-invitation" => ProjectGuestInvitationTemplate.Render(JsonSerializer.Deserialize<ProjectGuestInvitationEmailRequestedV1>(job.RequestJson) ?? throw new ArgumentException("Invalid invitation request."), data, options),
        _ => throw new ArgumentException("Unknown invitation request kind."),
    };

    public static Task<bool?> IsAllowedAsync(EmailDelivery job, IServiceProvider services, CancellationToken cancellationToken) => job.RequestKind switch
    {
        "organization-invitation" => services.GetRequiredService<IInvitationDispatchEligibility>().IsAllowedAsync(job.Id, cancellationToken),
        "project-guest-invitation" => services.GetRequiredService<IProjectGuestInvitationDispatchEligibility>().IsAllowedAsync(job.Id, cancellationToken),
        _ => throw new ArgumentException("Unknown invitation request kind."),
    };

    public static void Stage(NotificationDbContext db, EmailDelivery job, string status, string? failureCode, DateTimeOffset now)
        => InvitationStatusOutbox.Stage(db, job, status, failureCode, now);
}
