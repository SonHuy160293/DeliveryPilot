using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProjectStakeholder;

public sealed class CreateProjectStakeholderValidator : IValidator<CreateProjectStakeholderCommand>
{
    public Task ValidateAsync(CreateProjectStakeholderCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || request.ProjectId == Guid.Empty)
        {
            throw new CqrsValidationException(["A project is required."]);
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
