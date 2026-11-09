using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementContact;

public sealed class AddEngagementContactValidator : IValidator<AddEngagementContactCommand>
{
    public Task ValidateAsync(AddEngagementContactCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            null,
            null,
            false,
            () =>
            {
                EngagementFields.RequiredText(request.Name, 200, nameof(request.Name));
                EngagementFields.Email(request.Email);
                EngagementContact.ValidateRole(request.ContactRole);
            },
            cancellationToken);
}
