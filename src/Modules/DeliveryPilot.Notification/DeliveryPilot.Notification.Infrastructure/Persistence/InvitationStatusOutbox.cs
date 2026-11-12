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
        Guid messageId = Guid.CreateVersion7();
        string eventType;
        string json;
        if (job.RequestKind == "project-guest-invitation")
        {
            var message = new ProjectGuestInvitationEmailStatusV1(messageId, job.Id, "notification", sequence, now.ToUniversalTime(), status, safeCode);
            message.Validate();
            eventType = ProjectGuestInvitationEmailStatusV1.EventType;
            json = JsonSerializer.Serialize(message);
        }
        else
        {
            var message = new OrganizationInvitationEmailStatusV1(messageId, job.Id, "notification", sequence, now.ToUniversalTime(), status, safeCode);
            message.Validate();
            eventType = OrganizationInvitationEmailStatusV1.EventType;
            json = JsonSerializer.Serialize(message);
        }

        db.Set<OutboxMessage>().Add(OutboxMessage.Create(messageId, eventType, 1, job.Id, sequence, json, job.Id, job.Id, now));
    }
}
