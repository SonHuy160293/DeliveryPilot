using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.UpdateOrganizationRole;

public sealed record UpdateOrganizationRoleCommand(Guid OrganizationId, Guid RoleId, string? Name, string? Description, IReadOnlyList<string>? PermissionCodes, long ExpectedVersion, string CorrelationId) : ICommand<CreateOrganizationRoleResponse>;
