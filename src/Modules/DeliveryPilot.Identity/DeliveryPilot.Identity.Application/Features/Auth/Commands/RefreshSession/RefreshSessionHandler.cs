using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.RefreshSession;

public sealed class RefreshSessionHandler(IAuthSessionService authSessions)
    : ICommandHandler<RefreshSessionCommand, RefreshSessionResult>
{
    public const string InvalidRefreshTokenCode = "Authentication.InvalidRefreshToken";
    public const string InvalidRefreshTokenMessage = "The session is no longer valid. Please log in again.";

    public async Task<Result<RefreshSessionResult>> HandleAsync(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        IssuedAuthSession? session = string.IsNullOrEmpty(request.RefreshToken)
            ? null
            : await authSessions.RefreshAsync(request.RefreshToken, cancellationToken);
        if (session is null)
        {
            throw new UnauthorizedException(InvalidRefreshTokenCode, InvalidRefreshTokenMessage);
        }

        var response = new LoginUserResponse(
            session.UserId,
            session.AccessToken,
            LoginUserHandler.BearerTokenType,
            session.AccessTokenExpiresAt);
        return Result.Success(new RefreshSessionResult(response, session.RefreshToken, session.RefreshTokenExpiresAt));
    }
}
