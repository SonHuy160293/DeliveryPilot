using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.PreviewEngagementLink;

public sealed class PreviewEngagementLinkValidator : IValidator<PreviewEngagementLinkCommand>
{
    public Task ValidateAsync(PreviewEngagementLinkCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 64)
        {
            throw new CqrsValidationException(["A workspace and a nonblank code of at most 64 characters are required."]);
        }

        return Task.CompletedTask;
    }
}
