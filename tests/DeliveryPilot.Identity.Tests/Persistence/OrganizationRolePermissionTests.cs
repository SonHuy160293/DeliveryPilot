using DeliveryPilot.Identity.Domain.Models;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class OrganizationRolePermissionTests
{
    [Fact]
    public void CreateRequiresScopeAndBoundedPermissionCode()
    {
        Guid organizationId = Guid.CreateVersion7();
        Guid roleId = Guid.CreateVersion7();

        OrganizationRolePermission permission = OrganizationRolePermission.Create(organizationId, roleId, " organization.read ");

        Assert.Equal(organizationId, permission.OrganizationId);
        Assert.Equal(roleId, permission.RoleId);
        Assert.Equal("organization.read", permission.PermissionCode);
        Assert.Throws<ArgumentException>(() => OrganizationRolePermission.Create(Guid.Empty, roleId, "organization.read"));
        Assert.Throws<ArgumentException>(() => OrganizationRolePermission.Create(organizationId, Guid.Empty, "organization.read"));
        Assert.Throws<ArgumentException>(() => OrganizationRolePermission.Create(organizationId, roleId, " "));
        Assert.Throws<ArgumentException>(() => OrganizationRolePermission.Create(organizationId, roleId, new string('x', 161)));
    }
}
