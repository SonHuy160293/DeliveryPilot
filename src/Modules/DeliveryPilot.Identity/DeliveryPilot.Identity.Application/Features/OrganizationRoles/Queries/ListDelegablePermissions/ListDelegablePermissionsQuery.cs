using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Queries.ListDelegablePermissions;

public sealed record ListDelegablePermissionsQuery(Guid OrganizationId) : IQuery<DelegablePermissionsResponse>;
