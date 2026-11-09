using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementClientLink;

public sealed class RemoveEngagementClientLinkHandler(EngagementMutationService mutations) : ICommandHandler<RemoveEngagementClientLinkCommand, EngagementStateResult>
{
    public async Task<Result<EngagementStateResult>> HandleAsync(RemoveEngagementClientLinkCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        Engagement engagement = context.Engagement;
        engagement.RequireVersion(request.ExpectedVersion!.Value);
        Guid clientOrganizationId = engagement.RemoveClientLink(context.OccurredAt);
        mutations.Record(context, "link.removed", "Engagement", engagement.Id, new { clientOrganizationId }, request.Reason);
        return Result.Success(EngagementStateResult.From(engagement));
    }
}
