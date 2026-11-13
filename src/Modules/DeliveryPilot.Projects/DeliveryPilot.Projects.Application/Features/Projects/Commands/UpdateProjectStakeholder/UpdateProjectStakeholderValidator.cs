using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectStakeholder;

public sealed class UpdateProjectStakeholderValidator : IValidator<UpdateProjectStakeholderCommand>
{
    public Task ValidateAsync(UpdateProjectStakeholderCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || request.ProjectId == Guid.Empty || request.StakeholderId == Guid.Empty || request.ExpectedVersion is null or <= 0)
        {
            throw new CqrsValidationException(["A stakeholder and positive expected version are required."]);
        }

        try
        {
            request.Values.Normalize();
        }
        catch (BadRequestException error)
        {
            throw new CqrsValidationException([error.Message]);
        }

        return Task.CompletedTask;
    }
}
