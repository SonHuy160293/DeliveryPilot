using System.Security.Cryptography;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using DeliveryPilot.Projects.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace DeliveryPilot.Projects.Tests.Persistence;

[Collection("Projects PostgreSQL")]
public sealed class ProjectPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task ProjectMigrationRollsBackWithoutChangingEngagementOrReliability()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(await context.Projects.AnyAsync());
        Engagement engagement = CreateEngagement();
        var messageId = Guid.NewGuid();
        context.AddRange(engagement, OutboxMessage.Create(messageId, "projects.persistence-test.v1", 1, Guid.NewGuid(), 1, "{}", Guid.NewGuid(), null, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        IMigrator migrator = context.GetService<IMigrator>();
        string engagementMigration = context.Database.GetMigrations().Single(id => id.EndsWith("_AddEngagementPersistence", StringComparison.Ordinal));
        string latest = context.Database.GetMigrations().Last();
        string downSql = migrator.GenerateScript(latest, engagementMigration);
        Assert.DoesNotContain("DROP TABLE engagement.", downSql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE reliability.", downSql, StringComparison.Ordinal);

        try
        {
            await migrator.MigrateAsync(engagementMigration);
            Assert.True(await context.Engagements.AnyAsync(candidate => candidate.Id == engagement.Id));
            Assert.True(await context.Set<OutboxMessage>().AnyAsync(message => message.MessageId == messageId));
            await using var connection = new NpgsqlConnection(PostgreSqlFactAttribute.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'project'", connection);
            Assert.Equal(0L, await command.ExecuteScalarAsync());
        }
        finally
        {
            await migrator.MigrateAsync();
            await context.Engagements.Where(candidate => candidate.Id == engagement.Id).ExecuteDeleteAsync();
            await context.Set<OutboxMessage>().Where(message => message.MessageId == messageId).ExecuteDeleteAsync();
        }

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgreSqlTheory]
    [InlineData("brief", false)]
    [InlineData("participant", false)]
    [InlineData("role", false)]
    [InlineData("invitation", false)]
    [InlineData("delivery", false)]
    [InlineData("stakeholder", false)]
    [InlineData("event", false)]
    [InlineData("brief", true)]
    [InlineData("participant", true)]
    [InlineData("role", true)]
    [InlineData("invitation", true)]
    [InlineData("delivery", true)]
    [InlineData("stakeholder", true)]
    [InlineData("event", true)]
    public async Task EveryChildRejectsAnotherTenantOrMissingProject(string kind, bool missingProject)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        ProjectGuestInvitation invitation = Issue(project);
        ProjectParticipant participant = Member(project);
        context.AddRange(project, invitation, participant);
        await context.SaveChangesAsync();
        object child = CreateChild(kind, project, participant, invitation);
        context.Entry(child).Property("OrganizationId").CurrentValue = missingProject ? project.OrganizationId : Guid.NewGuid();
        context.Entry(child).Property("ProjectId").CurrentValue = missingProject ? Guid.CreateVersion7() : project.Id;
        context.Add(child);

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    [PostgreSqlTheory]
    [InlineData("participant", false)]
    [InlineData("role", false)]
    [InlineData("invitation", false)]
    [InlineData("delivery", false)]
    [InlineData("stakeholder", false)]
    [InlineData("participant", true)]
    [InlineData("role", true)]
    [InlineData("invitation", true)]
    [InlineData("delivery", true)]
    [InlineData("stakeholder", true)]
    public async Task NestedLinksRejectAnotherProjectOrTenant(string kind, bool otherTenant)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        Project foreignProject = CreateProject(otherTenant ? Guid.NewGuid() : project.OrganizationId);
        ProjectGuestInvitation foreignInvitation = Issue(foreignProject);
        ProjectParticipant foreignParticipant = Member(foreignProject);
        context.AddRange(project, foreignProject, foreignInvitation, foreignParticipant);
        await context.SaveChangesAsync();
        object child = kind switch
        {
            "participant" => ProjectParticipant.CreateGuest(project.OrganizationId, project.Id, Guid.NewGuid(), DateTimeOffset.UtcNow, foreignInvitation.Id),
            "role" => ProjectParticipantRole.Grant(foreignParticipant, "Viewer", project.CreatedByUserId, DateTimeOffset.UtcNow),
            "invitation" => Issue(project, replacesInvitationId: foreignInvitation.Id),
            "delivery" => ProjectGuestInvitationDelivery.Create(project.OrganizationId, project.Id, foreignInvitation.Id, Guid.CreateVersion7(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7)),
            "stakeholder" => ProjectStakeholder.Create(project.OrganizationId, project.Id, project.CreatedByUserId, DateTimeOffset.UtcNow, "Name", "Client", participantId: foreignParticipant.Id),
            _ => throw new ArgumentException("Unknown nested link.", nameof(kind)),
        };
        if (kind == "role")
        {
            context.Entry(child).Property("OrganizationId").CurrentValue = project.OrganizationId;
            context.Entry(child).Property("ProjectId").CurrentValue = project.Id;
        }

        context.Add(child);
        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientPoCannotPointOutsideItsProject(bool otherTenant)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        Project foreignProject = CreateProject(otherTenant ? Guid.NewGuid() : project.OrganizationId);
        ProjectGuestInvitation invitation = Issue(foreignProject);
        var guest = ProjectParticipant.CreateGuest(foreignProject.OrganizationId, foreignProject.Id, Guid.NewGuid(), DateTimeOffset.UtcNow, invitation.Id);
        context.AddRange(project, foreignProject, invitation, guest);
        await context.SaveChangesAsync();
        AssignClientPo(context, project, guest.Id);
        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("FkProjectsClientProductOwner", Assert.IsType<PostgresException>(failure.InnerException).ConstraintName);
    }

    [PostgreSqlFact]
    public async Task ProjectCannotReferenceAnotherTenantsEngagement()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement engagement = CreateEngagement();
        context.Add(engagement);
        await context.SaveChangesAsync();
        var project = Project.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "Name", "Purpose", "Client", engagement.Id);
        context.Add(project);
        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("FkProjectsEngagements", Assert.IsType<PostgresException>(failure.InnerException).ConstraintName);
    }

    [PostgreSqlFact]
    public async Task CircularClientPoInsertionAndAllEightEntitiesRoundTrip()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Engagement engagement = CreateEngagement();
        var project = Project.Create(engagement.OrganizationId, engagement.CreatedByUserId, DateTimeOffset.UtcNow, "Name", "Purpose", "Client", engagement.Id);
        context.AddRange(engagement, project, ProjectBrief.CreateFor(project));
        await context.SaveChangesAsync();
        ProjectGuestInvitation invitation = Issue(project);
        context.Add(invitation);
        await context.SaveChangesAsync();
        var participant = ProjectParticipant.CreateGuest(project.OrganizationId, project.Id, Guid.NewGuid(), DateTimeOffset.UtcNow, invitation.Id);
        context.Add(participant);
        await context.SaveChangesAsync();
        AssignClientPo(context, project, participant.Id);
        var deliveryId = Guid.CreateVersion7();
        context.Entry(invitation).Property(x => x.CurrentDeliveryMessageId).CurrentValue = deliveryId;
        context.AddRange(
            ProjectParticipantRole.Grant(participant, "ProjectManager", project.CreatedByUserId, DateTimeOffset.UtcNow),
            ProjectGuestInvitationDelivery.Create(project.OrganizationId, project.Id, invitation.Id, deliveryId, invitation.LastIssuedAt, invitation.ExpiresAt),
            ProjectStakeholder.Create(project.OrganizationId, project.Id, project.CreatedByUserId, DateTimeOffset.UtcNow, "Client PO", "Client", participantId: participant.Id),
            ProjectEvent.Create(project.OrganizationId, project.Id, "project.created", "Project", project.Id, project.CreatedByUserId, project.OrganizationId, DateTimeOffset.UtcNow, 1, "{\"created\":true}"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Project loaded = await context.Projects.SingleAsync(row => row.Id == project.Id);
        Assert.Equal(participant.Id, loaded.ClientProductOwnerParticipantId);
        Assert.Equal(TimeSpan.Zero, loaded.ClientPoAssignedAt!.Value.Offset);
        Assert.Equal(1, (await context.ProjectBriefs.SingleAsync(row => row.ProjectId == project.Id)).Version);
        Assert.Equal(invitation.Id, (await context.ProjectParticipants.SingleAsync(row => row.Id == participant.Id)).AdmittedViaInvitationId);
        Assert.Equal("ProjectManager", (await context.ProjectParticipantRoles.SingleAsync(row => row.ProjectId == project.Id)).RoleCode);
        ProjectGuestInvitation reloadedInvitation = await context.ProjectGuestInvitations.SingleAsync(row => row.Id == invitation.Id);
        Assert.Equal(invitation.TokenVerifier, reloadedInvitation.TokenVerifier);
        byte[] exposed = reloadedInvitation.TokenVerifier;
        exposed[0] ^= 255;
        Assert.Equal(invitation.TokenVerifier, reloadedInvitation.TokenVerifier);
        Assert.Equal(deliveryId, reloadedInvitation.CurrentDeliveryMessageId);
        Assert.Equal("queued", (await context.ProjectGuestInvitationDeliveries.SingleAsync(row => row.DeliveryMessageId == deliveryId)).Status);
        Assert.Equal(participant.Id, (await context.ProjectStakeholders.SingleAsync(row => row.ProjectId == project.Id)).ParticipantId);
        Assert.Contains("created", (await context.ProjectEvents.SingleAsync(row => row.ProjectId == project.Id)).Details, StringComparison.Ordinal);
    }

    [PostgreSqlTheory]
    [InlineData("project", "\"SprintLengthWeeks\" = 0", "CkProjectsSprintLength")]
    [InlineData("project", "\"SprintLengthWeeks\" = 5", "CkProjectsSprintLength")]
    [InlineData("project", "\"Kind\" = 'Unknown'", "CkProjectsKind")]
    [InlineData("project", "\"Kind\" = 'Internal', \"EngagementId\" = gen_random_uuid()", "CkProjectsEngagement")]
    [InlineData("project", "\"Status\" = 'Active'", "CkProjectsActivation")]
    [InlineData("project", "\"ActivatedAt\" = now()", "CkProjectsActivation")]
    [InlineData("project", "\"ClientPoAssignedAt\" = now()", "CkProjectsClientPo")]
    [InlineData("project", "\"Name\" = ' '", "CkProjectsName")]
    [InlineData("project", "\"Purpose\" = ' padded '", "CkProjectsPurpose")]
    [InlineData("project", "\"Version\" = 0", "CkProjectsVersion")]
    [InlineData("project", "\"CreatedByUserId\" = '00000000-0000-0000-0000-000000000000'", "CkProjectsIds")]
    [InlineData("brief", "\"UpdatedAt\" = now()", "CkProjectBriefsUpdate")]
    [InlineData("brief", "\"Version\" = 0", "CkProjectBriefsVersion")]
    [InlineData("participant", "\"Kind\" = 'Unknown'", "CkProjectParticipantsKind")]
    [InlineData("participant", "\"Status\" = 'Removed'", "CkProjectParticipantsEnd")]
    [InlineData("participant", "\"EndedAt\" = now()", "CkProjectParticipantsEnd")]
    [InlineData("participant", "\"Version\" = 0", "CkProjectParticipantsVersion")]
    [InlineData("role", "\"RoleCode\" = 'Unknown'", "CkProjectParticipantRolesCode")]
    [InlineData("role", "\"RevokedAt\" = now()", "CkProjectParticipantRolesRevocation")]
    [InlineData("invitation", "\"RecipientEmail\" = 'UPPER@example.com'", "CkProjectGuestInvitationsEmail")]
    [InlineData("invitation", "\"OfferedRoleCode\" = 'ProjectAdministrator'", "CkProjectGuestInvitationsRole")]
    [InlineData("invitation", "\"TokenVerifier\" = decode('00', 'hex')", "CkProjectGuestInvitationsVerifier")]
    [InlineData("invitation", "\"ExpiresAt\" = \"LastIssuedAt\" + interval '6 days'", "CkProjectGuestInvitationsExpiry")]
    [InlineData("invitation", "\"Status\" = 'Accepted'", "CkProjectGuestInvitationsClosure")]
    [InlineData("invitation", "\"ClosedAt\" = now()", "CkProjectGuestInvitationsClosure")]
    [InlineData("invitation", "\"Version\" = 0", "CkProjectGuestInvitationsVersion")]
    [InlineData("delivery", "\"Status\" = 'Unknown'", "CkProjectGuestInvitationDeliveriesStatus")]
    [InlineData("delivery", "\"LastEventSequence\" = -1", "CkProjectGuestInvitationDeliveriesSequence")]
    [InlineData("delivery", "\"ExpiresAt\" = \"IssuedAt\"", "CkProjectGuestInvitationDeliveriesExpiry")]
    [InlineData("delivery", "\"Version\" = 0", "CkProjectGuestInvitationDeliveriesVersion")]
    [InlineData("stakeholder", "\"Side\" = 'Unknown'", "CkProjectStakeholdersSide")]
    [InlineData("stakeholder", "\"Name\" = ''", "CkProjectStakeholdersName")]
    [InlineData("stakeholder", "\"Email\" = 'UPPER@example.com'", "CkProjectStakeholdersEmail")]
    [InlineData("stakeholder", "\"RetiredAt\" = now()", "CkProjectStakeholdersRetirement")]
    [InlineData("stakeholder", "\"Version\" = 0", "CkProjectStakeholdersVersion")]
    [InlineData("event", "\"Details\" = '[]'::jsonb", "CkProjectEventsDetails")]
    [InlineData("event", "\"SubjectType\" = 'Unknown'", "CkProjectEventsSubject")]
    [InlineData("event", "\"ProjectVersion\" = 0", "CkProjectEventsVersion")]
    public async Task DatabaseChecksRejectInvalidRows(string kind, string assignment, string constraint)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        ProjectGuestInvitation invitation = Issue(project);
        ProjectParticipant participant = Member(project);
        context.AddRange(project, invitation, participant);
        await context.SaveChangesAsync();
        object entity = kind == "project" ? project : kind == "participant" ? participant : kind == "invitation" ? invitation : CreateChild(kind, project, participant, invitation);
        if (entity != project && entity != participant && entity != invitation)
        {
            context.Add(entity);
            await context.SaveChangesAsync();
        }

        string table = context.Entry(entity).Metadata.GetTableName()!;
        string key = Assert.Single(context.Entry(entity).Metadata.FindPrimaryKey()!.Properties).Name;
        var id = (Guid)context.Entry(entity).Property(key).CurrentValue!;

        // Identifiers and assignments are fixed test cases. Values use database parameters.
        string sql = $"UPDATE project.\"{table}\" SET {assignment} WHERE \"{key}\" = @id";
        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql, new NpgsqlParameter("id", id)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    [PostgreSqlTheory]
    [InlineData("Problem")]
    [InlineData("ObjectivesAndSuccessCriteria")]
    [InlineData("ScopeAndExclusions")]
    [InlineData("Assumptions")]
    [InlineData("ConstraintsAndDependencies")]
    [InlineData("OpenQuestions")]
    [InlineData("ProductGoal")]
    public async Task EveryBriefItemEnforcesThe4000CharacterLimit(string field)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        context.AddRange(project, ProjectBrief.CreateFor(project));
        await context.SaveChangesAsync();
        string sql = $"UPDATE project.\"ProjectBriefs\" SET \"{field}\" = @value WHERE \"ProjectId\" = @id";
        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql, new NpgsqlParameter("value", new string('x', 4001)), new NpgsqlParameter("id", project.Id)));
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, failure.SqlState);
    }

    [PostgreSqlFact]
    public async Task ActiveRolesAreUniqueButRevokedGrantsRemainAsHistory()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        ProjectParticipant participant = Member(project);
        var first = ProjectParticipantRole.Grant(participant, "Developer", project.CreatedByUserId, DateTimeOffset.UtcNow);
        context.AddRange(project, participant, first);
        await context.SaveChangesAsync();
        var second = ProjectParticipantRole.Grant(participant, "Developer", project.CreatedByUserId, DateTimeOffset.UtcNow);
        context.Add(second);
        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("UxProjectParticipantRolesActive", Assert.IsType<PostgresException>(failure.InnerException).ConstraintName);
        context.Entry(second).State = EntityState.Detached;
        context.Entry(first).Property(x => x.RevokedAt).CurrentValue = DateTimeOffset.UtcNow;
        context.Entry(first).Property(x => x.RevokedByUserId).CurrentValue = project.CreatedByUserId;
        await context.SaveChangesAsync();
        context.Add(second);
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.ProjectParticipantRoles.CountAsync(row => row.ParticipantId == participant.Id));
    }

    [PostgreSqlTheory]
    [InlineData("Accepted")]
    [InlineData("Declined")]
    [InlineData("Cancelled")]
    [InlineData("Expired")]
    public async Task OnlyPendingInvitationsAreUniqueAndExpiryHasNoActor(string closedStatus)
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        ProjectGuestInvitation first = Issue(project);
        context.AddRange(project, first);
        await context.SaveChangesAsync();
        ProjectGuestInvitation second = Issue(project);
        context.Add(second);
        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("UxProjectGuestInvitationsPending", Assert.IsType<PostgresException>(failure.InnerException).ConstraintName);
        context.Entry(second).State = EntityState.Detached;
        context.Entry(first).Property(x => x.Status).CurrentValue = closedStatus;
        context.Entry(first).Property(x => x.ClosedAt).CurrentValue = DateTimeOffset.UtcNow;
        context.Entry(first).Property(x => x.ClosedByUserId).CurrentValue = closedStatus == "Expired" ? null : project.CreatedByUserId;
        await context.SaveChangesAsync();
        context.Add(second);
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.ProjectGuestInvitations.CountAsync(row => row.ProjectId == project.Id));
    }

    [PostgreSqlFact]
    public async Task APersonHasOnlyOneDurableParticipantRowPerProject()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Project project = CreateProject();
        ProjectParticipant first = Member(project);
        context.AddRange(project, first);
        await context.SaveChangesAsync();
        context.Add(ProjectParticipant.CreateMember(project.OrganizationId, project.Id, first.UserId, DateTimeOffset.UtcNow));
        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("UxProjectParticipantsUser", Assert.IsType<PostgresException>(failure.InnerException).ConstraintName);
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectAndBriefDetectStaleWritersWithoutConflictingWithEachOther(bool briefFirst)
    {
        await using ProjectsDbContext setup = CreateContext();
        await setup.Database.MigrateAsync();
        Project project = CreateProject();
        setup.AddRange(project, ProjectBrief.CreateFor(project));
        await setup.SaveChangesAsync();
        try
        {
            await using ProjectsDbContext first = CreateContext();
            await using ProjectsDbContext second = CreateContext();
            Project firstProject = await first.Projects.SingleAsync(row => row.Id == project.Id);
            ProjectBrief firstBrief = await first.ProjectBriefs.SingleAsync(row => row.ProjectId == project.Id);
            Project secondProject = await second.Projects.SingleAsync(row => row.Id == project.Id);
            ProjectBrief secondBrief = await second.ProjectBriefs.SingleAsync(row => row.ProjectId == project.Id);
            object firstTarget = briefFirst ? firstBrief : firstProject;
            object staleTarget = briefFirst ? secondBrief : secondProject;
            object independent = briefFirst ? secondProject : secondBrief;
            first.Entry(firstTarget).Property("Version").CurrentValue = 2L;
            await first.SaveChangesAsync();
            second.Entry(independent).Property("Version").CurrentValue = 2L;
            await second.SaveChangesAsync();
            second.Entry(staleTarget).Property("Version").CurrentValue = 3L;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }
        finally
        {
            await setup.ProjectBriefs.Where(row => row.ProjectId == project.Id).ExecuteDeleteAsync();
            await setup.Projects.Where(row => row.Id == project.Id).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task InvitationExpiryIsElapsedTimeEvenAcrossDaylightSavingChanges()
    {
        await using ProjectsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync("SET LOCAL TIME ZONE 'America/New_York'");
        Project project = CreateProject();
        var issuedAt = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var invitation = ProjectGuestInvitation.Issue(project.OrganizationId, project.Id, project.CreatedByUserId, issuedAt, "guest@example.com", "Viewer", RandomNumberGenerator.GetBytes(32));
        context.AddRange(project, invitation);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(issuedAt.AddHours(168), (await context.ProjectGuestInvitations.SingleAsync(row => row.Id == invitation.Id)).ExpiresAt);
    }

    private static ProjectsDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);

    private static Project CreateProject(Guid? organizationId = null)
        => Project.Create(organizationId ?? Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "Project", "Purpose", "Internal");

    private static Engagement CreateEngagement()
        => Engagement.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "Client", "FP-1", new DateOnly(2026, 11, 1));

    private static ProjectParticipant Member(Project project)
        => ProjectParticipant.CreateMember(project.OrganizationId, project.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);

    private static ProjectGuestInvitation Issue(Project project, Guid? replacesInvitationId = null)
        => ProjectGuestInvitation.Issue(project.OrganizationId, project.Id, project.CreatedByUserId, DateTimeOffset.UtcNow, "guest@example.com", "Viewer", RandomNumberGenerator.GetBytes(32), replacesInvitationId);

    private static object CreateChild(string kind, Project project, ProjectParticipant participant, ProjectGuestInvitation invitation)
        => kind switch
        {
            "brief" => ProjectBrief.CreateFor(project),
            "participant" => Member(project),
            "role" => ProjectParticipantRole.Grant(participant, "Developer", project.CreatedByUserId, DateTimeOffset.UtcNow),
            "invitation" => ProjectGuestInvitation.Issue(project.OrganizationId, project.Id, project.CreatedByUserId, DateTimeOffset.UtcNow, "other@example.com", "Viewer", RandomNumberGenerator.GetBytes(32)),
            "delivery" => ProjectGuestInvitationDelivery.Create(project.OrganizationId, project.Id, invitation.Id, Guid.CreateVersion7(), invitation.LastIssuedAt, invitation.ExpiresAt),
            "stakeholder" => ProjectStakeholder.Create(project.OrganizationId, project.Id, project.CreatedByUserId, DateTimeOffset.UtcNow, "Name", "Vendor"),
            "event" => ProjectEvent.Create(project.OrganizationId, project.Id, "project.created", "Project", project.Id, project.CreatedByUserId, project.OrganizationId, DateTimeOffset.UtcNow, 1, "{}"),
            _ => throw new ArgumentException("Unknown child kind.", nameof(kind)),
        };

    private static void AssignClientPo(ProjectsDbContext context, Project project, Guid participantId)
    {
        context.Entry(project).Property(x => x.ClientProductOwnerParticipantId).CurrentValue = participantId;
        context.Entry(project).Property(x => x.ClientPoAssignedAt).CurrentValue = DateTimeOffset.UtcNow;
        context.Entry(project).Property(x => x.ClientPoAssignedByUserId).CurrentValue = project.CreatedByUserId;
    }
}
