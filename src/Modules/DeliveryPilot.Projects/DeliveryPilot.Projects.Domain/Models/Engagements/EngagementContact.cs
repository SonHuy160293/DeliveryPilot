using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Engagements;

public sealed class EngagementContact
{
    public const string ClientProductOwnerRole = "ClientProductOwner";
    public const string SponsorRole = "Sponsor";
    public const string OtherRole = "Other";

    private EngagementContact() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EngagementId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string ContactRole { get; private set; } = null!;
    public DateTimeOffset? RemovedAt { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new ConflictException("Contact.VersionConflict", "The contact has changed.");
        }
    }

    public bool Edit(string? name, string? email, string? contactRole, DateTimeOffset occurredAt)
    {
        string normalizedName = EngagementFields.RequiredText(name, 200, nameof(name));
        string normalizedEmail = EngagementFields.Email(email);
        string normalizedRole = ValidateRole(contactRole);
        if (Name == normalizedName && Email == normalizedEmail && ContactRole == normalizedRole)
        {
            return false;
        }

        Name = normalizedName;
        Email = normalizedEmail;
        ContactRole = normalizedRole;
        AdvanceVersion(occurredAt);
        return true;
    }

    public void Remove(Guid userId, DateTimeOffset occurredAt)
    {
        RemovedByUserId = EngagementFields.NonEmptyId(userId, nameof(userId));
        RemovedAt = occurredAt.ToUniversalTime();
        AdvanceVersion(occurredAt);
    }

    public static string ValidateRole(string? contactRole)
        => contactRole is ClientProductOwnerRole or SponsorRole or OtherRole
            ? contactRole
            : throw new BadRequestException("Engagement.InvalidContactRole", "A valid contact role is required.");

    public static EngagementContact Create(Guid organizationId, Guid engagementId, Guid createdByUserId, DateTimeOffset createdAt, string name, string email, string contactRole)
    {
        DateTimeOffset instant = createdAt.ToUniversalTime();
        return new EngagementContact
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = EngagementFields.NonEmptyId(organizationId, nameof(organizationId)),
            EngagementId = EngagementFields.NonEmptyId(engagementId, nameof(engagementId)),
            CreatedByUserId = EngagementFields.NonEmptyId(createdByUserId, nameof(createdByUserId)),
            Name = EngagementFields.RequiredText(name, 200, nameof(name)),
            Email = EngagementFields.Email(email),
            ContactRole = ValidateRole(contactRole),
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }

    private void AdvanceVersion(DateTimeOffset occurredAt)
    {
        UpdatedAt = occurredAt.ToUniversalTime();
        Version = checked(Version + 1);
    }
}
