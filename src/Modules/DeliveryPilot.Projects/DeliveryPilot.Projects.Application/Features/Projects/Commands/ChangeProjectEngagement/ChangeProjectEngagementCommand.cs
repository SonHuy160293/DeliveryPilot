using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeProjectEngagement;

public sealed record ChangeProjectEngagementCommand(Guid OrganizationId, Guid ProjectId, long? ExpectedVersion, string? Kind, Guid? EngagementId, string? Reason) : ICommand<ProjectSettingsResult>
{
    public override string ToString() => nameof(ChangeProjectEngagementCommand);
}
