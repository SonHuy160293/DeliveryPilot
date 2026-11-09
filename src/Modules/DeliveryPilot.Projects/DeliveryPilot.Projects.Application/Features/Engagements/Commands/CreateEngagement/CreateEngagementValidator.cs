using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;

public sealed class CreateEngagementValidator : IValidator<CreateEngagementCommand>
{
    public Task ValidateAsync(CreateEngagementCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        if (request.OrganizationId == Guid.Empty)
        {
            errors.Add("A nonempty organization identifier is required.");
        }

        if (request.IdempotencyKey == Guid.Empty)
        {
            errors.Add("Idempotency-Key must be a nonempty UUID.");
        }

        try
        {
            EngagementCreationDetails.Normalize(request);
        }
        catch (BadRequestException exception)
        {
            errors.Add(exception.Message);
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
