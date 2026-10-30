using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.Identity.API.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationReadRoutesTests
{
    [Fact]
    public void MemberControllersExposeReadAndRoleAssignmentRoutes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfiguredApiVersioning();
        services.AddControllers().AddApplicationPart(typeof(OrganizationMembersController).Assembly);
        using ServiceProvider provider = services.BuildServiceProvider();
        var routes = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .Where(x => x.DisplayName?.Contains("OrganizationMembersController", StringComparison.Ordinal) == true
                || x.DisplayName?.Contains("MeOrganizationsController", StringComparison.Ordinal) == true)
            .Select(x => x.AttributeRouteInfo?.Template)
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(6, routes.Length);
        Assert.Contains("api/v{version:apiVersion}/me/organizations", routes);
        Assert.Contains("api/v{version:apiVersion}/organizations/{organizationId:guid}/members", routes);
        Assert.Contains("api/v{version:apiVersion}/organizations/{organizationId:guid}/members/me", routes);
        Assert.Contains("api/v{version:apiVersion}/organizations/{organizationId:guid}/members/{membershipId:guid}", routes);
        Assert.Contains("api/v{version:apiVersion}/organizations/{organizationId:guid}/members/{membershipId:guid}/roles/{roleId:guid}/grant", routes);
        Assert.Contains("api/v{version:apiVersion}/organizations/{organizationId:guid}/members/{membershipId:guid}/roles/{roleId:guid}/revoke", routes);
    }
}
