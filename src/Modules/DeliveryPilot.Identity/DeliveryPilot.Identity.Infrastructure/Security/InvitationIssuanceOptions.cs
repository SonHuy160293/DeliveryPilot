using DeliveryPilot.Identity.Application.Abstractions.Security;

namespace DeliveryPilot.Identity.Infrastructure.Security;

public sealed class InvitationIssuanceOptions
{
    public const string SectionName = "OrganizationInvitationIssuance";
    public int PairCooldownSeconds { get; set; } = 60;
    public int PairDailyLimit { get; set; } = 10;
    public int WorkspaceHourlyLimit { get; set; } = 100;

    public InvitationIssuanceLimits ToLimits()
    {
        if (PairCooldownSeconds <= 0 || PairDailyLimit <= 0 || WorkspaceHourlyLimit <= 0)
        {
            throw new InvalidOperationException("Invitation issuance limits must be positive.");
        }

        return new InvitationIssuanceLimits(TimeSpan.FromSeconds(PairCooldownSeconds), PairDailyLimit, WorkspaceHourlyLimit);
    }
}
