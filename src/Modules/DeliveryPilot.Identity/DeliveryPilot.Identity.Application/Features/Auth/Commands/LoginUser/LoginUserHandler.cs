using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;

public sealed class LoginUserHandler(
    IUserRepository users,
    IPasswordCredentialRepository credentials,
    IPasswordHashService passwordHasher,
    IAuthSessionService authSessions,
    TimeProvider timeProvider)
    : ICommandHandler<LoginUserCommand, LoginUserResult>
{
    public const string InvalidCredentialsCode = "Authentication.InvalidCredentials";
    public const string InvalidCredentialsMessage = "Invalid email or password.";
    public const string BearerTokenType = "Bearer";
    private const string ActiveStatus = "active";

    public async Task<Result<LoginUserResult>> HandleAsync(LoginUserCommand request, CancellationToken cancellationToken)
    {
        string email = User.NormalizeEmail(request.Email!);
        User? user = await users.FindByEmailForLoginAsync(email, cancellationToken);
        PasswordCredential? credential = user is null
            ? null
            : await credentials.GetByUserIdAsync(user.Id, cancellationToken);

        bool passwordValid = await passwordHasher.VerifyAsync(user, credential?.PasswordHash, request.Password!, cancellationToken);
        if (!passwordValid || user is null || user.Status != ActiveStatus)
        {
            throw new UnauthorizedException(InvalidCredentialsCode, InvalidCredentialsMessage);
        }

        user.RecordLogin(timeProvider.GetUtcNow());
        IssuedAuthSession? session = await authSessions.CreateAsync(user.Id, cancellationToken);
        if (session is null)
        {
            throw new UnauthorizedException(InvalidCredentialsCode, InvalidCredentialsMessage);
        }

        var response = new LoginUserResponse(
            user.Id,
            session.AccessToken,
            BearerTokenType,
            session.AccessTokenExpiresAt);
        return Result.Success(new LoginUserResult(response, session.RefreshToken, session.RefreshTokenExpiresAt));
    }
}
