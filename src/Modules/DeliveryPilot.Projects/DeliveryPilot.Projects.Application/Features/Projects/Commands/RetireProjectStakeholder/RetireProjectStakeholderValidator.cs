using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.RetireProjectStakeholder;

public sealed class RetireProjectStakeholderValidator : IValidator<RetireProjectStakeholderCommand>
{
    public Task ValidateAsync(RetireProjectStakeholderCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || request.ProjectId == Guid.Empty || request.StakeholderId == Guid.Empty || request.ExpectedVersion is null or <= 0)
        {
            throw new CqrsValidationException(["A stakeholder and positive expected version are required."]);
        }

        return Task.CompletedTask;
    }
}
