using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Notification.Domain.Models;

namespace DeliveryPilot.Notification.Infrastructure.Persistence;

public static class InvitationStatusOutbox
{
    public static void Stage(NotificationDbContext db, EmailDelivery job, string status, string? failureCode, DateTimeOffset now)
    {
        long sequence = job.NextInvitationStatusSequence();
        string? safeCode = OrganizationInvitationEmailStatusV1.IsAllowedFailureCode(failureCode) ? failureCode : "Other";
        var message = new OrganizationInvitationEmailStatusV1(Guid.CreateVersion7(), job.Id, "notification", sequence, now.ToUniversalTime(), status, safeCode);
        message.Validate();
        db.Set<OutboxMessage>().Add(OutboxMessage.Create(message.MessageId, OrganizationInvitationEmailStatusV1.EventType, 1, job.Id, sequence, JsonSerializer.Serialize(message), job.Id, job.Id, now));
    }
}
