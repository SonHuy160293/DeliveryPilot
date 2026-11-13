using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeSprintLength;

public sealed class ChangeSprintLengthValidator : IValidator<ChangeSprintLengthCommand>
{
    public Task ValidateAsync(ChangeSprintLengthCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId == Guid.Empty || request.ProjectId == Guid.Empty || request.ExpectedVersion is null or <= 0 || request.SprintLengthWeeks is null or < 1 or > 4)
        {
            throw new CqrsValidationException(["A project, positive expected version and Sprint length of one to four weeks are required."]);
        }

        return Task.CompletedTask;
    }
}
