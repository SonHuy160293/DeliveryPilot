using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationMembership;

public sealed record CheckOrganizationMembershipQuery(Guid? OrganizationId, IReadOnlyList<Guid>? UserIds) : IQuery<OrganizationMembershipCheckResult>
{
    public override string ToString() => nameof(CheckOrganizationMembershipQuery);
}
