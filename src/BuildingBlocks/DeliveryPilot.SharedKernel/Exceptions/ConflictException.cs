using DeliveryPilot.SharedKernel.Abstractions.Exceptions;

namespace DeliveryPilot.SharedKernel.Exceptions;

public class ConflictException : DomainException
{
    public ConflictException(string code, string message)
        : base(code, message) { }

    public ConflictException(string message)
        : this("Conflict", message) { }
}
