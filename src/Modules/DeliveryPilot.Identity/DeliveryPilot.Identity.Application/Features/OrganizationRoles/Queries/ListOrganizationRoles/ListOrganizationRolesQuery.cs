using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Queries.ListOrganizationRoles;

public sealed record ListOrganizationRolesQuery(Guid OrganizationId, int Limit, string? Cursor) : IQuery<OrganizationPageResponse<CreateOrganizationRoleResponse>>;
