using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementMilestone;

public sealed class EditEngagementMilestoneHandler(EngagementMutationService mutations) : ICommandHandler<EditEngagementMilestoneCommand, MilestoneResult>
{
    public async Task<Result<MilestoneResult>> HandleAsync(EditEngagementMilestoneCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        context.Engagement.EnsureActive();
        EngagementMilestone milestone = await mutations.FindMilestoneAsync(context, request.MilestoneId, cancellationToken);
        milestone.RequireVersion(request.ExpectedVersion!.Value);
        var previous = new { milestone.Name, milestone.Description };
        if (milestone.Edit(request.Name, request.Description, context.OccurredAt))
        {
            context.Engagement.RecordChange(context.OccurredAt);
            mutations.Record(context, "milestone.changed", "Milestone", milestone.Id, new { previous, @new = new { milestone.Name, milestone.Description } }, null);
        }

        return Result.Success(MilestoneResult.From(milestone));
    }
}
