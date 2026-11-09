using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementMilestone;

public sealed class AddEngagementMilestoneHandler(EngagementMutationService mutations) : ICommandHandler<AddEngagementMilestoneCommand, MilestoneResult>
{
    public async Task<Result<MilestoneResult>> HandleAsync(AddEngagementMilestoneCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        context.Engagement.EnsureActive();
        var milestone = EngagementMilestone.Create(request.OrganizationId, request.EngagementId, context.UserId, context.OccurredAt, request.Name!, request.TargetDate!.Value, request.Description);
        mutations.AddMilestone(milestone);
        context.Engagement.RecordChange(context.OccurredAt);
        mutations.Record(context, "milestone.added", "Milestone", milestone.Id, new { @new = MilestoneResult.From(milestone) }, null);
        return Result.Success(MilestoneResult.From(milestone));
    }
}
