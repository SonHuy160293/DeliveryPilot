namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationRole
{
    private OrganizationRole() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Origin { get; private set; } = null!;
    public int? TemplateVersion { get; private set; }
    public long PermissionSetVersion { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static OrganizationRole CreateCustom(Guid organizationId, string name, Guid createdByUserId, DateTimeOffset createdAt, string? description = null)
    {
        var role = Create(organizationId, "pending", name, "custom", null, createdByUserId, createdAt, description);
        role.Code = $"role-{role.Id:N}";
        return role;
    }

    public static OrganizationRole Create(Guid organizationId, string code, string name, string origin, int? templateVersion, Guid? createdByUserId, DateTimeOffset createdAt, string? description = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization must not be empty.", nameof(organizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        code = code.Trim();
        name = name.Trim();
        if (code.Length > 80 || name.Length > 160)
        {
            throw new ArgumentException("Role code or name exceeds its maximum length.");
        }

        if (description?.Length > 1000)
        {
            throw new ArgumentException("Role description must not exceed 1000 characters.", nameof(description));
        }

        if (createdByUserId == Guid.Empty || (origin == "system" && (templateVersion is null or <= 0)) ||
            (origin == "custom" && (templateVersion is not null || createdByUserId is null)) ||
            (origin != "system" && origin != "custom"))
        {
            throw new ArgumentException("Role origin, template version, and creator are inconsistent.");
        }

        DateTimeOffset instant = createdAt.ToUniversalTime();
        return new OrganizationRole
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            Code = code,
            Name = name,
            Description = description,
            Origin = origin,
            TemplateVersion = templateVersion,
            PermissionSetVersion = 1,
            CreatedByUserId = createdByUserId,
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }

    public void UpdateDetails(string name, string? description, DateTimeOffset occurredAt)
    {
        EnsureMutableCustomRole();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string normalizedName = name.Trim();
        if (normalizedName.Length > 160)
        {
            throw new ArgumentException("Role name must not exceed 160 characters.", nameof(name));
        }

        if (description?.Length > 1000)
        {
            throw new ArgumentException("Role description must not exceed 1000 characters.", nameof(description));
        }

        DateTimeOffset instant = ValidateTransitionInstant(occurredAt);
        long nextVersion = checked(Version + 1);
        Name = normalizedName;
        Description = description;
        UpdatedAt = instant;
        Version = nextVersion;
    }

    public void AdvancePermissionSetVersion(DateTimeOffset occurredAt)
    {
        EnsureMutableCustomRole();
        DateTimeOffset instant = ValidateTransitionInstant(occurredAt);
        long nextPermissionSetVersion = checked(PermissionSetVersion + 1);
        long nextVersion = checked(Version + 1);
        PermissionSetVersion = nextPermissionSetVersion;
        UpdatedAt = instant;
        Version = nextVersion;
    }

    public void Retire(DateTimeOffset occurredAt)
    {
        EnsureMutableCustomRole();
        DateTimeOffset instant = ValidateTransitionInstant(occurredAt);
        long nextVersion = checked(Version + 1);
        RetiredAt = instant;
        UpdatedAt = instant;
        Version = nextVersion;
    }

    private void EnsureMutableCustomRole()
    {
        if (Origin != "custom")
        {
            throw new InvalidOperationException("System roles cannot be modified or retired.");
        }

        if (RetiredAt is not null)
        {
            throw new InvalidOperationException("Retired roles cannot be modified.");
        }
    }

    private DateTimeOffset ValidateTransitionInstant(DateTimeOffset occurredAt)
    {
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (instant < UpdatedAt)
        {
            throw new ArgumentException("Transition must not precede the last update.", nameof(occurredAt));
        }

        return instant;
    }
}
