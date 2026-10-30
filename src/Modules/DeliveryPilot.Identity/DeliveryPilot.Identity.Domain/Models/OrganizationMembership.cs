namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationMembership
{
    private OrganizationMembership() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset StatusChangedAt { get; private set; }
    public Guid? StatusChangedByUserId { get; private set; }
    public string? StatusReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static OrganizationMembership Create(Guid organizationId, Guid userId, Guid? actorUserId, DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization must not be empty.", nameof(organizationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User must not be empty.", nameof(userId));
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Actor must not be empty when supplied.", nameof(actorUserId));
        }

        DateTimeOffset instant = createdAt.ToUniversalTime();
        return new OrganizationMembership
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = "active",
            StatusChangedAt = instant,
            StatusChangedByUserId = actorUserId,
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }

    // The caller must load every assignment for this membership in the same guarded transaction.
    public void Remove(Guid actorUserId, string reason, DateTimeOffset occurredAt, IReadOnlyCollection<OrganizationRoleAssignment> assignments)
    {
        if (Status != "active")
        {
            throw new InvalidOperationException("Only an active membership can be removed.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (actorUserId == Guid.Empty || reason.Trim().Length > 1000)
        {
            throw new ArgumentException("A valid actor and removal reason are required.");
        }

        ArgumentNullException.ThrowIfNull(assignments);
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (instant < UpdatedAt || Version == long.MaxValue || assignments.Select(assignment => assignment.Id).Distinct().Count() != assignments.Count || assignments.Any(assignment =>
                assignment.OrganizationId != OrganizationId || assignment.MembershipId != Id ||
                (assignment.RevokedAt is null && (instant < assignment.UpdatedAt || assignment.Version == long.MaxValue))))
        {
            throw new InvalidOperationException("The membership and its assignments cannot be removed with these inputs.");
        }

        foreach (OrganizationRoleAssignment assignment in assignments.Where(assignment => assignment.RevokedAt is null))
        {
            assignment.Revoke(actorUserId, instant, reason.Trim());
        }

        ChangeStatus("removed", actorUserId, reason.Trim(), instant);
    }

    public void Rejoin(Guid actorUserId, DateTimeOffset occurredAt)
    {
        if (Status != "removed")
        {
            throw new InvalidOperationException("Only a removed membership can rejoin.");
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Actor must not be empty.", nameof(actorUserId));
        }

        ChangeStatus("active", actorUserId, null, occurredAt);
    }

    // Load every historical assignment under the organization guard before reactivating a member.
    public void Rejoin(Guid actorUserId, DateTimeOffset occurredAt, IReadOnlyCollection<OrganizationRoleAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (Status != "removed" || actorUserId == Guid.Empty || instant < UpdatedAt || Version == long.MaxValue ||
            assignments.Select(assignment => assignment.Id).Distinct().Count() != assignments.Count ||
            assignments.Any(assignment => assignment.OrganizationId != OrganizationId || assignment.MembershipId != Id ||
                (assignment.RevokedAt is null && (instant < assignment.UpdatedAt || assignment.Version == long.MaxValue))))
        {
            throw new InvalidOperationException("The membership and its assignments cannot rejoin with these inputs.");
        }

        foreach (OrganizationRoleAssignment assignment in assignments.Where(assignment => assignment.RevokedAt is null))
        {
            assignment.Revoke(actorUserId, instant, "Historical grant closed before invitation rejoin.");
        }

        Rejoin(actorUserId, instant);
    }

    public void RecordAssignmentChange(DateTimeOffset occurredAt)
    {
        if (Status != "active")
        {
            throw new InvalidOperationException("Only an active membership can change role assignments.");
        }

        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (instant < UpdatedAt)
        {
            throw new ArgumentException("Assignment change must not precede the last update.", nameof(occurredAt));
        }

        long nextVersion = checked(Version + 1);
        UpdatedAt = instant;
        Version = nextVersion;
    }

    private void ChangeStatus(string status, Guid actorUserId, string? reason, DateTimeOffset occurredAt)
    {
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (instant < UpdatedAt)
        {
            throw new ArgumentException("Transition must not precede the last update.", nameof(occurredAt));
        }

        long nextVersion = checked(Version + 1);
        Status = status;
        StatusChangedAt = instant;
        StatusChangedByUserId = actorUserId;
        StatusReason = reason;
        UpdatedAt = instant;
        Version = nextVersion;
    }
}
