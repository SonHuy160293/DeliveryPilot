using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectStakeholder;

public sealed record UpdateProjectStakeholderCommand(Guid OrganizationId, Guid ProjectId, Guid StakeholderId, long? ExpectedVersion, ProjectStakeholderValues Values) : ICommand<ProjectStakeholderView>
{
    public override string ToString() => nameof(UpdateProjectStakeholderCommand);
}
