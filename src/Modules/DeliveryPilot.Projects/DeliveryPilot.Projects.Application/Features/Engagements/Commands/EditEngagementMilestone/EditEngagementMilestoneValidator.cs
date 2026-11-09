using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementMilestone;

public sealed class EditEngagementMilestoneValidator : IValidator<EditEngagementMilestoneCommand>
{
    public Task ValidateAsync(EditEngagementMilestoneCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            request.MilestoneId,
            request.ExpectedVersion,
            true,
            () =>
            {
                EngagementFields.RequiredText(request.Name, 200, nameof(request.Name));
                EngagementFields.OptionalText(request.Description, 2000, nameof(request.Description));
            },
            cancellationToken);
}
