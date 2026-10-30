using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Commands.CreateOrganization;

public sealed class CreateOrganizationValidator : IValidator<CreateOrganizationCommand>
{
    public Task ValidateAsync(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
        {
            errors.Add("Name must contain between 1 and 160 characters.");
        }

        if (request.AttemptId == Guid.Empty)
        {
            errors.Add("Idempotency-Key must be a nonempty UUID.");
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
