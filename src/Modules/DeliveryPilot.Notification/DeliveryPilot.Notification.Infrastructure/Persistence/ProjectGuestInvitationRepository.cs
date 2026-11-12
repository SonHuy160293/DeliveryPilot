using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Notification.Infrastructure.Persistence;

public sealed class ProjectGuestInvitationRepository(NotificationDbContext db) : IProjectGuestInvitationRepository
{
    public async Task AcceptAsync(ProjectGuestInvitationEmailRequestedV1 message, CancellationToken cancellationToken)
    {
        message.Validate();
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Pipeline transaction required.");
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({message.MessageId.ToString()}, 271001))", cancellationToken);
        string json = JsonSerializer.Serialize(message);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        const string consumer = "notification.project-guest-invitation.v1";
        InboxMessage? inbox = await db.Set<InboxMessage>().SingleOrDefaultAsync(x => x.ConsumerName == consumer && x.MessageId == message.MessageId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (inbox is null)
        {
            EmailDelivery job = EmailDelivery.CreateProjectGuestInvitation(message.MessageId, hash, json, message.IssuedAt, message.ExpiresAt);
            if (!job.CanSend(now)) { job.Expire(); }
            db.EmailDeliveries.Add(job);
            InvitationStatusOutbox.Stage(db, job, job.Status == "Expired" ? "expired" : "queued", null, now);
            Guid lease = Guid.CreateVersion7();
            inbox = InboxMessage.Create(consumer, message.MessageId, hash, lease, now, now.AddMinutes(1));
            inbox.Complete(lease, now);
            db.Set<InboxMessage>().Add(inbox);
        }
        else
        {
            inbox.EnsurePayloadMatches(hash);
        }

        var receipt = new EmailDeliveryRecordedV1(Guid.CreateVersion7(), message.MessageId, message.CorrelationId, now);
        db.Set<OutboxMessage>().Add(OutboxMessage.Create(receipt.MessageId, EmailDeliveryRecordedV1.EventType, 1, message.MessageId, 1, JsonSerializer.Serialize(receipt), message.CorrelationId, message.MessageId, now));
    }
}
