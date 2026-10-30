using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public sealed class ListOrganizationAuditEventsHandler(IOrganizationAuthorization authorization, IOrganizationAuditReadStore store)
    : IQueryHandler<ListOrganizationAuditEventsQuery, OrganizationReadPage<OrganizationAuditEventResponse>>
{
    public async Task<Result<OrganizationReadPage<OrganizationAuditEventResponse>>> HandleAsync(ListOrganizationAuditEventsQuery request, CancellationToken cancellationToken)
    {
        await authorization.AuthorizeCapabilityAsync(request.OrganizationId, OrganizationCapabilityCatalog.AuditRead, cancellationToken);
        AuditEventFilter filter = AuditEventFilter.Parse(request);
        return Result.Success(await store.ListAsync(filter, request.Limit, AuditEventCursor.Decode(request.Cursor, filter), cancellationToken));
    }
}
