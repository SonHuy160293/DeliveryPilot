using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class UserMigrationTests
{
    [PostgreSqlFact]
    public async Task PermissionPolicyMigrationPreservesExistingRowsAndPinsTheirBaseline()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.Context();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261104025115_AddOrganizationCreationAttempts");
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("policy-migration@example.test", "Policy migration", status: "active");
        var organization = Organization.Create("Policy migration", user.Id, now);
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, now);
        context.Users.Add(user);
        context.Organizations.Add(organization);
        context.OrganizationRoles.AddRange(seed.Roles);
        await context.SaveChangesAsync();
        const string unknownCode = "future.unknown.permission";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO shared."OrganizationRolePermissions" ("OrganizationId", "RoleId", "PermissionCode")
            VALUES ({organization.Id}, {seed.OwnerRole.Id}, {OrganizationCapabilityCatalog.AuditRead}),
                   ({organization.Id}, {seed.OwnerRole.Id}, {unknownCode})
            """);

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
        List<OrganizationRolePermission> rows = await context.OrganizationRolePermissions
            .Where(row => row.RoleId == seed.OwnerRole.Id).ToListAsync();

        // The pinned baseline rows stay at version 1; the later catalog policy v2 upgrade adds only its three codes.
        List<OrganizationRolePermission> baseline = rows.Where(row => row.CatalogPolicyVersion == 1).ToList();
        Assert.Equal(2, baseline.Count);
        Assert.Equal(
            [OrganizationCapabilityCatalog.EngagementsCreate, OrganizationCapabilityCatalog.EngagementsReadAll, OrganizationCapabilityCatalog.ProjectsCreate],
            rows.Where(row => row.CatalogPolicyVersion == 2).Select(row => row.PermissionCode).Order(StringComparer.Ordinal));
        Assert.True(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.OwnerRole, rows).SetEquals(
        [
            OrganizationCapabilityCatalog.AuditRead,
            OrganizationCapabilityCatalog.EngagementsCreate,
            OrganizationCapabilityCatalog.EngagementsReadAll,
            OrganizationCapabilityCatalog.ProjectsCreate,
        ]));

        await migrator.MigrateAsync("20261104025115_AddOrganizationCreationAttempts");
        int preserved = await context.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM shared."OrganizationRolePermissions"
            """).SingleAsync();
        Assert.Equal(2, preserved);
    }

    [PostgreSqlFact]
    public async Task CatalogPolicyV2UpgradeGrantsBuiltInOwnerAndAdminOnceAndRollsBack()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.Context();
        string[] migrations = context.Database.GetMigrations().ToArray();
        string upgrade = migrations.Single(migration => migration.EndsWith("_UpgradeBuiltInRolesToCatalogPolicyV2", StringComparison.Ordinal));
        string previous = migrations[Array.IndexOf(migrations, upgrade) - 1];
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(previous);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create($"catalog-v2-{Guid.NewGuid():N}@example.test", "Catalog Owner", status: "active");
        var organization = Organization.Create("Catalog v2 workspace", user.Id, now);
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, now);
        var custom = OrganizationRole.Create(organization.Id, "custom-reader", "Custom reader", "custom", null, user.Id, now);
        context.Users.Add(user);
        context.Organizations.Add(organization);
        context.OrganizationRoles.AddRange(seed.Roles);
        context.OrganizationRoles.Add(custom);
        context.OrganizationRolePermissions.AddRange(seed.Permissions);
        context.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(organization.Id, custom.Id, OrganizationCapabilityCatalog.MembersRead));
        await context.SaveChangesAsync();

        // Recreate the pre-upgrade state of an organization created under catalog policy version 1.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM shared."OrganizationRolePermissions"
            WHERE "OrganizationId" = {organization.Id}
              AND "PermissionCode" IN ({OrganizationCapabilityCatalog.EngagementsCreate}, {OrganizationCapabilityCatalog.EngagementsReadAll}, {OrganizationCapabilityCatalog.ProjectsCreate})
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE shared."OrganizationRolePermissions" SET "CatalogPolicyVersion" = 1 WHERE "OrganizationId" = {organization.Id}
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE shared."OrganizationRoles" SET "TemplateVersion" = 1 WHERE "OrganizationId" = {organization.Id} AND "Origin" = 'system'
            """);
        context.ChangeTracker.Clear();

        await migrator.MigrateAsync(upgrade);
        context.ChangeTracker.Clear();
        List<OrganizationRole> roles = await context.OrganizationRoles.AsNoTracking().Where(role => role.OrganizationId == organization.Id).ToListAsync();
        List<OrganizationRolePermission> permissions = await context.OrganizationRolePermissions.AsNoTracking().Where(row => row.OrganizationId == organization.Id).ToListAsync();
        string[] newCodes = [OrganizationCapabilityCatalog.EngagementsCreate, OrganizationCapabilityCatalog.EngagementsReadAll, OrganizationCapabilityCatalog.ProjectsCreate];

        foreach (Guid roleId in new[] { seed.OwnerRole.Id, seed.AdminRole.Id })
        {
            OrganizationRole upgraded = roles.Single(role => role.Id == roleId);
            Assert.Equal(2, upgraded.Version);
            Assert.Equal(2, upgraded.PermissionSetVersion);
            Assert.Equal(2, upgraded.TemplateVersion);
            Assert.Equal(newCodes, permissions.Where(row => row.RoleId == roleId && row.CatalogPolicyVersion == 2).Select(row => row.PermissionCode).Order(StringComparer.Ordinal));
            Assert.True(newCodes.All(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(upgraded, permissions).Contains));
        }

        OrganizationRole member = roles.Single(role => role.Id == seed.MemberRole.Id);
        Assert.Equal(1, member.PermissionSetVersion);
        Assert.Equal(1, member.TemplateVersion);
        OrganizationRole untouchedCustom = roles.Single(role => role.Id == custom.Id);
        Assert.Equal(1, untouchedCustom.PermissionSetVersion);
        Assert.DoesNotContain(permissions, row => row.RoleId == custom.Id && newCodes.Contains(row.PermissionCode));

        List<IdentityAuditEvent> audit = await context.IdentityAuditEvents.AsNoTracking().Where(item => item.OrganizationId == organization.Id).ToListAsync();
        Assert.Equal(2, audit.Count);
        Assert.All(audit, item =>
        {
            Assert.Equal("system", item.ActorKind);
            Assert.Null(item.ActorUserId);
            Assert.Equal("catalog-policy-v2-upgrade", item.ActorReference);
            Assert.Equal(2, item.AggregateVersion);
            _ = DeliveryPilot.Identity.Domain.Auditing.IdentityAuditSchemaCatalog.ValidateAndCanonicalizeDetails(item.AggregateType, item.EventType, item.DetailsSchemaVersion, item.Details);
        });

        await migrator.MigrateAsync(previous);
        context.ChangeTracker.Clear();
        OrganizationRole rolledBackOwner = await context.OrganizationRoles.AsNoTracking().SingleAsync(role => role.Id == seed.OwnerRole.Id);
        Assert.Equal(1, rolledBackOwner.PermissionSetVersion);
        Assert.Equal(1, rolledBackOwner.TemplateVersion);
        Assert.Equal(3, rolledBackOwner.Version);
        Assert.False(await context.OrganizationRolePermissions.AnyAsync(row => row.OrganizationId == organization.Id && newCodes.Contains(row.PermissionCode)));
        Assert.False(await context.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == organization.Id));

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(2, (await context.OrganizationRoles.AsNoTracking().SingleAsync(role => role.Id == seed.AdminRole.Id)).PermissionSetVersion);
        Assert.Equal(2, await context.IdentityAuditEvents.CountAsync(item => item.OrganizationId == organization.Id));
    }

    [PostgreSqlFact]
    public async Task UpgradePreservesUsersAndRemovesOtherDomainTables()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.Context();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261025182021_AddIdentityEmailOutbox");
        var user = User.Create("preserved@example.test", "Preserved user");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO shared."Organizations" ("Slug", "DisplayName", "CreatedByUserId")
            VALUES ('removed-organization', 'Removed organization', {user.Id})
            """);

        await migrator.MigrateAsync("20261101162318_RetainUsersOnly");
        context.ChangeTracker.Clear();
        Assert.Equal("Preserved user", (await context.Users.SingleAsync()).DisplayName);
        Assert.Equal(user.Id, (await context.Users.SingleAsync()).Id);
        await AssertTablesAsync(context, "shared.Users");

        // Recreating the old schema cannot recover dropped data, but must restore
        // its SQL-only constraints if an operator rolls back the migration.
        await migrator.MigrateAsync("20261025182021_AddIdentityEmailOutbox");
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_indexes
            WHERE schemaname = 'shared' AND indexname = 'UxOrganizationsSlugCi'
            """).SingleAsync());
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_indexes
            WHERE schemaname = 'dbo' AND indexname = 'UxInvitationsPendingEmail'
            """).SingleAsync());
        await migrator.MigrateAsync("20261101173630_AddRegistrationCredentials");
        Assert.Equal(user.Id, (await context.Users.SingleAsync()).Id);
        await AssertTablesAsync(context, "shared.PasswordCredentials", "shared.Users");

        context.PasswordCredentials.Add(PasswordCredential.Create(user.Id, "test-version-1$opaque-verifier", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        await context.Database.MigrateAsync();
        Assert.Equal("test-version-1$opaque-verifier", (await context.PasswordCredentials.SingleAsync()).PasswordHash);
        await AssertTablesAsync(context, "shared.AuthSessions", "shared.IdentityAuditEvents", "shared.OrganizationCreationAttempts", "shared.OrganizationInvitationDeliveries", "shared.OrganizationInvitationRoleOffers", "shared.OrganizationInvitations", "shared.OrganizationMemberships", "shared.OrganizationOwnershipNominations", "shared.OrganizationRoleAssignments", "shared.OrganizationRolePermissions", "shared.OrganizationRoles", "shared.Organizations", "shared.PasswordCredentials", "shared.Users");

        var now = DateTimeOffset.UtcNow;
        context.AuthSessions.Add(AuthSession.Create(Guid.NewGuid(), user.Id, new byte[AuthSession.RefreshVerifierLength], now, now.AddDays(7)));
        await context.SaveChangesAsync();

        // Rolling back organization and session migrations preserves Users and PasswordCredentials.
        await migrator.MigrateAsync("20261101173630_AddRegistrationCredentials");
        context.ChangeTracker.Clear();
        await AssertTablesAsync(context, "shared.PasswordCredentials", "shared.Users");
        Assert.Equal(user.Id, (await context.Users.SingleAsync()).Id);
        Assert.Equal("test-version-1$opaque-verifier", (await context.PasswordCredentials.SingleAsync()).PasswordHash);

        // Removing the newly added credential schema loses its rows, not the User.
        await migrator.MigrateAsync("20261101162318_RetainUsersOnly");
        await AssertTablesAsync(context, "shared.Users");
        Assert.Equal(user.Id, (await context.Users.SingleAsync()).Id);
        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
        Assert.Empty(await context.PasswordCredentials.ToListAsync());
        Assert.Empty(await context.AuthSessions.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task FreshDatabaseMigratesAndRepeatedRunIsIdempotent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.Context();
        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        await AssertTablesAsync(context, "shared.AuthSessions", "shared.IdentityAuditEvents", "shared.OrganizationCreationAttempts", "shared.OrganizationInvitationDeliveries", "shared.OrganizationInvitationRoleOffers", "shared.OrganizationInvitations", "shared.OrganizationMemberships", "shared.OrganizationOwnershipNominations", "shared.OrganizationRoleAssignments", "shared.OrganizationRolePermissions", "shared.OrganizationRoles", "shared.Organizations", "shared.PasswordCredentials", "shared.Users");
    }

    [PostgreSqlFact]
    public async Task OwnershipNominationMigrationPreservesMembershipRolesAndAuditHistory()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.Context();
        string[] migrations = context.Database.GetMigrations().ToArray();
        string added = migrations.Single(migration => migration.EndsWith("_AddOrganizationOwnershipNominations", StringComparison.Ordinal));
        string previous = migrations[Array.IndexOf(migrations, added) - 1];
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(previous);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create($"nomination-upgrade-{Guid.NewGuid():N}@example.test", "Preserved Owner", status: "active");
        var organization = Organization.Create("Preserved workspace", user.Id, now);
        var membership = OrganizationMembership.Create(organization.Id, user.Id, user.Id, now);
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, now);
        var assignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, seed.OwnerRole.Id, user.Id, now);
        context.Users.Add(user);
        context.Organizations.Add(organization);
        context.OrganizationMemberships.Add(membership);
        context.OrganizationRoles.AddRange(seed.Roles);
        context.OrganizationRolePermissions.AddRange(seed.Permissions);
        context.OrganizationRoleAssignments.Add(assignment);
        await context.SaveChangesAsync();
        Guid auditId;
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var writer = new DeliveryPilot.Identity.Infrastructure.Auditing.IdentityAuditWriter(
                context,
                TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<DeliveryPilot.Identity.Infrastructure.Auditing.IdentityAuditWriter>.Instance);
            IdentityAuditEvent entry = await writer.StageAsync(DeliveryPilot.Identity.Application.Abstractions.Auditing.IdentityAuditEntry.ForOrganizationCreated(
                organization,
                DeliveryPilot.Identity.Application.Abstractions.Auditing.IdentityAuditActor.User(user.Id),
                now,
                "nomination-migration-upgrade"));
            auditId = entry.Id;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await migrator.MigrateAsync(added);
        context.ChangeTracker.Clear();
        Assert.Equal(membership.Id, (await context.OrganizationMemberships.SingleAsync()).Id);
        Assert.Equal(assignment.Id, (await context.OrganizationRoleAssignments.SingleAsync()).Id);
        Assert.Equal(user.Id, (await context.Users.SingleAsync()).Id);
        Assert.Equal(auditId, (await context.IdentityAuditEvents.SingleAsync()).Id);
        Assert.Empty(await context.OrganizationOwnershipNominations.ToListAsync());
        var nomination = OrganizationOwnershipNomination.Create(organization.Id, membership.Id, user.Id, now);
        context.OrganizationOwnershipNominations.Add(nomination);
        await context.SaveChangesAsync();
        await migrator.MigrateAsync(previous);
        Assert.Equal(assignment.Id, (await context.OrganizationRoleAssignments.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(auditId, (await context.IdentityAuditEvents.AsNoTracking().SingleAsync()).Id);
        await migrator.MigrateAsync(added);
        Assert.Empty(await context.OrganizationOwnershipNominations.AsNoTracking().ToListAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static async Task AssertTablesAsync(IdentityDbContext context, params string[] expected)
    {
        var tables = await context.Database.SqlQueryRaw<string>("""
            SELECT table_schema || '.' || table_name AS "Value"
            FROM information_schema.tables
            WHERE table_schema IN ('shared', 'dbo') AND table_type = 'BASE TABLE'
            """).ToListAsync();
        Assert.Equal(expected.OrderBy(name => name), tables.OrderBy(name => name));
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_indexes
            WHERE schemaname = 'shared' AND indexname = 'UxUsersPrimaryEmailCi'
            """).SingleAsync());
    }

    private sealed class TestDatabase(string adminConnection, string name) : IAsyncDisposable
    {
        public static async Task<TestDatabase> CreateAsync()
        {
            string adminConnection = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")!;
            string name = "deliverypilot_user_migration_" + Guid.NewGuid().ToString("N");
            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await command.ExecuteNonQueryAsync();
            return new TestDatabase(adminConnection, name);
        }

        public IdentityDbContext Context()
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name, Pooling = false };
            return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection.ConnectionString).Options);
        }

        public async ValueTask DisposeAsync()
        {
            if (!name.StartsWith("deliverypilot_user_migration_", StringComparison.Ordinal)
                || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            {
                throw new InvalidOperationException("Unsafe test database cleanup target.");
            }

            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}
