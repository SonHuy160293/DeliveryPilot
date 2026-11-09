using System.Text.RegularExpressions;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationAuthority;

public sealed partial class CheckOrganizationAuthorityQueryValidator : IValidator<CheckOrganizationAuthorityQuery>
{
    public const int MaximumPermissionCodeLength = 128;

    public Task ValidateAsync(CheckOrganizationAuthorityQuery request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (request.UserId == Guid.Empty)
        {
            errors.Add("UserId must not be empty.");
        }

        if (request.OrganizationId == Guid.Empty)
        {
            errors.Add("OrganizationId must not be empty.");
        }

        if (request.SessionId == Guid.Empty)
        {
            errors.Add("SessionId must not be empty when supplied.");
        }

        if (request.RequiredPermissionCode is not null
            && (request.RequiredPermissionCode.Length > MaximumPermissionCodeLength
                || !PermissionCodePattern().IsMatch(request.RequiredPermissionCode)))
        {
            errors.Add($"RequiredPermissionCode must be 1-{MaximumPermissionCodeLength} lowercase letters, digits, dots or hyphens.");
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex("^[a-z0-9.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionCodePattern();
}
