using DeliveryPilot.Projects.Application.Features.Projects.Mutations;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectStakeholder;

public sealed class UpdateProjectStakeholderHandler(ProjectMutationService mutations) : ICommandHandler<UpdateProjectStakeholderCommand, ProjectStakeholderView>
{
    public async Task<Result<ProjectStakeholderView>> HandleAsync(UpdateProjectStakeholderCommand request, CancellationToken cancellationToken)
    {
        (ProjectMutationContext context, ProjectStakeholder? found) = await mutations.LockStakeholderAsync(request.OrganizationId, request.ProjectId, request.StakeholderId, cancellationToken);
        ProjectStakeholder stakeholder = found!;
        ProjectStakeholderValues previous = stakeholder.Values();
        await mutations.RequireActiveParticipantAsync(context, request.Values.ParticipantId, cancellationToken);
        if (stakeholder.Update(request.Values, request.ExpectedVersion!.Value, context.OccurredAt))
        {
            mutations.Record(context, "stakeholder.changed", "Stakeholder", stakeholder.Id, new { previous, next = stakeholder.Values() });
        }

        return Result.Success(ProjectStakeholderView.From(stakeholder));
    }
}
