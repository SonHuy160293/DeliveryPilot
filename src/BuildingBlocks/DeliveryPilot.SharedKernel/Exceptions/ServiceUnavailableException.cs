using DeliveryPilot.SharedKernel.Abstractions.Exceptions;

namespace DeliveryPilot.SharedKernel.Exceptions;

public sealed class ServiceUnavailableException : DomainException
{
    public ServiceUnavailableException(string code, string message)
        : base(code, message)
    {
    }
}
