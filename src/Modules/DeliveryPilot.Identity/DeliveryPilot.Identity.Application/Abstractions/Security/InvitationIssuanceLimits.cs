namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public sealed record InvitationIssuanceLimits(TimeSpan PairCooldown, int PairDailyLimit, int WorkspaceHourlyLimit)
{
    public static InvitationIssuanceLimits Default { get; } = new(TimeSpan.FromSeconds(60), 10, 100);
}
