using DeliveryPilot.SharedKernel.Abstractions.Exceptions;

namespace DeliveryPilot.SharedKernel.Exceptions;

public class UnauthorizedException : DomainException
{
    public UnauthorizedException()
        : this("Authentication is required to access this resource.")
    {
    }

    public UnauthorizedException(string message)
        : base("Unauthorized", message)
    {
    }

    public UnauthorizedException(string code, string message)
        : base(code, message)
    {
    }
}
