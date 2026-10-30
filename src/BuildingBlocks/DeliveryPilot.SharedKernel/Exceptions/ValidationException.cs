using DeliveryPilot.SharedKernel.Abstractions.Exceptions;

namespace DeliveryPilot.SharedKernel.Exceptions;

public sealed class ValidationException : DomainException
{
    public ValidationException(IReadOnlyCollection<ValidationError> errors)
        : base("Validation.Failure", "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyCollection<ValidationError> Errors { get; }
}
