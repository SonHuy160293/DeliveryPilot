using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.UpdateOrganizationRole;

public sealed class UpdateOrganizationRoleValidator : IValidator<UpdateOrganizationRoleCommand>
{
    public async Task ValidateAsync(UpdateOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        if (request.RoleId == Guid.Empty || request.ExpectedVersion <= 0)
        {
            throw new CqrsValidationException(["Role and expected version are required."]);
        }

        await new CreateOrganizationRoleValidator().ValidateAsync(new CreateOrganizationRoleCommand(request.OrganizationId, request.Name, request.Description, request.PermissionCodes, request.CorrelationId), cancellationToken);
    }
}
