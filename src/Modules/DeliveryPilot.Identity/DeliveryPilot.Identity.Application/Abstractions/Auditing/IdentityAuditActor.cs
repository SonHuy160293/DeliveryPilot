namespace DeliveryPilot.Identity.Application.Abstractions.Auditing;

public sealed record IdentityAuditActor(string Kind, Guid? UserId, string? Reference)
{
    public static IdentityAuditActor User(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Actor user ID must not be empty.", nameof(userId));
        }

        return new IdentityAuditActor("user", userId, null);
    }

    public static IdentityAuditActor System(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return new IdentityAuditActor("system", null, reference.Trim());
    }

    public static IdentityAuditActor Operator(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return new IdentityAuditActor("operator", null, reference.Trim());
    }
}
