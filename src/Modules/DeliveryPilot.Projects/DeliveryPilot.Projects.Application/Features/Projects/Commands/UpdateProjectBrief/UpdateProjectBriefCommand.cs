using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectBrief;

public sealed record UpdateProjectBriefCommand(Guid OrganizationId, Guid ProjectId, long? ExpectedVersion, ProjectBriefValues Values) : ICommand<ProjectBriefView>
{
    public override string ToString() => nameof(UpdateProjectBriefCommand);
}
