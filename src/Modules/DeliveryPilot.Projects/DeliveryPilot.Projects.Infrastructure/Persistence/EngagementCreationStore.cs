using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class EngagementCreationStore(ProjectsDbContext dbContext) : IEngagementCreationStore
{
    private const string _operation = "engagement.create";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task LockAttemptAsync(Guid organizationId, Guid userId, Guid key, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{_operation}:v1:{organizationId:D}:{userId:D}:{key:D}"));
        long lockId = BinaryPrimitives.ReadInt64BigEndian(digest);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
    }

    public Task LockReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken)
        => EngagementReferenceQueries.LockAsync(dbContext, organizationId, reference, cancellationToken);

    public async Task<EngagementCreationSnapshot?> FindReplayAsync(Guid organizationId, Guid userId, Guid key, EngagementCreationDetails details, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        string ownerHash = OwnerHash(organizationId, userId);
        string keyHash = Hash(key.ToString("D"));
        IdempotencyRecord? receipt = await dbContext.Set<IdempotencyRecord>().AsNoTracking().SingleOrDefaultAsync(
            record => record.Operation == _operation && record.OwnerScopeHash == ownerHash && record.KeyHash == keyHash, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        receipt.EnsureRequestMatches(Fingerprint(details));
        if (receipt.Status != IdempotencyStatus.Completed)
        {
            throw new ConflictException("Idempotency.InProgress", "This creation intent has not completed.");
        }

        EngagementEvent? entry = await dbContext.EngagementEvents.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.EngagementId == receipt.ResultResourceId
                && candidate.ActorUserId == userId && candidate.EventType == "engagement.created", cancellationToken);
        EngagementCreationSnapshot? snapshot = entry is null ? null : JsonSerializer.Deserialize<EngagementCreationSnapshot>(entry.Details, _jsonOptions);
        if (snapshot is null || snapshot.Id != receipt.ResultResourceId || snapshot.Version != 1 || snapshot.Status != Engagement.ActiveStatus)
        {
            throw new InvalidOperationException("The engagement creation receipt is invalid.");
        }

        return snapshot;
    }

    public Task<bool> HasActiveReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken)
        => EngagementReferenceQueries.HasOtherActiveAsync(dbContext, organizationId, reference, Guid.Empty, cancellationToken);

    public void Stage(Engagement engagement, EngagementEvent entry, Guid userId, Guid key, EngagementCreationDetails details, DateTimeOffset now)
    {
        EnsureTransaction();
        var lease = Guid.CreateVersion7();
        var receipt = IdempotencyRecord.Create(_operation, OwnerHash(engagement.OrganizationId, userId), Hash(key.ToString("D")), Fingerprint(details), lease, now, now.AddMinutes(1));
        receipt.Complete(lease, engagement.Id, "engagement.created", now);
        dbContext.Engagements.Add(engagement);
        dbContext.EngagementEvents.Add(entry);
        dbContext.Set<IdempotencyRecord>().Add(receipt);
    }

    private static string OwnerHash(Guid organizationId, Guid userId) => Hash($"v1:{organizationId:D}:{userId:D}");
    private static string Fingerprint(EngagementCreationDetails details) => Hash(JsonSerializer.Serialize(details, _jsonOptions));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Engagement creation requires the pipeline transaction.");
        }
    }
}
