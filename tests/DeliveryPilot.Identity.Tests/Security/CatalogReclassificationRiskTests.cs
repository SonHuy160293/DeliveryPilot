using System.Reflection;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

[Collection("Catalog classification isolation")]
public sealed class CatalogReclassificationRiskTests
{
    [Fact]
    public void UnknownGrantPolicyVersionFailsClosed()
    {
        Guid organizationId = Guid.CreateVersion7();
        var seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, DateTimeOffset.UtcNow);
        OrganizationRolePermission row = OrganizationRolePermission.Create(organizationId, seed.OwnerRole.Id, OrganizationCapabilityCatalog.AuditRead);
        typeof(OrganizationRolePermission).GetProperty(nameof(OrganizationRolePermission.CatalogPolicyVersion))!.SetValue(row, 999);
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.OwnerRole, [row]));
    }

    [Fact]
    public void ExistingUnknownRowsRemainDeniedWhenCatalogRecognizesTheirCode()
    {
        const string code = "test.catalog.reclassification.unique";
        Guid organizationId = Guid.CreateVersion7();
        var builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, DateTimeOffset.UtcNow);
        var custom = OrganizationRole.Create(organizationId, "test-custom", "Custom", "custom", null, Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        OrganizationRolePermission[] rows =
        [
            OrganizationRolePermission.Create(organizationId, builtIns.OwnerRole.Id, code),
            OrganizationRolePermission.Create(organizationId, builtIns.AdminRole.Id, code),
            OrganizationRolePermission.Create(organizationId, custom.Id, code),
        ];
        var known = (HashSet<string>)typeof(OrganizationCapabilityCatalog).GetField("AllPermissionSet", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var delegable = (HashSet<string>)typeof(OrganizationCapabilityCatalog).GetField("DelegablePermissionSet", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(builtIns.OwnerRole, rows));
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(builtIns.AdminRole, rows));
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(custom, rows));
        try
        {
            Assert.True(known.Add(code));
            Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(builtIns.OwnerRole, rows));
            Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(builtIns.AdminRole, rows));
            Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(custom, rows));
            Assert.True(delegable.Add(code));
            Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(builtIns.AdminRole, rows));
            Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(custom, rows));
        }
        finally
        {
            known.Remove(code);
            delegable.Remove(code);
        }
    }
}

[CollectionDefinition("Catalog classification isolation", DisableParallelization = true)]
public sealed class CatalogClassificationIsolationCollection
{
}
