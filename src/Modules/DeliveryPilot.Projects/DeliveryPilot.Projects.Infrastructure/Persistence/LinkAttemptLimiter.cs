using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

/// <summary>Dedicated session lock and autocommit failures, independent of the command transaction.</summary>
public sealed class LinkAttemptLimiter(string connectionString, IOptions<EngagementLinkAttemptOptions> options) : ILinkAttemptLimiter
{
    private NpgsqlConnection? _connection;
    private Guid _lockedUserId;

    public int RetryAfterSeconds { get; private set; } = 1;

    public async Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            throw new InvalidOperationException("The attempt limiter already holds a lease.");
        }

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", connection);
            command.Parameters.AddWithValue("key", "engagement.link-attempt:v1:" + userId.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken);
            _connection = connection;
            _lockedUserId = userId;
            return new Lease(this, connection, userId);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<bool> IsLimitedAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = RequireLease(userId);
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*), min("AttemptedAt") FROM engagement."EngagementLinkAttempts"
            WHERE "UserId" = @user AND "AttemptedAt" > @since
            """,
            connection);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("since", now.ToUniversalTime() - options.Value.Window);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        bool limited = reader.GetInt64(0) >= options.Value.MaxFailures;
        if (limited)
        {
            DateTimeOffset oldest = reader.GetFieldValue<DateTimeOffset>(1);
            RetryAfterSeconds = Math.Max(1, (int)Math.Ceiling((oldest + options.Value.Window - now).TotalSeconds));
        }

        return limited;
    }

    public async Task RecordFailureAsync(Guid userId, Guid actingOrganizationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = RequireLease(userId);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO engagement."EngagementLinkAttempts" ("Id", "UserId", "ActingOrganizationId", "AttemptedAt")
            VALUES (@id, @user, @organization, @now)
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("organization", actingOrganizationId);
        command.Parameters.AddWithValue("now", now.ToUniversalTime());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlConnection RequireLease(Guid userId)
        => _connection is not null && _lockedUserId == userId ? _connection : throw new InvalidOperationException("An attempt lease is required.");

    private sealed class Lease(LinkAttemptLimiter owner, NpgsqlConnection connection, Guid userId) : IAsyncDisposable
    {
        private bool _disposed;

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended(@key, 0))", connection);
                command.Parameters.AddWithValue("key", "engagement.link-attempt:v1:" + userId.ToString("D"));
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally
            {
                owner._connection = null;
                owner._lockedUserId = Guid.Empty;
                await connection.DisposeAsync();
            }
        }
    }
}
