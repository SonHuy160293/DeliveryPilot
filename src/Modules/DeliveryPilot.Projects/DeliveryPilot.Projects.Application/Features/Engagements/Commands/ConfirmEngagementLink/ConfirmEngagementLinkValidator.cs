using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ConfirmEngagementLink;

public sealed class ConfirmEngagementLinkValidator : IValidator<ConfirmEngagementLinkCommand>
{
    public Task ValidateAsync(ConfirmEngagementLinkCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 64)
        {
            throw new CqrsValidationException(["A workspace and a nonblank code of at most 64 characters are required."]);
        }

        return Task.CompletedTask;
    }
}
