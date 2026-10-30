using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.RegisterUser;

public sealed record RegisterUserCommand(string? DisplayName, string? Email, string? Password) : ICommand<RegisterUserResponse>
{
    public override string ToString() => nameof(RegisterUserCommand);
}
