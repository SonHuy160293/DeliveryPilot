using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementClientLink;

public sealed class RemoveEngagementClientLinkValidator : IValidator<RemoveEngagementClientLinkCommand>
{
    public Task ValidateAsync(RemoveEngagementClientLinkCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            null,
            request.ExpectedVersion,
            true,
            () => EngagementFields.OptionalText(request.Reason, 2000, nameof(request.Reason)),
            cancellationToken);
}
