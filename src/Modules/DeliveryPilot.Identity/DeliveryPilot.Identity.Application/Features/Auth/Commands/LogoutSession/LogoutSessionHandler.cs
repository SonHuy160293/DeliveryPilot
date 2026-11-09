using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.LogoutSession;

/// <summary>
/// Idempotent logout. Prefers the session named by a validated access token; otherwise uses a refresh
/// token that is still the session's current token. Nothing to revoke is a successful outcome.
/// </summary>
public sealed class LogoutSessionHandler(IAuthSessionService authSessions)
    : ICommandHandler<LogoutSessionCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(LogoutSessionCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId is { } userId && request.SessionId is { } sessionId
            && await authSessions.RevokeAsync(userId, sessionId, cancellationToken))
        {
            return Result.Success(true);
        }

        bool revoked = !string.IsNullOrEmpty(request.RefreshToken)
            && await authSessions.RevokeByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        return Result.Success(revoked);
    }
}
