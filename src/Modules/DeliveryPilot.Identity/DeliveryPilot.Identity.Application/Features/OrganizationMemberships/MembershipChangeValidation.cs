using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships;

internal static class MembershipChangeValidation
{
    public static void Validate(Guid organizationId, Guid membershipId, long version, string? reason)
    {
        if (organizationId == Guid.Empty || membershipId == Guid.Empty || version <= 0 || reason is null || reason.Length > 1000)
        {
            throw new CqrsValidationException(["Valid identifiers, a positive membership version and a reason of at most 1,000 characters are required."]);
        }

        try
        {
            IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(reason, true);
        }
        catch (ArgumentException)
        {
            throw new CqrsValidationException(["A valid removal or demotion reason is required."]);
        }
    }
}
