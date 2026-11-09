using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementMilestone;

public sealed class AddEngagementMilestoneValidator : IValidator<AddEngagementMilestoneCommand>
{
    public Task ValidateAsync(AddEngagementMilestoneCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            null,
            null,
            false,
            () =>
            {
                EngagementFields.RequiredText(request.Name, 200, nameof(request.Name));
                EngagementFields.OptionalText(request.Description, 2000, nameof(request.Description));
                EngagementFields.CalendarDate(request.TargetDate ?? default, nameof(request.TargetDate));
            },
            cancellationToken);
}
