using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CloseEngagement;

public sealed class CloseEngagementHandler(EngagementMutationService mutations) : ICommandHandler<CloseEngagementCommand, EngagementStateResult>
{
    public async Task<Result<EngagementStateResult>> HandleAsync(CloseEngagementCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        Engagement engagement = context.Engagement;
        engagement.RequireVersion(request.ExpectedVersion!.Value);
        if (engagement.Close(context.UserId, request.Reason, context.OccurredAt))
        {
            EngagementLinkCode? code = await mutations.FindOutstandingCodeAsync(context, cancellationToken);
            code?.Invalidate(context.OccurredAt);
            object details = code is null
                ? new { previousStatus = Engagement.ActiveStatus, newStatus = Engagement.ClosedStatus }
                : new { previousStatus = Engagement.ActiveStatus, newStatus = Engagement.ClosedStatus, invalidatedLinkCodeId = code.Id };
            mutations.Record(context, "engagement.closed", "Engagement", engagement.Id, details, request.Reason);
        }

        return Result.Success(EngagementStateResult.From(engagement));
    }
}
