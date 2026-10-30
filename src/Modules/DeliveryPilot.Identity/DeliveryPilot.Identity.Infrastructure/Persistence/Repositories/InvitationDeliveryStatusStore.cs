using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Notification.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class InvitationDeliveryStatusStore(IdentityDbContext db) : IInvitationDeliveryStatusStore
{
    public async Task RecordAsync(OrganizationInvitationEmailStatusV1 message, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Delivery projection requires a command transaction.");
        }

        message.Validate();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({message.MessageId.ToString()}, 271004))", cancellationToken);
        const string consumer = "identity.organization-invitation-status.v1";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message))));
        InboxMessage? inbox = await db.Set<InboxMessage>().SingleOrDefaultAsync(x => x.ConsumerName == consumer && x.MessageId == message.MessageId, cancellationToken);
        if (inbox is not null)
        {
            inbox.EnsurePayloadMatches(hash);
            return;
        }

        Guid? organizationId = await db.OrganizationInvitationDeliveries.AsNoTracking().Where(x => x.DeliveryMessageId == message.DeliveryMessageId).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(cancellationToken);
        if (organizationId is not null)
        {
            await db.Organizations.FromSqlInterpolated($"""SELECT * FROM shared."Organizations" WHERE "Id" = {organizationId.Value} FOR UPDATE""").AsNoTracking().ToListAsync(cancellationToken);
            OrganizationInvitationDelivery? delivery = await db.OrganizationInvitationDeliveries.SingleOrDefaultAsync(x => x.DeliveryMessageId == message.DeliveryMessageId && x.OrganizationId == organizationId.Value, cancellationToken);
            bool current = delivery is not null && await db.OrganizationInvitations.AsNoTracking().AnyAsync(x => x.Id == delivery.InvitationId && x.OrganizationId == organizationId.Value && x.CurrentDeliveryMessageId == message.DeliveryMessageId, cancellationToken);
            if (current)
            {
                delivery!.ApplyStatus(message.Sequence, message.Status, message.FailureCode, message.OccurredAt);
            }
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid lease = Guid.CreateVersion7();
        inbox = InboxMessage.Create(consumer, message.MessageId, hash, lease, now, now.AddMinutes(1));
        inbox.Complete(lease, now);
        db.Set<InboxMessage>().Add(inbox);
    }
}
