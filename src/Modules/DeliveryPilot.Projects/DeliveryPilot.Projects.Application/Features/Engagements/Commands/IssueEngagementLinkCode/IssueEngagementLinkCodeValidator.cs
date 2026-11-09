using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.IssueEngagementLinkCode;

public sealed class IssueEngagementLinkCodeValidator : IValidator<IssueEngagementLinkCodeCommand>
{
    public Task ValidateAsync(IssueEngagementLinkCodeCommand request, CancellationToken cancellationToken)
        => MutationValidation.ValidateAsync(request.OrganizationId, request.EngagementId, null, request.ExpectedVersion, true, () => { }, cancellationToken);
}
