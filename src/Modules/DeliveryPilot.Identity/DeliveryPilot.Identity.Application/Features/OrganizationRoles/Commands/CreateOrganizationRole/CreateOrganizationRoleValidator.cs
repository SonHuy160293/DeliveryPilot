using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;

public sealed class CreateOrganizationRoleValidator : IValidator<CreateOrganizationRoleCommand>
{
    public Task ValidateAsync(CreateOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        if (request.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160 || request.Description?.Length > 1000)
        {
            errors.Add("Invalid role name, description, or organization.");
        }

        if (request.PermissionCodes is null || request.PermissionCodes.Count == 0
            || request.PermissionCodes.Any(code => string.IsNullOrWhiteSpace(code) || !OrganizationCapabilityCatalog.IsDelegablePermission(code))
            || request.PermissionCodes.Distinct(StringComparer.Ordinal).Count() != request.PermissionCodes.Count)
        {
            errors.Add("Role permissions must contain distinct registered delegable codes.");
        }

        if (errors.Count != 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
