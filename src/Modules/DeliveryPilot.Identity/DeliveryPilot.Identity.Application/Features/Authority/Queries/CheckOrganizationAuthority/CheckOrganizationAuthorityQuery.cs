using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationAuthority;

public sealed record CheckOrganizationAuthorityQuery(
    Guid UserId,
    Guid OrganizationId,
    Guid? SessionId,
    string? RequiredPermissionCode) : IQuery<OrganizationAuthorityCheckResult>;
