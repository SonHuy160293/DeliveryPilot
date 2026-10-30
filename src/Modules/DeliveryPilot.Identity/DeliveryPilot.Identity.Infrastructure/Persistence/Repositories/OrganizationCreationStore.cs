using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class OrganizationCreationStore(IdentityDbContext dbContext) : IOrganizationCreationStore
{
    public async Task LockAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"organization.create:v1:{userId:D}:{attemptId:D}"));
        long lockId = BinaryPrimitives.ReadInt64BigEndian(digest);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
    }

    public Task<OrganizationCreationAttempt?> FindAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken)
        => dbContext.Set<OrganizationCreationAttempt>().AsNoTracking()
            .SingleOrDefaultAsync(attempt => attempt.UserId == userId && attempt.AttemptId == attemptId, cancellationToken);

    public async Task LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        List<User> users = await dbContext.Users
            .FromSqlInterpolated($"""SELECT * FROM shared."Users" WHERE "Id" = {userId} FOR UPDATE""")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (users.Count != 1 || users[0].Status != AuthSessionService.ActiveUserStatus)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }
    }

    public async Task LockSessionAndCheckEligibilityAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        List<AuthSession> sessions = await dbContext.AuthSessions
            .FromSqlInterpolated($"""SELECT * FROM shared."AuthSessions" WHERE "Id" = {sessionId} FOR UPDATE""")
            .AsNoTracking().ToListAsync(cancellationToken);
        bool activeUser = await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.Status == AuthSessionService.ActiveUserStatus, cancellationToken);
        if (!activeUser || sessions.Count != 1 || sessions[0].UserId != userId || sessions[0].RevokedAt is not null)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }
    }

    public void Stage(
        Organization organization,
        IReadOnlyCollection<OrganizationRole> roles,
        IReadOnlyCollection<OrganizationRolePermission> permissions,
        OrganizationMembership membership,
        OrganizationRoleAssignment assignment,
        OrganizationCreationAttempt attempt)
    {
        EnsureTransaction();
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationRoles.AddRange(roles);
        dbContext.OrganizationRolePermissions.AddRange(permissions);
        dbContext.OrganizationMemberships.Add(membership);
        dbContext.OrganizationRoleAssignments.Add(assignment);
        dbContext.Set<OrganizationCreationAttempt>().Add(attempt);
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Organization creation requires the pipeline-owned transaction.");
        }
    }
}
