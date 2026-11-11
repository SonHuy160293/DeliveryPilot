using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.UpdateEngagementDetails;

public sealed class UpdateEngagementDetailsHandler(EngagementMutationService mutations) : ICommandHandler<UpdateEngagementDetailsCommand, UpdatedEngagementDetails>
{
    public async Task<Result<UpdatedEngagementDetails>> HandleAsync(UpdateEngagementDetailsCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        Engagement engagement = context.Engagement;
        engagement.EnsureActive();
        engagement.RequireVersion(request.ExpectedVersion!.Value);
        IReadOnlyDictionary<string, EngagementFieldChange> changed = engagement.UpdateDetails(
            request.ClientDisplayName,
            request.ClientLegalName,
            request.ClientCountryCode,
            request.EngagementName,
            request.AgreementReference,
            request.AgreementSignedOn!.Value,
            request.DocumentUrl,
            request.Reason,
            context.OccurredAt);
        bool duplicate = changed.ContainsKey("agreementReference") && await mutations.CheckDuplicateReferenceAsync(context, cancellationToken);
        if (changed.Count > 0)
        {
            mutations.Record(context, "engagement.details-changed", "Engagement", engagement.Id, new { changed }, request.Reason);
        }

        return Result.Success(UpdatedEngagementDetails.From(engagement, duplicate));
    }
}
