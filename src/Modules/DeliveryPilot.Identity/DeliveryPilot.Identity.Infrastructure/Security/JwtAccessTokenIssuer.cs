using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DeliveryPilot.Identity.Infrastructure.Security;

/// <summary>
/// Signs access JWTs with the pinned HS256 algorithm and the configured symmetric key. The claim set is
/// limited to server-owned identity data: <c>sub</c>, <c>sid</c>, <c>jti</c>, <c>iat</c>, <c>nbf</c>,
/// <c>exp</c>, <c>iss</c> and <c>aud</c>. No tenant, role or permission claim is issued.
/// </summary>
public sealed class JwtAccessTokenIssuer
{
    public const string SigningAlgorithm = SecurityAlgorithms.HmacSha256;

    private readonly AuthTokenOptions _options;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public JwtAccessTokenIssuer(IOptions<AuthTokenOptions> options)
    {
        _options = options.Value;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            SigningAlgorithm);
    }

    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId, Guid sessionId, DateTimeOffset now)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            throw new ArgumentException("A token requires nonempty user and session identifiers.");
        }

        // JWT NumericDate has second precision; truncate so the reported expiry equals the signed exp.
        DateTimeOffset issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        DateTimeOffset expiresAt = issuedAt.Add(_options.AccessTokenLifetime);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Sid] = sessionId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };

        return (_handler.CreateToken(descriptor), expiresAt);
    }
}
