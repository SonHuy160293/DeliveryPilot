using System.Reflection;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

[Collection("Catalog classification isolation")]
public sealed class CatalogPolicyV2Tests
{
    private static readonly string[] DeliveryEntryCodes =
    [
        OrganizationCapabilityCatalog.EngagementsCreate,
        OrganizationCapabilityCatalog.EngagementsReadAll,
        OrganizationCapabilityCatalog.ProjectsCreate,
    ];

    [Fact]
    public void NewOrganizationsSeedDeliveryEntryCodesForOwnerAndAdminOnly()
    {
        Guid organizationId = Guid.CreateVersion7();
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, DateTimeOffset.UtcNow);

        Assert.All(seed.Roles, role => Assert.Equal(OrganizationCapabilityCatalog.CurrentTemplateVersion, role.TemplateVersion));
        Assert.All(seed.Permissions, row => Assert.Equal(OrganizationCapabilityCatalog.CurrentCatalogPolicyVersion, row.CatalogPolicyVersion));
        IReadOnlySet<string> owner = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.OwnerRole, seed.Permissions);
        IReadOnlySet<string> admin = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, seed.Permissions);
        IReadOnlySet<string> member = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.MemberRole, seed.Permissions);
        Assert.All(DeliveryEntryCodes, code => Assert.Contains(code, owner));
        Assert.All(DeliveryEntryCodes, code => Assert.Contains(code, admin));
        Assert.Contains(OrganizationCapabilityCatalog.OwnershipManage, owner);
        Assert.DoesNotContain(OrganizationCapabilityCatalog.OwnershipManage, admin);
        Assert.Empty(member);
    }

    [Fact]
    public void DeliveryEntryCodesAreDelegableToCustomRolesButIneffectiveUnderVersion1()
    {
        Guid organizationId = Guid.CreateVersion7();
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, DateTimeOffset.UtcNow);
        var custom = OrganizationRole.Create(organizationId, "delivery-lead", "Delivery lead", "custom", null, Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        Assert.All(DeliveryEntryCodes, code => Assert.True(OrganizationCapabilityCatalog.IsDelegablePermission(code)));

        OrganizationRolePermission current = OrganizationRolePermission.Create(organizationId, custom.Id, OrganizationCapabilityCatalog.ProjectsCreate);
        Assert.Contains(OrganizationCapabilityCatalog.ProjectsCreate, OrganizationCapabilityCatalog.GetEffectiveRolePermissions(custom, [current]));

        // A version 1 grant of a code introduced in version 2 never becomes effective by reinterpretation.
        OrganizationRolePermission versionOneCustom = WithPolicyVersion(OrganizationRolePermission.Create(organizationId, custom.Id, OrganizationCapabilityCatalog.ProjectsCreate), 1);
        OrganizationRolePermission versionOneAdmin = WithPolicyVersion(OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.EngagementsCreate), 1);
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(custom, [versionOneCustom]));
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, [versionOneAdmin]));
    }

    [Fact]
    public void Version1GrantsRemainEffectiveAlongsideVersion2Grants()
    {
        Guid organizationId = Guid.CreateVersion7();
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, DateTimeOffset.UtcNow);
        OrganizationRolePermission legacy = WithPolicyVersion(OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.MembersRead), 1);
        OrganizationRolePermission upgraded = OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.EngagementsReadAll);

        Assert.True(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, [legacy, upgraded])
            .SetEquals([OrganizationCapabilityCatalog.MembersRead, OrganizationCapabilityCatalog.EngagementsReadAll]));
        Assert.True(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, [WithPolicyVersion(upgraded, 2)])
            .SetEquals([OrganizationCapabilityCatalog.EngagementsReadAll]));
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, [WithPolicyVersion(upgraded, 4)]));
    }

    private static OrganizationRolePermission WithPolicyVersion(OrganizationRolePermission row, int version)
    {
        typeof(OrganizationRolePermission).GetProperty(nameof(OrganizationRolePermission.CatalogPolicyVersion), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(row, version);
        return row;
    }
}
