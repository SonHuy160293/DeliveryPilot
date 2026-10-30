using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.ChangeRoleAssignment;

public sealed class ChangeRoleAssignmentValidator : IValidator<ChangeRoleAssignmentCommand>
{
    public Task ValidateAsync(ChangeRoleAssignmentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || request.MembershipId == Guid.Empty || request.RoleId == Guid.Empty || request.ExpectedMembershipVersion <= 0 || request.Reason?.Length > 1000 || (request.Reason is not null && string.IsNullOrWhiteSpace(request.Reason)))
        {
            throw new CqrsValidationException(["Invalid role assignment request."]);
        }

        return Task.CompletedTask;
    }
}
