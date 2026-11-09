using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.LogoutSession;

/// <summary>
/// Ends the caller's current session. <see cref="UserId"/> and <see cref="SessionId"/> come only from a
/// validated access token; <see cref="RefreshToken"/> comes only from the refresh cookie and is a secret.
/// The response is true when a session was revoked; callers report success either way.
/// </summary>
public sealed record LogoutSessionCommand(Guid? UserId, Guid? SessionId, string? RefreshToken) : ICommand<bool>
{
    public override string ToString() => $"{nameof(LogoutSessionCommand)} {{ UserId = {UserId}, SessionId = {SessionId} }}";
}
