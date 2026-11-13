using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProjectStakeholder;

public sealed record CreateProjectStakeholderCommand(Guid OrganizationId, Guid ProjectId, ProjectStakeholderValues Values) : ICommand<ProjectStakeholderView>
{
    public override string ToString() => nameof(CreateProjectStakeholderCommand);
}
