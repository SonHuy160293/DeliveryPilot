using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeliveryPilot.Identity.Infrastructure.Security;

/// <summary>
/// PostgreSQL-backed Identity sessions. Refresh and revocation lock the same session row
/// (<c>SELECT ... FOR UPDATE</c>) inside the pipeline-owned command transaction, so they are
/// serialized: at most one concurrent use of a refresh token succeeds, and a committed revocation
/// cannot be undone by a refresh. This service never calls <c>SaveChangesAsync</c>.
/// </summary>
public sealed class AuthSessionService(
    IdentityDbContext dbContext,
    JwtAccessTokenIssuer accessTokenIssuer,
    IOptions<AuthTokenOptions> options,
    TimeProvider timeProvider) : IAuthSessionService
{
    internal const string ActiveUserStatus = "active";

    public async Task<IssuedAuthSession?> CreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureCommandTransaction();
        if (userId == Guid.Empty || !await IsUserActiveAsync(userId, cancellationToken))
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        var sessionId = Guid.NewGuid();
        (string refreshToken, byte[] verifier) = RefreshTokenCodec.Create(sessionId);
        var session = AuthSession.Create(sessionId, userId, verifier, now, now.Add(options.Value.RefreshTokenLifetime));
        await dbContext.AuthSessions.AddAsync(session, cancellationToken);

        (string accessToken, DateTimeOffset accessExpiresAt) = accessTokenIssuer.Issue(userId, sessionId, now);
        return new IssuedAuthSession(sessionId, accessToken, accessExpiresAt, refreshToken, session.RefreshExpiresAt) { UserId = userId };
    }

    public async Task<IssuedAuthSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        EnsureCommandTransaction();
        if (!RefreshTokenCodec.TryParse(refreshToken, out Guid sessionId, out byte[] presentedVerifier))
        {
            return null;
        }

        AuthSession? session = await LockSessionAsync(sessionId, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (session is null
            || !RefreshTokenCodec.VerifierMatches(session.RefreshTokenVerifier, presentedVerifier)
            || !session.CanRefreshAt(now)
            || !await IsUserActiveAsync(session.UserId, cancellationToken))
        {
            // An old rotated token is rejected without revoking the still-valid current session.
            return null;
        }

        (string nextRefreshToken, byte[] nextVerifier) = RefreshTokenCodec.Create(session.Id);
        session.RotateRefreshToken(nextVerifier, now);
        (string accessToken, DateTimeOffset accessExpiresAt) = accessTokenIssuer.Issue(session.UserId, session.Id, now);
        return new IssuedAuthSession(session.Id, accessToken, accessExpiresAt, nextRefreshToken, session.RefreshExpiresAt) { UserId = session.UserId };
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        EnsureCommandTransaction();
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return false;
        }

        AuthSession? session = await LockSessionAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return false;
        }

        session.Revoke(timeProvider.GetUtcNow());
        return true;
    }

    public async Task<bool> RevokeByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        EnsureCommandTransaction();
        if (!RefreshTokenCodec.TryParse(refreshToken, out Guid sessionId, out byte[] presentedVerifier))
        {
            return false;
        }

        AuthSession? session = await LockSessionAsync(sessionId, cancellationToken);
        if (session is null || !RefreshTokenCodec.VerifierMatches(session.RefreshTokenVerifier, presentedVerifier))
        {
            return false;
        }

        session.Revoke(timeProvider.GetUtcNow());
        return true;
    }

    private async Task<AuthSession?> LockSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        List<AuthSession> sessions = await dbContext.AuthSessions
            .FromSqlInterpolated($"""SELECT * FROM shared."AuthSessions" WHERE "Id" = {sessionId} FOR UPDATE""")
            .AsTracking()
            .ToListAsync(cancellationToken);
        return sessions.Count == 1 ? sessions[0] : null;
    }

    private Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken)
        => dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.Status == ActiveUserStatus, cancellationToken);

    private void EnsureCommandTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Session changes require the pipeline-owned command transaction.");
        }
    }
}
