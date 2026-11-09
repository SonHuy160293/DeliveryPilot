namespace DeliveryPilot.Identity.Application.Abstractions.Security;

/// <summary>
/// Credentials produced for an authenticated session. Both tokens are reusable secrets:
/// deliver them only through a reviewed successful response and never log or persist them.
/// All instants are UTC.
/// </summary>
public sealed record IssuedAuthSession(
    Guid SessionId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
{
    /// <summary>Gets the subject of the session, set by the issuing service.</summary>
    public Guid UserId { get; init; }

    public override string ToString()
        => $"{nameof(IssuedAuthSession)} {{ SessionId = {SessionId}, UserId = {UserId}, AccessTokenExpiresAt = {AccessTokenExpiresAt:O}, RefreshTokenExpiresAt = {RefreshTokenExpiresAt:O} }}";
}
