using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;

public sealed record CreateOrganizationRoleCommand(Guid OrganizationId, string? Name, string? Description, IReadOnlyList<string>? PermissionCodes, string CorrelationId) : ICommand<CreateOrganizationRoleResponse>;
