using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ReopenEngagement;

public sealed class ReopenEngagementHandler(EngagementMutationService mutations) : ICommandHandler<ReopenEngagementCommand, EngagementStateResult>
{
    public async Task<Result<EngagementStateResult>> HandleAsync(ReopenEngagementCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        Engagement engagement = context.Engagement;
        engagement.RequireVersion(request.ExpectedVersion!.Value);
        if (engagement.Reopen(request.Reason, context.OccurredAt))
        {
            mutations.Record(context, "engagement.reopened", "Engagement", engagement.Id, new { previousStatus = Engagement.ClosedStatus, newStatus = Engagement.ActiveStatus }, request.Reason);
        }

        return Result.Success(EngagementStateResult.From(engagement));
    }
}
