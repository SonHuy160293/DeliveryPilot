using System.Security.Cryptography;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using DeliveryPilot.Projects.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace DeliveryPilot.Projects.Tests.Persistence;

[Collection("Projects PostgreSQL")]
public sealed class EngagementPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task MigrationRollbackPreservesReliabilityAndCanBeReapplied()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        // Never roll back a populated engagement schema, even if a test connection was misconfigured.
        Assert.False(await context.Engagements.AnyAsync());
        Assert.False(await context.EngagementLinkAttempts.AnyAsync());
        var messageId = Guid.NewGuid();
        context.Set<OutboxMessage>().Add(OutboxMessage.Create(messageId, "projects.persistence-test.v1", 1, Guid.NewGuid(), 1, "{}", Guid.NewGuid(), null, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        IMigrator migrator = context.GetService<IMigrator>();
        string initialMigration = context.Database.GetMigrations().Single(id => id.EndsWith("_InitialReliability", StringComparison.Ordinal));
        string lastMigration = context.Database.GetMigrations().Last();
        string downSql = migrator.GenerateScript(lastMigration, initialMigration);
        Assert.DoesNotContain("DROP TABLE reliability.", downSql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP SCHEMA reliability", downSql, StringComparison.Ordinal);

        try
        {
            await migrator.MigrateAsync(initialMigration);
            Assert.True(await context.Set<OutboxMessage>().AnyAsync(message => message.MessageId == messageId));
            await using var connection = new NpgsqlConnection(PostgreSqlFactAttribute.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'engagement'", connection);
            Assert.Equal(0L, await command.ExecuteScalarAsync());
        }
        finally
        {
            await migrator.MigrateAsync();
            await context.Set<OutboxMessage>().Where(message => message.MessageId == messageId).ExecuteDeleteAsync();
        }

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgreSqlTheory]
    [InlineData("milestone")]
    [InlineData("contact")]
    [InlineData("code")]
    [InlineData("event")]
    public async Task CompositeForeignKeysRejectCrossTenantChildren(string kind)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement engagement = CreateEngagement();
        context.Add(engagement);
        await context.SaveChangesAsync();
        object child = CreateChild(kind, engagement, Guid.NewGuid());
        context.Add(child);

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    [PostgreSqlTheory]
    [InlineData("\"ContractAmount\" = 1", "CkEngagementsAmountCurrency")]
    [InlineData("\"ContractAmount\" = -1, \"ContractCurrency\" = 'USD'", "CkEngagementsAmount")]
    [InlineData("\"ContractAmount\" = 1.001, \"ContractCurrency\" = 'USD'", "CkEngagementsAmount")]
    [InlineData("\"ContractAmount\" = 'NaN'::numeric, \"ContractCurrency\" = 'USD'", "CkEngagementsAmount")]
    [InlineData("\"ContractAmount\" = 'Infinity'::numeric, \"ContractCurrency\" = 'USD'", "CkEngagementsAmount")]
    [InlineData("\"PlannedEffortPersonDays\" = -1", "CkEngagementsEffort")]
    [InlineData("\"PlannedEffortPersonDays\" = 1.25", "CkEngagementsEffort")]
    [InlineData("\"ClientDisplayName\" = ' '", "CkEngagementsClientDisplayName")]
    [InlineData("\"AgreementReference\" = ' padded '", "CkEngagementsAgreementReference")]
    [InlineData("\"ClientCountryCode\" = 'vn'", "CkEngagementsCountry")]
    [InlineData("\"ContractCurrency\" = 'usd'", "CkEngagementsCurrency")]
    [InlineData("\"Status\" = 'Deleted'", "CkEngagementsClosure")]
    [InlineData("\"Status\" = 'Closed'", "CkEngagementsClosure")]
    [InlineData("\"ClosedAt\" = now()", "CkEngagementsClosure")]
    [InlineData("\"ClientOrganizationId\" = gen_random_uuid()", "CkEngagementsClientLink")]
    [InlineData("\"ClientOrganizationId\" = \"OrganizationId\", \"ClientLinkedAt\" = now(), \"ClientLinkedByUserId\" = \"CreatedByUserId\"", "CkEngagementsClientOrganization")]
    [InlineData("\"Version\" = 0", "CkEngagementsVersion")]
    public async Task EngagementChecksRejectInvalidRows(string assignment, string constraint)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement engagement = CreateEngagement();
        context.Add(engagement);
        await context.SaveChangesAsync();

        await AssertCheckAsync(context, "Engagements", engagement.Id, assignment, constraint);
    }

    [PostgreSqlTheory]
    [InlineData("milestone", "\"Status\" = 'Unknown'", "CkEngagementMilestonesStatus")]
    [InlineData("milestone", "\"Name\" = ''", "CkEngagementMilestonesName")]
    [InlineData("milestone", "\"Version\" = 0", "CkEngagementMilestonesVersion")]
    [InlineData("contact", "\"ContactRole\" = 'Unknown'", "CkEngagementContactsRole")]
    [InlineData("contact", "\"Email\" = 'ADA@EXAMPLE.COM'", "CkEngagementContactsEmail")]
    [InlineData("contact", "\"RemovedAt\" = now()", "CkEngagementContactsRemoval")]
    [InlineData("contact", "\"Version\" = 0", "CkEngagementContactsVersion")]
    [InlineData("code", "\"ExpiresAt\" = \"IssuedAt\" + interval '6 days'", "CkEngagementLinkCodesExpiry")]
    [InlineData("code", "\"CodeVerifier\" = decode('00', 'hex')", "CkEngagementLinkCodesVerifier")]
    [InlineData("code", "\"Status\" = 'Consumed'", "CkEngagementLinkCodesConsumption")]
    [InlineData("code", "\"ConsumedAt\" = now()", "CkEngagementLinkCodesConsumption")]
    [InlineData("code", "\"Status\" = 'Invalidated'", "CkEngagementLinkCodesInvalidation")]
    [InlineData("code", "\"InvalidatedAt\" = now()", "CkEngagementLinkCodesInvalidation")]
    [InlineData("code", "\"Status\" = 'Unknown'", "CkEngagementLinkCodesStatus")]
    [InlineData("code", "\"Version\" = 0", "CkEngagementLinkCodesVersion")]
    [InlineData("event", "\"Details\" = '[]'::jsonb", "CkEngagementEventsDetails")]
    [InlineData("event", "\"SubjectType\" = 'Unknown'", "CkEngagementEventsSubject")]
    [InlineData("event", "\"EngagementVersion\" = 0", "CkEngagementEventsVersion")]
    public async Task ChildChecksRejectInvalidRows(string kind, string assignment, string constraint)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement engagement = CreateEngagement();
        object child = CreateChild(kind, engagement, engagement.OrganizationId);
        context.Add(engagement);
        context.Add(child);
        await context.SaveChangesAsync();
        var childId = (Guid)context.Entry(child).Property("Id").CurrentValue!;
        string tableName = context.Entry(child).Metadata.GetTableName()!;

        await AssertCheckAsync(context, tableName, childId, assignment, constraint);
    }

    [PostgreSqlFact]
    public async Task PartialUniqueIndexesKeepOneActivePoAndOneOutstandingCode()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement engagement = CreateEngagement();
        context.Add(engagement);
        var firstPo = EngagementContact.Create(engagement.OrganizationId, engagement.Id, engagement.CreatedByUserId, DateTimeOffset.UtcNow, "First", "first@example.com", EngagementContact.ClientProductOwnerRole);
        context.Add(firstPo);
        await context.SaveChangesAsync();
        var secondPo = EngagementContact.Create(engagement.OrganizationId, engagement.Id, engagement.CreatedByUserId, DateTimeOffset.UtcNow, "Second", "second@example.com", EngagementContact.ClientProductOwnerRole);
        context.Add(secondPo);
        DbUpdateException duplicatePo = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("UxEngagementContactsClientProductOwner", Assert.IsType<PostgresException>(duplicatePo.InnerException).ConstraintName);
        context.Entry(secondPo).State = EntityState.Detached;

        context.Entry(firstPo).Property(x => x.RemovedAt).CurrentValue = DateTimeOffset.UtcNow;
        context.Entry(firstPo).Property(x => x.RemovedByUserId).CurrentValue = engagement.CreatedByUserId;
        await context.SaveChangesAsync();
        context.Add(secondPo);
        context.Add(CreateChild("code", engagement, engagement.OrganizationId));
        await context.SaveChangesAsync();
        var secondCode = (EngagementLinkCode)CreateChild("code", engagement, engagement.OrganizationId);
        context.Add(secondCode);
        DbUpdateException duplicateCode = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("UxEngagementLinkCodesOutstanding", Assert.IsType<PostgresException>(duplicateCode.InnerException).ConstraintName);
        context.Entry(secondCode).State = EntityState.Detached;
        EngagementLinkCode firstCode = await context.EngagementLinkCodes.SingleAsync(code => code.EngagementId == engagement.Id);
        context.Entry(firstCode).Property(x => x.Status).CurrentValue = EngagementLinkCode.InvalidatedStatus;
        context.Entry(firstCode).Property(x => x.InvalidatedAt).CurrentValue = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        context.Add(secondCode);
        await context.SaveChangesAsync();
    }

    [PostgreSqlFact]
    public async Task VerifiersAreGloballyUniqueAndRoundTripWithoutExposingMutableState()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement first = CreateEngagement();
        Engagement second = CreateEngagement();
        byte[] verifier = RandomNumberGenerator.GetBytes(32);
        var code = EngagementLinkCode.Create(first.OrganizationId, first.Id, first.CreatedByUserId, DateTimeOffset.UtcNow, verifier);
        context.AddRange(first, second, code);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        EngagementLinkCode loaded = await context.EngagementLinkCodes.SingleAsync(candidate => candidate.Id == code.Id);
        Assert.Equal(verifier, loaded.CodeVerifier);
        context.Add(EngagementLinkCode.Create(second.OrganizationId, second.Id, second.CreatedByUserId, DateTimeOffset.UtcNow, verifier));
        DbUpdateException duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("UxEngagementLinkCodesVerifier", Assert.IsType<PostgresException>(duplicate.InnerException).ConstraintName);
    }

    [PostgreSqlTheory]
    [InlineData("engagement")]
    [InlineData("milestone")]
    [InlineData("contact")]
    [InlineData("code")]
    public async Task VersionTokensRejectASecondWriter(string kind)
    {
        await using ProjectsDbContext setup = CreateContext();
        await setup.Database.MigrateAsync();
        Engagement engagement = CreateEngagement();
        object entity = kind == "engagement" ? engagement : CreateChild(kind, engagement, engagement.OrganizationId);
        setup.Add(engagement);
        if (kind != "engagement")
        {
            setup.Add(entity);
        }

        await setup.SaveChangesAsync();
        var id = (Guid)setup.Entry(entity).Property("Id").CurrentValue!;
        try
        {
            await using ProjectsDbContext first = CreateContext();
            await using ProjectsDbContext second = CreateContext();
            object firstEntity = (await first.FindAsync(entity.GetType(), id))!;
            object secondEntity = (await second.FindAsync(entity.GetType(), id))!;
            first.Entry(firstEntity).Property("Version").CurrentValue = 2L;
            second.Entry(secondEntity).Property("Version").CurrentValue = 2L;
            await first.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }
        finally
        {
            setup.ChangeTracker.Clear();
            if (kind != "engagement")
            {
                string table = setup.Model.FindEntityType(entity.GetType())!.GetTableName()!;
                string deleteSql = $"DELETE FROM engagement.\"{table}\" WHERE \"Id\" = @id";
                await setup.Database.ExecuteSqlRawAsync(deleteSql, new NpgsqlParameter("id", id));
            }

            await setup.Engagements.Where(candidate => candidate.Id == engagement.Id).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task RoundTripPreservesDatesDecimalsAndClientSideAttribution()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement engagement = CreateEngagement();
        var milestone = (EngagementMilestone)CreateChild("milestone", engagement, engagement.OrganizationId);
        var clientOrganizationId = Guid.NewGuid();
        var entry = EngagementEvent.Create(engagement.OrganizationId, engagement.Id, "link.confirmed", "Engagement", engagement.Id, engagement.CreatedByUserId, clientOrganizationId, DateTimeOffset.UtcNow, 1, "{\"clientOrganizationId\":\"" + clientOrganizationId + "\"}");
        context.AddRange(engagement, milestone, entry, EngagementLinkAttempt.Create(engagement.CreatedByUserId, clientOrganizationId, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        context.Entry(engagement).Property(x => x.ContractAmount).CurrentValue = decimal.MaxValue;
        context.Entry(engagement).Property(x => x.ContractCurrency).CurrentValue = "USD";
        context.Entry(engagement).Property(x => x.PlannedEffortPersonDays).CurrentValue = 1.2m;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Engagement loaded = await context.Engagements.SingleAsync(candidate => candidate.Id == engagement.Id);
        Assert.Equal(decimal.MaxValue, loaded.ContractAmount);
        Assert.Equal(1.2m, loaded.PlannedEffortPersonDays);
        Assert.Equal(engagement.AgreementSignedOn, loaded.AgreementSignedOn);
        Assert.Equal(TimeSpan.Zero, loaded.CreatedAt.Offset);
        Assert.Equal(clientOrganizationId, (await context.EngagementEvents.SingleAsync(candidate => candidate.Id == entry.Id)).ActorOrganizationId);
        EngagementMilestone loadedMilestone = await context.EngagementMilestones.SingleAsync(candidate => candidate.Id == milestone.Id);
        context.Entry(loadedMilestone).Property(x => x.OriginalTargetDate).CurrentValue = milestone.OriginalTargetDate.AddDays(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task AgreementIndexIsFilteredCaseInsensitiveAndNonUnique()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement first = CreateEngagement();
        var second = Engagement.Create(first.OrganizationId, first.CreatedByUserId, DateTimeOffset.UtcNow, "Second", "fp-1", new DateOnly(2026, 11, 1));
        context.AddRange(first, second);
        await context.SaveChangesAsync();
        await using var connection = new NpgsqlConnection(PostgreSqlFactAttribute.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT indexdef FROM pg_indexes WHERE schemaname = 'engagement' AND indexname = 'IxEngagementsActiveAgreementReferenceCi'", connection);
        string definition = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Contains("lower(", definition, StringComparison.Ordinal);
        Assert.Contains("Active", definition, StringComparison.Ordinal);
        Assert.Contains("WHERE", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE", definition, StringComparison.Ordinal);
    }

    private static ProjectsDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);

    private static Engagement CreateEngagement()
        => Engagement.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "Client", "FP-1", new DateOnly(2026, 11, 1));

    private static object CreateChild(string kind, Engagement engagement, Guid organizationId)
        => kind switch
        {
            "milestone" => EngagementMilestone.Create(organizationId, engagement.Id, engagement.CreatedByUserId, DateTimeOffset.UtcNow, "Launch", new DateOnly(2026, 12, 1)),
            "contact" => EngagementContact.Create(organizationId, engagement.Id, engagement.CreatedByUserId, DateTimeOffset.UtcNow, "Ada", "ada@example.com", EngagementContact.OtherRole),
            "code" => EngagementLinkCode.Create(organizationId, engagement.Id, engagement.CreatedByUserId, DateTimeOffset.UtcNow, RandomNumberGenerator.GetBytes(32)),
            "event" => EngagementEvent.Create(organizationId, engagement.Id, "engagement.created", "Engagement", engagement.Id, engagement.CreatedByUserId, organizationId, DateTimeOffset.UtcNow, 1, "{}"),
            _ => throw new ArgumentException("Unknown child kind.", nameof(kind)),
        };

    private static async Task AssertCheckAsync(ProjectsDbContext context, string tableName, Guid id, string assignment, string constraint)
    {
        // Identifiers and assignments are fixed test cases; values use database parameters.
        string updateSql = $"UPDATE engagement.\"{tableName}\" SET {assignment} WHERE \"Id\" = @id";
        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(updateSql, new NpgsqlParameter("id", id)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }
}
