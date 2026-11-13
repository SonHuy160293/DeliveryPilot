using DeliveryPilot.Projects.Application.Features.Projects.Mutations;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProjectStakeholder;

public sealed class CreateProjectStakeholderHandler(ProjectMutationService mutations) : ICommandHandler<CreateProjectStakeholderCommand, ProjectStakeholderView>
{
    public async Task<Result<ProjectStakeholderView>> HandleAsync(CreateProjectStakeholderCommand request, CancellationToken cancellationToken)
    {
        (ProjectMutationContext context, _) = await mutations.LockStakeholderAsync(request.OrganizationId, request.ProjectId, null, cancellationToken);
        ProjectStakeholderValues values = request.Values.Normalize();
        await mutations.RequireActiveParticipantAsync(context, values.ParticipantId, cancellationToken);
        ProjectStakeholder stakeholder = ProjectStakeholder.Create(request.OrganizationId, request.ProjectId, context.UserId, context.OccurredAt, values.Name!, values.Side!, values.OrganizationName, values.Responsibilities, values.DecisionAreas, values.Email, values.ParticipantId);
        mutations.AddStakeholder(stakeholder);
        mutations.Record(context, "stakeholder.added", "Stakeholder", stakeholder.Id, new { previous = (object?)null, next = stakeholder.Values() });
        return Result.Success(ProjectStakeholderView.From(stakeholder));
    }
}
