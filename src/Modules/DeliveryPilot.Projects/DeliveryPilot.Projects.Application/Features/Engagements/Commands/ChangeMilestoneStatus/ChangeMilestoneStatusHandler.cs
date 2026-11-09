using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneStatus;

public sealed class ChangeMilestoneStatusHandler(EngagementMutationService mutations) : ICommandHandler<ChangeMilestoneStatusCommand, MilestoneResult>
{
    public async Task<Result<MilestoneResult>> HandleAsync(ChangeMilestoneStatusCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        context.Engagement.EnsureActive();
        EngagementMilestone milestone = await mutations.FindMilestoneAsync(context, request.MilestoneId, cancellationToken);
        milestone.RequireVersion(request.ExpectedVersion!.Value);
        string previous = milestone.Status;
        if (milestone.ChangeStatus(request.Status?.Trim(), context.OccurredAt))
        {
            context.Engagement.RecordChange(context.OccurredAt);
            mutations.Record(context, "milestone.status-changed", "Milestone", milestone.Id, new { previous, @new = milestone.Status }, request.Reason);
        }

        return Result.Success(MilestoneResult.From(milestone));
    }
}
