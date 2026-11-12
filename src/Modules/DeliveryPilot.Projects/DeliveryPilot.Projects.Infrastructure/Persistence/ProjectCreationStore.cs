using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class ProjectCreationStore(ProjectsDbContext dbContext) : IProjectCreationStore
{
    private const string _operation = "project.create";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task LockAttemptAsync(Guid organizationId, Guid userId, Guid key, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{_operation}:v1:{organizationId:D}:{userId:D}:{key:D}"));
        long lockId = BinaryPrimitives.ReadInt64BigEndian(digest);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
    }

    public async Task<ProjectCreationSnapshot?> FindReplayAsync(Guid organizationId, Guid userId, Guid key, ProjectCreationDetails details, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        string ownerHash = OwnerHash(organizationId, userId);
        string keyHash = Hash(key.ToString("D"));
        IdempotencyRecord? receipt = await dbContext.Set<IdempotencyRecord>().AsNoTracking().SingleOrDefaultAsync(record => record.Operation == _operation && record.OwnerScopeHash == ownerHash && record.KeyHash == keyHash, cancellationToken);
        if (receipt is null) { return null; }
        receipt.EnsureRequestMatches(Fingerprint(details));
        if (receipt.Status != IdempotencyStatus.Completed) { throw new ConflictException("Idempotency.InProgress", "This creation intent has not completed."); }
        ProjectEvent? entry = await dbContext.ProjectEvents.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.ProjectId == receipt.ResultResourceId && candidate.ActorUserId == userId && candidate.EventType == "project.created", cancellationToken);
        ProjectCreationSnapshot? snapshot = entry is null ? null : JsonSerializer.Deserialize<ProjectCreationSnapshot>(entry.Details, _jsonOptions);
        if (snapshot is null || snapshot.Id != receipt.ResultResourceId || snapshot.Version != 1 || snapshot.Status != Project.DraftStatus) { throw new InvalidOperationException("The project creation receipt is invalid."); }
        return snapshot;
    }

    public Task<Engagement?> LockEngagementAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        return dbContext.Engagements.FromSqlInterpolated($"""
            SELECT * FROM engagement."Engagements"
            WHERE "OrganizationId" = {organizationId} AND "Id" = {engagementId}
            FOR SHARE
            """).SingleOrDefaultAsync(cancellationToken);
    }

    public void Stage(ProjectCreation creation, ProjectEvent entry, Guid userId, Guid key, ProjectCreationDetails details, DateTimeOffset now)
    {
        EnsureTransaction();
        Guid lease = Guid.CreateVersion7();
        IdempotencyRecord receipt = IdempotencyRecord.Create(_operation, OwnerHash(creation.Project.OrganizationId, userId), Hash(key.ToString("D")), Fingerprint(details), lease, now, now.AddMinutes(1));
        receipt.Complete(lease, creation.Project.Id, "project.created", now);
        dbContext.Projects.Add(creation.Project);
        dbContext.ProjectBriefs.Add(creation.Brief);
        dbContext.ProjectParticipants.Add(creation.Participant);
        dbContext.ProjectParticipantRoles.Add(creation.AdministratorRole);
        dbContext.ProjectEvents.Add(entry);
        dbContext.Set<IdempotencyRecord>().Add(receipt);
    }

    private static string OwnerHash(Guid organizationId, Guid userId) => Hash($"v1:{organizationId:D}:{userId:D}");
    private static string Fingerprint(ProjectCreationDetails details) => Hash(JsonSerializer.Serialize(details, _jsonOptions));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null) { throw new InvalidOperationException("Project creation requires the pipeline transaction."); }
    }
}
