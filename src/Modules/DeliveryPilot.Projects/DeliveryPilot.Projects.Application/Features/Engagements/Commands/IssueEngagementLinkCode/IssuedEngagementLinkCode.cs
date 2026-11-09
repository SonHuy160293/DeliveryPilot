namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.IssueEngagementLinkCode;

public sealed record IssuedEngagementLinkCode(string Code, DateTimeOffset ExpiresAt, long Version)
{
    public override string ToString() => nameof(IssuedEngagementLinkCode);
}
