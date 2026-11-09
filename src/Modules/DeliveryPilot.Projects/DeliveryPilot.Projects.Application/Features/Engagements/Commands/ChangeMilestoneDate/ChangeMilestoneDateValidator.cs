using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneDate;

public sealed class ChangeMilestoneDateValidator : IValidator<ChangeMilestoneDateCommand>
{
    public Task ValidateAsync(ChangeMilestoneDateCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            request.MilestoneId,
            request.ExpectedVersion,
            true,
            () =>
            {
                EngagementFields.CalendarDate(request.TargetDate ?? default, nameof(request.TargetDate));
                EngagementFields.RequiredText(request.Reason, 2000, nameof(request.Reason));
            },
            cancellationToken);
}
