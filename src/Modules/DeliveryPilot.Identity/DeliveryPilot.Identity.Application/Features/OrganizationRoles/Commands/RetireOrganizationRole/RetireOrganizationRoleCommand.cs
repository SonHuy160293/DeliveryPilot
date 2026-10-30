using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.RetireOrganizationRole;

public sealed record RetireOrganizationRoleCommand(Guid OrganizationId, Guid RoleId, long ExpectedVersion, string? Reason, string? CorrelationId) : ICommand<CreateOrganizationRoleResponse>
{
    public override string ToString() => nameof(RetireOrganizationRoleCommand);
}
