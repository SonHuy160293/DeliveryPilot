using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.UpdateEngagementDetails;

public sealed class UpdateEngagementDetailsValidator : IValidator<UpdateEngagementDetailsCommand>
{
    public Task ValidateAsync(UpdateEngagementDetailsCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            null,
            request.ExpectedVersion,
            true,
            () =>
            {
                EngagementFields.RequiredText(request.ClientDisplayName, 200, nameof(request.ClientDisplayName));
                EngagementFields.RequiredText(request.AgreementReference, 100, nameof(request.AgreementReference));
                EngagementFields.CalendarDate(request.AgreementSignedOn ?? default, nameof(request.AgreementSignedOn));
                EngagementFields.OptionalText(request.ClientLegalName, 200, nameof(request.ClientLegalName));
                EngagementFields.CountryCode(request.ClientCountryCode);
                EngagementFields.OptionalText(request.EngagementName, 200, nameof(request.EngagementName));
                EngagementFields.DocumentUrl(request.DocumentUrl);
                EngagementFields.OptionalText(request.Reason, 2000, nameof(request.Reason));
            },
            cancellationToken);
}
