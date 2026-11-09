using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneStatus;

public sealed class ChangeMilestoneStatusValidator : IValidator<ChangeMilestoneStatusCommand>
{
    public Task ValidateAsync(ChangeMilestoneStatusCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            request.MilestoneId,
            request.ExpectedVersion,
            true,
            () =>
            {
                EngagementFields.RequiredText(request.Status, 16, nameof(request.Status));
                EngagementFields.OptionalText(request.Reason, 2000, nameof(request.Reason));
            },
            cancellationToken);
}
