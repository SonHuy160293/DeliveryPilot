using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;

public sealed record CreateProjectCommand(Guid OrganizationId, Guid IdempotencyKey, string? Name, string? Purpose, string? Kind, Guid? EngagementId) : ICommand<CreateProjectResult>
{
    public override string ToString() => nameof(CreateProjectCommand);
}
