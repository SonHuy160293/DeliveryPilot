using System.Net.Mail;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations;

internal static class InvitationValidation
{
    public static void Scope(Guid organizationId, Guid? invitationId = null)
        => Require(organizationId != Guid.Empty && invitationId != Guid.Empty, "Valid organization and invitation identifiers are required.");

    public static void Version(long expectedVersion)
        => Require(expectedVersion > 0, "A positive expected version is required.");

    public static void Email(string? email)
    {
        string? normalized = email?.Trim();
        Require(normalized is { Length: >= 3 and <= 320 } && MailAddress.TryCreate(normalized, out MailAddress? address)
            && string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase), "A valid recipient email is required.");
    }

    public static void Roles(IReadOnlyList<Guid>? roleIds)
        => Require(roleIds is { Count: > 0 } && roleIds.All(id => id != Guid.Empty) && roleIds.Distinct().Count() == roleIds.Count, "Distinct nonempty role identifiers are required.");

    public static void Reason(string? reason, bool required)
    {
        try
        {
            IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(reason, required);
        }
        catch (ArgumentException)
        {
            throw new CqrsValidationException(["A valid reason is required."]);
        }
    }

    public static void Page(string status, int limit, string? cursor)
        => Require(status is "pending" or "accepted" or "declined" or "cancelled" or "expired"
            && limit is >= 1 and <= 100 && cursor is not { Length: > 1024 } && cursor != string.Empty, "Invalid invitation status or pagination.");

    private static void Require(bool condition, string error)
    {
        if (!condition)
        {
            throw new CqrsValidationException([error]);
        }
    }
}
