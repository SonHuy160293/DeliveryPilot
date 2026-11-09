using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.IssueEngagementLinkCode;

public sealed record IssueEngagementLinkCodeCommand(Guid OrganizationId, Guid EngagementId, long? ExpectedVersion) : ICommand<IssuedEngagementLinkCode>
{
    public override string ToString() => nameof(IssueEngagementLinkCodeCommand);
}
