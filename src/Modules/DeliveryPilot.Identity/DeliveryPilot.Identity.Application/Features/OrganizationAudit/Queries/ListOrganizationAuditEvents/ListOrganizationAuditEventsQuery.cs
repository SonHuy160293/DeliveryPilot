using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public sealed record ListOrganizationAuditEventsQuery(Guid OrganizationId, string? ActorUserId, string? MembershipId,
    string? TargetId, string? Action, string? From, string? To, int Limit = 50, string? Cursor = null) : IQuery<OrganizationReadPage<OrganizationAuditEventResponse>>
{
    public override string ToString() => nameof(ListOrganizationAuditEventsQuery);
}
