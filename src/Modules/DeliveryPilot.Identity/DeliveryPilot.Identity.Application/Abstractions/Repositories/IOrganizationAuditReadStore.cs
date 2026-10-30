using DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IOrganizationAuditReadStore
{
    Task<OrganizationReadPage<OrganizationAuditEventResponse>> ListAsync(AuditEventFilter filter, int limit, AuditPageKey? after, CancellationToken cancellationToken);
}
