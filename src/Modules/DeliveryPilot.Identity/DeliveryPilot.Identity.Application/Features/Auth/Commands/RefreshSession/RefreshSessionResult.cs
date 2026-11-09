using DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.RefreshSession;

/// <summary>
/// Successful refresh. <see cref="Response"/> is returned in the body; the rotated refresh token is
/// delivered only through the HttpOnly cookie.
/// </summary>
public sealed record RefreshSessionResult(
    LoginUserResponse Response,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
{
    public override string ToString()
        => $"{nameof(RefreshSessionResult)} {{ UserId = {Response.UserId}, ExpiresAt = {Response.ExpiresAt:O}, RefreshTokenExpiresAt = {RefreshTokenExpiresAt:O} }}";
}
