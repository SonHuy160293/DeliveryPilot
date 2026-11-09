using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementContact;

public sealed class EditEngagementContactValidator : IValidator<EditEngagementContactCommand>
{
    public Task ValidateAsync(EditEngagementContactCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            request.ContactId,
            request.ExpectedVersion,
            true,
            () =>
            {
                EngagementFields.RequiredText(request.Name, 200, nameof(request.Name));
                EngagementFields.Email(request.Email);
                EngagementContact.ValidateRole(request.ContactRole);
            },
            cancellationToken);
}
