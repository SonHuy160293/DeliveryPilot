using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneDate;

public sealed class ChangeMilestoneDateHandler(EngagementMutationService mutations) : ICommandHandler<ChangeMilestoneDateCommand, MilestoneResult>
{
    public async Task<Result<MilestoneResult>> HandleAsync(ChangeMilestoneDateCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        context.Engagement.EnsureActive();
        EngagementMilestone milestone = await mutations.FindMilestoneAsync(context, request.MilestoneId, cancellationToken);
        milestone.RequireVersion(request.ExpectedVersion!.Value);
        DateOnly previous = milestone.CurrentTargetDate;
        if (milestone.ChangeDate(request.TargetDate!.Value, request.Reason, context.OccurredAt))
        {
            context.Engagement.RecordChange(context.OccurredAt);
            mutations.Record(context, "milestone.date-changed", "Milestone", milestone.Id, new { milestone.OriginalTargetDate, previous, @new = milestone.CurrentTargetDate }, request.Reason);
        }

        return Result.Success(MilestoneResult.From(milestone));
    }
}
