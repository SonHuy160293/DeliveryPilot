using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Auditing;

public interface IIdentityAuditWriter
{
    Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default);

    void RecordDenialTelemetry(
        string eventType,
        Guid? organizationId,
        Guid? actorUserId,
        string correlationId,
        string denialCode);
}
