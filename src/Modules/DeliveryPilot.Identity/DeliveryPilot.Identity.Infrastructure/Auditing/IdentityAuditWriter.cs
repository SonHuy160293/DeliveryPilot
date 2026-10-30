using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeliveryPilot.Identity.Infrastructure.Auditing;

public sealed class IdentityAuditWriter(
    IdentityDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<IdentityAuditWriter> logger) : IIdentityAuditWriter
{
    public Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Actor);

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Pipeline transaction required.");
        }

        int ordinal = entry.EventOrdinal ?? AllocateNextOrdinal(entry);
        EnsureNoStagedOrdinalCollision(entry, ordinal);

        IdentityAuditEvent auditEvent = IdentityAuditEvent.Create(
            entry.OrganizationId,
            entry.MembershipId,
            entry.MembershipVersion,
            entry.AggregateType,
            entry.AggregateId,
            entry.AggregateVersion,
            ordinal,
            entry.EventType,
            "succeeded",
            entry.Actor.Kind,
            entry.Actor.UserId,
            entry.Actor.Reference,
            entry.OccurredAt,
            timeProvider.GetUtcNow(),
            entry.CorrelationId,
            entry.DetailsJson,
            entry.Reason,
            detailsSchemaVersion: 1,
            requiresReason: entry.RequiresReason);

        dbContext.IdentityAuditEvents.Add(auditEvent);
        return Task.FromResult(auditEvent);
    }

    public void RecordDenialTelemetry(
        string eventType,
        Guid? organizationId,
        Guid? actorUserId,
        string correlationId,
        string denialCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(denialCode);

        logger.LogWarning(
            "Identity operation denied for EventType={EventType} OrganizationId={OrganizationId} ActorUserId={ActorUserId} CorrelationId={CorrelationId} DenialCode={DenialCode}",
            eventType.Trim(),
            organizationId,
            actorUserId,
            correlationId.Trim(),
            denialCode.Trim());
    }

    private int AllocateNextOrdinal(IdentityAuditEntry entry)
    {
        int maxOrdinal = 0;
        foreach (var tracked in dbContext.ChangeTracker.Entries<IdentityAuditEvent>())
        {
            if (tracked.State == EntityState.Detached)
            {
                continue;
            }

            IdentityAuditEvent existing = tracked.Entity;
            bool sameAggregateRevision =
                string.Equals(existing.AggregateType, entry.AggregateType, StringComparison.Ordinal) &&
                existing.AggregateId == entry.AggregateId &&
                existing.AggregateVersion == entry.AggregateVersion;
            bool sameMembershipRevision =
                entry.MembershipId is not null &&
                existing.MembershipId == entry.MembershipId &&
                existing.MembershipVersion == entry.MembershipVersion;

            if ((sameAggregateRevision || sameMembershipRevision) && existing.EventOrdinal > maxOrdinal)
            {
                maxOrdinal = existing.EventOrdinal;
            }
        }

        return checked(maxOrdinal + 1);
    }

    private void EnsureNoStagedOrdinalCollision(IdentityAuditEntry entry, int ordinal)
    {
        foreach (var tracked in dbContext.ChangeTracker.Entries<IdentityAuditEvent>())
        {
            if (tracked.State == EntityState.Detached)
            {
                continue;
            }

            IdentityAuditEvent existing = tracked.Entity;
            if (string.Equals(existing.AggregateType, entry.AggregateType, StringComparison.Ordinal) &&
                existing.AggregateId == entry.AggregateId &&
                existing.AggregateVersion == entry.AggregateVersion &&
                existing.EventOrdinal == ordinal)
            {
                throw new InvalidOperationException("Duplicate audit aggregate revision ordinal in the current transaction.");
            }

            if (entry.MembershipId is not null &&
                existing.MembershipId == entry.MembershipId &&
                existing.MembershipVersion == entry.MembershipVersion &&
                existing.EventOrdinal == ordinal)
            {
                throw new InvalidOperationException("Duplicate audit membership revision ordinal in the current transaction.");
            }
        }
    }
}
