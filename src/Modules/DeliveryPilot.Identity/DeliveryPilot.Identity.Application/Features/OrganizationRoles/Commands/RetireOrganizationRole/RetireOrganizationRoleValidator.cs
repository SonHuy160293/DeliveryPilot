using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.RetireOrganizationRole;

public sealed class RetireOrganizationRoleValidator : IValidator<RetireOrganizationRoleCommand>
{
    public Task ValidateAsync(RetireOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || request.RoleId == Guid.Empty || request.ExpectedVersion <= 0)
        {
            throw new CqrsValidationException(["Organization, role and positive expected version are required."]);
        }

        try
        {
            IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(request.Reason, true);
        }
        catch (ArgumentException)
        {
            throw new CqrsValidationException(["A safe retirement reason of at most 1,000 characters is required."]);
        }

        return Task.CompletedTask;
    }
}
