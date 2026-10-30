namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;

public sealed record LoginUserResult(
    LoginUserResponse Response,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
{
    public override string ToString()
        => $"{nameof(LoginUserResult)} {{ UserId = {Response.UserId}, ExpiresAt = {Response.ExpiresAt:O}, RefreshTokenExpiresAt = {RefreshTokenExpiresAt:O} }}";
}
