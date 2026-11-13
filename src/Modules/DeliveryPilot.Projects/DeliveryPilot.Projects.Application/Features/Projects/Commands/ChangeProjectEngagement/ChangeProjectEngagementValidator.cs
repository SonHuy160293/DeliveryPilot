using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeProjectEngagement;

public sealed class ChangeProjectEngagementValidator : IValidator<ChangeProjectEngagementCommand>
{
    public Task ValidateAsync(ChangeProjectEngagementCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || request.ProjectId == Guid.Empty || request.ExpectedVersion is null or <= 0 || request.EngagementId == Guid.Empty || request.Kind is not (Project.ClientKind or Project.InternalKind) || (request.Kind == Project.InternalKind && request.EngagementId.HasValue) || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000)
        {
            throw new CqrsValidationException(["Valid project settings, a positive expected version and a reason are required."]);
        }

        return Task.CompletedTask;
    }
}
