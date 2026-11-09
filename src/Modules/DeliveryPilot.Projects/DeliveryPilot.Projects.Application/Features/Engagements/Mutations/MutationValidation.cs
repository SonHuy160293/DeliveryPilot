using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Mutations;

public static class MutationValidation
{
    public static Task ValidateAsync(Guid organizationId, Guid engagementId, Guid? childId, long? version, bool requireVersion, Action validateFields, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        if (organizationId == Guid.Empty || engagementId == Guid.Empty || childId == Guid.Empty || (requireVersion && version is null or <= 0))
        {
            errors.Add("Nonempty identifiers and a positive expectedVersion are required.");
        }

        try
        {
            validateFields();
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
