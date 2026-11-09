using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementContact;

public sealed class RemoveEngagementContactValidator : IValidator<RemoveEngagementContactCommand>
{
    public Task ValidateAsync(RemoveEngagementContactCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            request.ContactId,
            request.ExpectedVersion,
            true,
            () => EngagementFields.OptionalText(request.Reason, 2000, nameof(request.Reason)),
            cancellationToken);
}
