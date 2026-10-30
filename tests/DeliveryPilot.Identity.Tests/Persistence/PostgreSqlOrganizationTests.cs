using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class PostgreSqlOrganizationTests
{
    [PostgreSqlFact]
    public async Task RemovalAndRejoinKeepIdentityAndLeavePriorGrantRevoked()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options;
        await using var context = new IdentityDbContext(options);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
        User user = User.Create($"{Guid.NewGuid():N}@example.test", "Test user");
        Organization organization = Organization.Create("Workspace", user.Id, now);
        OrganizationMembership membership = OrganizationMembership.Create(organization.Id, user.Id, user.Id, now);
        OrganizationRole adminRole = OrganizationRole.Create(organization.Id, "admin", "Admin", "system", 1, user.Id, now);
        OrganizationRole memberRole = OrganizationRole.Create(organization.Id, "member", "Member", "system", 1, user.Id, now);
        OrganizationRoleAssignment adminAssignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, adminRole.Id, user.Id, now);
        OrganizationRoleAssignment memberAssignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, memberRole.Id, user.Id, now);
        context.Users.Add(user);
        context.Organizations.Add(organization);
        context.OrganizationMemberships.Add(membership);
        context.OrganizationRoles.AddRange(adminRole, memberRole);
        context.OrganizationRoleAssignments.AddRange(adminAssignment, memberAssignment);
        await context.SaveChangesAsync();

        membership.Remove(user.Id, "Removal", now.AddMinutes(1), [adminAssignment, memberAssignment]);
        await context.SaveChangesAsync();
        membership.Rejoin(user.Id, now.AddMinutes(2));
        memberAssignment.Regrant(user.Id, now.AddMinutes(2), null, "Fresh member admission");
        membership.RecordAssignmentChange(now.AddMinutes(2));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        OrganizationMembership actualMembership = await context.OrganizationMemberships.SingleAsync(x => x.Id == membership.Id);
        OrganizationRoleAssignment actualAdminAssignment = await context.OrganizationRoleAssignments.SingleAsync(x => x.Id == adminAssignment.Id);
        OrganizationRoleAssignment actualMemberAssignment = await context.OrganizationRoleAssignments.SingleAsync(x => x.Id == memberAssignment.Id);
        Assert.Equal("active", actualMembership.Status);
        Assert.Equal(now, actualMembership.CreatedAt);
        Assert.Equal(4, actualMembership.Version);
        Assert.NotNull(actualAdminAssignment.RevokedAt);
        Assert.Equal(1, actualAdminAssignment.GrantVersion);
        Assert.Equal(2, actualAdminAssignment.Version);
        Assert.Null(actualMemberAssignment.RevokedAt);
        Assert.Equal(now, actualMemberAssignment.CreatedAt);
        Assert.Equal(2, actualMemberAssignment.GrantVersion);
        Assert.Equal(3, actualMemberAssignment.Version);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE shared."OrganizationMemberships"
            SET "Version" = "Version" + 1
            WHERE "Id" = {actualMembership.Id}
            """);
        actualMembership.RecordAssignmentChange(now.AddMinutes(3));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    public async Task WorkspaceConstraintsEnforceDurableAndScopedRelationships()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options;
        await using var context = new IdentityDbContext(options);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        User user = User.Create($"{Guid.NewGuid():N}@example.test", "Test user");
        context.Users.Add(user);
        Organization first = Organization.Create("First", user.Id, now);
        Organization second = Organization.Create("Second", user.Id, now);
        context.Organizations.AddRange(first, second);
        OrganizationMembership member = OrganizationMembership.Create(first.Id, user.Id, user.Id, now);
        context.OrganizationMemberships.Add(member);
        OrganizationRole role = OrganizationRole.Create(first.Id, "member", "Member", "system", 1, user.Id, now);
        context.OrganizationRoles.Add(role);
        OrganizationRolePermission permission = OrganizationRolePermission.Create(first.Id, role.Id, "organization.members.read");
        context.OrganizationRolePermissions.Add(permission);
        OrganizationRoleAssignment assignment = OrganizationRoleAssignment.Create(first.Id, member.Id, role.Id, user.Id, now);
        context.OrganizationRoleAssignments.Add(assignment);
        await context.SaveChangesAsync();

        await AssertConstraintAsync(context, transaction, "INSERT INTO shared.\"OrganizationMemberships\" (\"Id\", \"OrganizationId\", \"UserId\", \"Status\", \"StatusChangedAt\", \"CreatedAt\", \"UpdatedAt\", \"Version\") SELECT uuidv7(), \"OrganizationId\", \"UserId\", \"Status\", \"StatusChangedAt\", \"CreatedAt\", \"UpdatedAt\", \"Version\" FROM shared.\"OrganizationMemberships\" WHERE \"Id\" = @id", member.Id, PostgresErrorCodes.UniqueViolation);
        await AssertConstraintAsync(context, transaction, "INSERT INTO shared.\"OrganizationRoleAssignments\" (\"Id\", \"OrganizationId\", \"MembershipId\", \"RoleId\", \"GrantVersion\", \"GrantedByUserId\", \"GrantedAt\", \"CreatedAt\", \"UpdatedAt\", \"Version\") SELECT uuidv7(), \"OrganizationId\", \"MembershipId\", \"RoleId\", \"GrantVersion\", \"GrantedByUserId\", \"GrantedAt\", \"CreatedAt\", \"UpdatedAt\", \"Version\" FROM shared.\"OrganizationRoleAssignments\" WHERE \"Id\" = @id", assignment.Id, PostgresErrorCodes.UniqueViolation);
        await AssertConstraintAsync(context, transaction, "UPDATE shared.\"OrganizationRoleAssignments\" SET \"OrganizationId\" = @other WHERE \"Id\" = @id", assignment.Id, PostgresErrorCodes.ForeignKeyViolation, second.Id);
        await AssertConstraintAsync(context, transaction, "UPDATE shared.\"OrganizationRolePermissions\" SET \"OrganizationId\" = @other WHERE \"RoleId\" = @id", role.Id, PostgresErrorCodes.ForeignKeyViolation, second.Id);
        await AssertConstraintAsync(context, transaction, "INSERT INTO shared.\"OrganizationRoles\" (\"Id\", \"OrganizationId\", \"Code\", \"Name\", \"Origin\", \"TemplateVersion\", \"PermissionSetVersion\", \"CreatedAt\", \"UpdatedAt\", \"Version\") SELECT uuidv7(), \"OrganizationId\", 'MEMBER', 'Different name', \"Origin\", \"TemplateVersion\", \"PermissionSetVersion\", \"CreatedAt\", \"UpdatedAt\", \"Version\" FROM shared.\"OrganizationRoles\" WHERE \"Id\" = @id", role.Id, PostgresErrorCodes.UniqueViolation);
        await AssertConstraintAsync(context, transaction, "INSERT INTO shared.\"OrganizationRoles\" (\"Id\", \"OrganizationId\", \"Code\", \"Name\", \"Origin\", \"TemplateVersion\", \"PermissionSetVersion\", \"CreatedAt\", \"UpdatedAt\", \"Version\") SELECT uuidv7(), \"OrganizationId\", 'different', 'MEMBER', \"Origin\", \"TemplateVersion\", \"PermissionSetVersion\", \"CreatedAt\", \"UpdatedAt\", \"Version\" FROM shared.\"OrganizationRoles\" WHERE \"Id\" = @id", role.Id, PostgresErrorCodes.UniqueViolation);

        await transaction.RollbackAsync();
    }

    private static async Task AssertConstraintAsync(IdentityDbContext context, IDbContextTransaction transaction, string sql, Guid id, string expectedCode, Guid? other = null)
    {
        await transaction.CreateSavepointAsync("before_constraint");
        await using var command = (NpgsqlCommand)context.Database.GetDbConnection().CreateCommand();
        command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        command.CommandText = sql;
        command.Parameters.AddWithValue("id", id);
        if (other.HasValue)
        {
            command.Parameters.AddWithValue("other", other.Value);
        }

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(expectedCode, exception.SqlState);
        await transaction.RollbackToSavepointAsync("before_constraint");
    }
}
