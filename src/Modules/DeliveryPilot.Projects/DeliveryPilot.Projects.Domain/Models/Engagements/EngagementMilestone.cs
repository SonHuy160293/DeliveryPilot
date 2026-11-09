using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Engagements;

public sealed class EngagementMilestone
{
    public const string PlannedStatus = "Planned";
    public const string AchievedStatus = "Achieved";
    public const string MissedStatus = "Missed";
    public const string RetiredStatus = "Retired";

    private EngagementMilestone() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EngagementId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateOnly OriginalTargetDate { get; private set; }
    public DateOnly CurrentTargetDate { get; private set; }
    public string Status { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new ConflictException("Milestone.VersionConflict", "The milestone has changed.");
        }
    }

    public bool Edit(string? name, string? description, DateTimeOffset occurredAt)
    {
        EnsureMutable();
        string normalizedName = EngagementFields.RequiredText(name, 200, nameof(name));
        string? normalizedDescription = EngagementFields.OptionalText(description, 2000, nameof(description));
        if (Name == normalizedName && Description == normalizedDescription)
        {
            return false;
        }

        Name = normalizedName;
        Description = normalizedDescription;
        RecordChange(occurredAt);
        return true;
    }

    public bool ChangeDate(DateOnly date, string? reason, DateTimeOffset occurredAt)
    {
        EnsureMutable();
        date = EngagementFields.CalendarDate(date, nameof(date));
        if (CurrentTargetDate == date)
        {
            return false;
        }

        EngagementFields.RequiredText(reason, 2000, nameof(reason));
        CurrentTargetDate = date;
        RecordChange(occurredAt);
        return true;
    }

    public bool ChangeStatus(string? status, DateTimeOffset occurredAt)
    {
        EnsureMutable();
        if (status == Status)
        {
            return false;
        }

        bool allowed = Status switch
        {
            PlannedStatus => status is AchievedStatus or MissedStatus or RetiredStatus,
            MissedStatus => status is AchievedStatus or RetiredStatus,
            _ => false,
        };
        if (!allowed)
        {
            throw new ConflictException("Milestone.InvalidTransition", "This milestone status transition is not allowed.");
        }

        Status = status!;
        RecordChange(occurredAt);
        return true;
    }

    public static EngagementMilestone Create(Guid organizationId, Guid engagementId, Guid createdByUserId, DateTimeOffset createdAt, string name, DateOnly targetDate, string? description = null)
    {
        DateTimeOffset instant = createdAt.ToUniversalTime();
        DateOnly date = EngagementFields.CalendarDate(targetDate, nameof(targetDate));
        return new EngagementMilestone
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = EngagementFields.NonEmptyId(organizationId, nameof(organizationId)),
            EngagementId = EngagementFields.NonEmptyId(engagementId, nameof(engagementId)),
            CreatedByUserId = EngagementFields.NonEmptyId(createdByUserId, nameof(createdByUserId)),
            Name = EngagementFields.RequiredText(name, 200, nameof(name)),
            Description = EngagementFields.OptionalText(description, 2000, nameof(description)),
            OriginalTargetDate = date,
            CurrentTargetDate = date,
            Status = PlannedStatus,
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }

    private void EnsureMutable()
    {
        if (Status is AchievedStatus or RetiredStatus)
        {
            throw new ConflictException("Milestone.InvalidTransition", "A terminal milestone cannot be changed.");
        }
    }

    private void RecordChange(DateTimeOffset occurredAt)
    {
        UpdatedAt = occurredAt.ToUniversalTime();
        Version = checked(Version + 1);
    }
}
