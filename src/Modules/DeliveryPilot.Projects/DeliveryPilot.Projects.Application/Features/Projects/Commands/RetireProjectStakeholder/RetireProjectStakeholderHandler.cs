using DeliveryPilot.Projects.Application.Features.Projects.Mutations;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.RetireProjectStakeholder;

public sealed class RetireProjectStakeholderHandler(ProjectMutationService mutations) : ICommandHandler<RetireProjectStakeholderCommand, ProjectStakeholderView>
{
    public async Task<Result<ProjectStakeholderView>> HandleAsync(RetireProjectStakeholderCommand request, CancellationToken cancellationToken)
    {
        (ProjectMutationContext context, ProjectStakeholder? found) = await mutations.LockStakeholderAsync(request.OrganizationId, request.ProjectId, request.StakeholderId, cancellationToken);
        ProjectStakeholder stakeholder = found!;
        if (stakeholder.Retire(request.ExpectedVersion!.Value, context.UserId, context.OccurredAt))
        {
            mutations.Record(context, "stakeholder.retired", "Stakeholder", stakeholder.Id, new { previous = (DateTimeOffset?)null, next = stakeholder.RetiredAt });
        }

        return Result.Success(ProjectStakeholderView.From(stakeholder));
    }
}
