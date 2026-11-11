namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class EngagementLinkAttemptOptions
{
    public int MaxFailures { get; set; } = 5;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(15);
}
