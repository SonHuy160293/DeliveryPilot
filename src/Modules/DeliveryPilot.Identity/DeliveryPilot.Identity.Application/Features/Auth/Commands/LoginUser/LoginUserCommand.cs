using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;

public sealed record LoginUserCommand(string? Email, string? Password) : ICommand<LoginUserResult>
{
    public override string ToString() => nameof(LoginUserCommand);
}
