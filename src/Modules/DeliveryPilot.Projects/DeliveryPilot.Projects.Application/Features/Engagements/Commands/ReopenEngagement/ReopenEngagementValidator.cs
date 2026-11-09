using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ReopenEngagement;

public sealed class ReopenEngagementValidator : IValidator<ReopenEngagementCommand>
{
    public Task ValidateAsync(ReopenEngagementCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            null,
            request.ExpectedVersion,
            true,
            () => EngagementFields.RequiredText(request.Reason, 2000, nameof(request.Reason)),
            cancellationToken);
}
