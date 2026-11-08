using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations;

internal static class NominationValidation
{
    public static void Scope(Guid organizationId, Guid id)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty)
        {
            throw new CqrsValidationException(["Valid workspace and nomination or membership identifiers are required."]);
        }
    }

    public static void Transition(long version, string? reason = null)
    {
        if (version <= 0 || reason?.Length > 1000)
        {
            throw new CqrsValidationException(["A positive version and a bounded reason are required."]);
        }

        try
        {
            IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(reason, false);
        }
        catch (ArgumentException)
        {
            throw new CqrsValidationException(["A valid cancellation reason is required when supplied."]);
        }
    }

    public static void Page(Guid? organizationId, string status, int limit, string? cursor)
    {
        if (organizationId == Guid.Empty || status is not "pending" and not "accepted" and not "declined" and not "cancelled" and not "expired" ||
            limit is < 1 or > 100 || cursor is { Length: > 1024 } || cursor == string.Empty)
        {
            throw new CqrsValidationException(["Invalid nomination status or pagination."]);
        }
    }
}
