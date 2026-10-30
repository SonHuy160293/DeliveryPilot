using System.Text;

namespace DeliveryPilot.Identity.Infrastructure.Security;

/// <summary>
/// Issuer-side token settings. Binds the same <c>Jwt</c> section as the shared bearer validator so
/// that issuance and validation agree on key, issuer, audience and access lifetime. Lifetimes have
/// no code fallback: they must be configured (appsettings) and positive.
/// </summary>
public sealed class AuthTokenOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretKeyBytes = 32;

    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; init; }
    public int RefreshTokenExpirationDays { get; init; }

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenExpirationMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenExpirationDays);

    public override string ToString()
        => $"{nameof(AuthTokenOptions)} {{ Issuer = {Issuer}, Audience = {Audience}, AccessTokenExpirationMinutes = {AccessTokenExpirationMinutes}, RefreshTokenExpirationDays = {RefreshTokenExpirationDays} }}";

    internal bool HasValidSecretKey()
        => !string.IsNullOrWhiteSpace(SecretKey) && Encoding.UTF8.GetByteCount(SecretKey) >= MinimumSecretKeyBytes;
}
