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
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(1, row.CatalogPolicyVersion));
        Assert.True(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.OwnerRole, rows)
            .SetEquals([OrganizationCapabilityCatalog.AuditRead]));

        await migrator.MigrateAsync("20261104025115_AddOrganizationCreationAttempts");
        int preserved = await context.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM shared."OrganizationRolePermissions"
            """).SingleAsync();
        Assert.Equal(2, preserved);
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
        await AssertTablesAsync(context, "shared.AuthSessions", "shared.IdentityAuditEvents", "shared.OrganizationCreationAttempts", "shared.OrganizationInvitationDeliveries", "shared.OrganizationInvitationRoleOffers", "shared.OrganizationInvitations", "shared.OrganizationMemberships", "shared.OrganizationRoleAssignments", "shared.OrganizationRolePermissions", "shared.OrganizationRoles", "shared.Organizations", "shared.PasswordCredentials", "shared.Users");

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
        await AssertTablesAsync(context, "shared.AuthSessions", "shared.IdentityAuditEvents", "shared.OrganizationCreationAttempts", "shared.OrganizationInvitationDeliveries", "shared.OrganizationInvitationRoleOffers", "shared.OrganizationInvitations", "shared.OrganizationMemberships", "shared.OrganizationRoleAssignments", "shared.OrganizationRolePermissions", "shared.OrganizationRoles", "shared.Organizations", "shared.PasswordCredentials", "shared.Users");
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
