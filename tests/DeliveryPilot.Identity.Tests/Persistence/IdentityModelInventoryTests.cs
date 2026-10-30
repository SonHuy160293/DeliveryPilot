using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class IdentityModelInventoryTests
{
    private static readonly string[] ExpectedSharedTables =
    [
        "shared.AuthSessions",
        "shared.IdentityAuditEvents",
        "shared.OrganizationCreationAttempts",
        "shared.OrganizationInvitationDeliveries",
        "shared.OrganizationInvitationRoleOffers",
        "shared.OrganizationInvitations",
        "shared.OrganizationMemberships",
        "shared.OrganizationOwnershipNominations",
        "shared.OrganizationRoleAssignments",
        "shared.OrganizationRolePermissions",
        "shared.OrganizationRoles",
        "shared.Organizations",
        "shared.PasswordCredentials",
        "shared.Users",
    ];

    private static readonly string[] ExpectedDomainModelTypes =
    [
        "AuthSession",
        "IdentityAuditEvent",
        "Organization",
        "OrganizationCreationAttempt",
        "OrganizationInvitation",
        "OrganizationInvitationDelivery",
        "OrganizationInvitationRoleOffer",
        "OrganizationMembership",
        "OrganizationOwnershipNomination",
        "OrganizationRole",
        "OrganizationRoleAssignment",
        "OrganizationRolePermission",
        "PasswordCredential",
        "PlatformRoleAssignment",
        "User",
    ];

    [Fact]
    public void ExistingModel_ContainsRegistrationDataAndOperationalMessagingTables()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options;
        using var context = new IdentityDbContext(options);
        Assert.False(context.Database.HasPendingModelChanges());
        var model = context.GetService<IDesignTimeModel>().Model;
        string[] tables = model.GetEntityTypes()
            .Where(x => x.GetSchema() is "shared" or "dbo")
            .Select(x => $"{x.GetSchema()}.{x.GetTableName()}")
            .OrderBy(x => x).ToArray();

        Assert.Equal(ExpectedSharedTables, tables);
        var domainTypes = typeof(User).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.IsNested && type.Namespace == "DeliveryPilot.Identity.Domain.Models");

        // Platform assignments remain intentionally unmapped.
        Assert.Equal(ExpectedDomainModelTypes, domainTypes.Select(type => type.Name).OrderBy(name => name));
        Assert.Null(model.FindEntityType(typeof(PlatformRoleAssignment)));
        Assert.NotNull(model.FindEntityType(typeof(OrganizationInvitation)));
        Assert.NotNull(model.FindEntityType(typeof(OrganizationInvitationRoleOffer)));
        Assert.NotNull(model.FindEntityType(typeof(OrganizationInvitationDelivery)));

        string[] reliabilityTables = model.GetEntityTypes()
            .Where(x => x.GetSchema() == "reliability")
            .Select(x => $"{x.GetSchema()}.{x.GetTableName()}")
            .OrderBy(x => x).ToArray();
        Assert.Contains("reliability.EmailDeliveryReceipts", reliabilityTables);
        Assert.Contains("reliability.OutboxRecoveries", reliabilityTables);

        var credential = model.FindEntityType(typeof(PasswordCredential))!;
        Assert.Equal("UserId", Assert.Single(credential.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(credential.GetForeignKeys()).DeleteBehavior);

        var session = model.FindEntityType(typeof(AuthSession))!;
        Assert.Equal("Id", Assert.Single(session.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(session.GetForeignKeys()).DeleteBehavior);

        foreach (Type entityClrType in new[]
                 {
                     typeof(Organization),
                     typeof(OrganizationMembership),
                     typeof(OrganizationRole),
                     typeof(OrganizationRolePermission),
                     typeof(OrganizationRoleAssignment),
                     typeof(IdentityAuditEvent),
                 })
        {
            IEntityType entityType = model.FindEntityType(entityClrType)!;
            Assert.All(entityType.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        }

        foreach (Type versionedClrType in new[]
                 {
                     typeof(Organization),
                     typeof(OrganizationMembership),
                     typeof(OrganizationRole),
                     typeof(OrganizationRoleAssignment),
                 })
        {
            IEntityType entityType = model.FindEntityType(versionedClrType)!;
            Assert.True(entityType.FindProperty("Version")!.IsConcurrencyToken);
        }

        string ddl = context.Database.GenerateCreateScript();
        Assert.Contains("CkUsersPrimaryEmail", ddl);
        Assert.Contains("IxUsersStatus", ddl);
        Assert.Contains("CkAuthSessionsRefreshDeadline", ddl);
        Assert.Contains("CkAuthSessionsRefreshTokenVerifier", ddl);
        Assert.Contains("IxAuthSessionsUserId", ddl);
        Assert.Contains("CkOrganizationsName", ddl);
        Assert.Contains("UxOrganizationMembershipsOrganizationUser", ddl);
        Assert.Contains("CkOrganizationRolesOrigin", ddl);
        Assert.Contains("CkOrganizationRolePermissionsCode", ddl);
        Assert.Contains("CkOrganizationRoleAssignmentsRevocation", ddl);
        Assert.Contains("UxIdentityAuditEventsAggregateRevisionOrdinal", ddl);
        Assert.Contains("UxIdentityAuditEventsMembershipRevisionOrdinal", ddl);
        Assert.Contains("CkIdentityAuditEventsDetailsObject", ddl);
    }
}
