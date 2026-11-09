using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.RefreshSession;

/// <summary>Rotates the session identified by the presented refresh token. The token is a secret.</summary>
public sealed record RefreshSessionCommand(string? RefreshToken) : ICommand<RefreshSessionResult>
{
    public override string ToString() => nameof(RefreshSessionCommand);
}
