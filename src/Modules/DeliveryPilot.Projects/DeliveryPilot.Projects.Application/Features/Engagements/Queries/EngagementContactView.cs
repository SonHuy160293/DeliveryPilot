namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public sealed record EngagementContactView(Guid Id, string Name, string Email, string ContactRole, Guid CreatedByUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version)
{
    public override string ToString() => nameof(EngagementContactView);
}
