using System.Reflection;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

[Collection("Catalog classification isolation")]
public sealed class CatalogPolicyV3Tests
{
    [Fact]
    public void NewOrganizationsSeedEngagementManageForOwnerAndAdminOnly()
    {
        Guid organizationId = Guid.CreateVersion7();
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, DateTimeOffset.UtcNow);

        Assert.Equal(3, OrganizationCapabilityCatalog.CurrentCatalogPolicyVersion);
        Assert.Equal(3, OrganizationCapabilityCatalog.CurrentTemplateVersion);
        Assert.All(seed.Permissions, row => Assert.Equal(3, row.CatalogPolicyVersion));
        Assert.Contains(OrganizationCapabilityCatalog.EngagementsManage, OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.OwnerRole, seed.Permissions));
        Assert.Contains(OrganizationCapabilityCatalog.EngagementsManage, OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, seed.Permissions));
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.MemberRole, seed.Permissions));
    }

    [Fact]
    public void EngagementManageIsDelegableButIneffectiveUnderEarlierVersions()
    {
        Guid organizationId = Guid.CreateVersion7();
        var custom = OrganizationRole.Create(organizationId, "engagement-manager", "Engagement manager", "custom", null, Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        Assert.True(OrganizationCapabilityCatalog.IsDelegablePermission(OrganizationCapabilityCatalog.EngagementsManage));
        Assert.Contains(OrganizationCapabilityCatalog.EngagementsManage, OrganizationCapabilityCatalog.DelegablePermissionCodes);

        OrganizationRolePermission current = OrganizationRolePermission.Create(organizationId, custom.Id, OrganizationCapabilityCatalog.EngagementsManage);
        Assert.Contains(OrganizationCapabilityCatalog.EngagementsManage, OrganizationCapabilityCatalog.GetEffectiveRolePermissions(custom, [current]));

        // Grants pinned to versions that did not know the code never become effective by reinterpretation.
        foreach (int version in new[] { 1, 2, 4 })
        {
            OrganizationRolePermission pinned = WithPolicyVersion(OrganizationRolePermission.Create(organizationId, custom.Id, OrganizationCapabilityCatalog.EngagementsManage), version);
            Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(custom, [pinned]));
        }
    }

    [Fact]
    public void Version1And2GrantsRemainEffectiveAlongsideVersion3Grants()
    {
        Guid organizationId = Guid.CreateVersion7();
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, DateTimeOffset.UtcNow);
        OrganizationRolePermission versionOne = WithPolicyVersion(OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.MembersRead), 1);
        OrganizationRolePermission versionTwo = WithPolicyVersion(OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.ProjectsCreate), 2);
        OrganizationRolePermission versionThree = OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.EngagementsManage);

        Assert.True(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, [versionOne, versionTwo, versionThree])
            .SetEquals([OrganizationCapabilityCatalog.MembersRead, OrganizationCapabilityCatalog.ProjectsCreate, OrganizationCapabilityCatalog.EngagementsManage]));
    }

    private static OrganizationRolePermission WithPolicyVersion(OrganizationRolePermission row, int version)
    {
        typeof(OrganizationRolePermission).GetProperty(nameof(OrganizationRolePermission.CatalogPolicyVersion), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(row, version);
        return row;
    }
}
