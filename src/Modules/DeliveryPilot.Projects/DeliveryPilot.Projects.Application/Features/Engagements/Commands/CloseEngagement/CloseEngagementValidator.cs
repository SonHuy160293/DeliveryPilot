using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CloseEngagement;

public sealed class CloseEngagementValidator : IValidator<CloseEngagementCommand>
{
    public Task ValidateAsync(CloseEngagementCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            null,
            request.ExpectedVersion,
            true,
            () => EngagementFields.RequiredText(request.Reason, 2000, nameof(request.Reason)),
            cancellationToken);
}
