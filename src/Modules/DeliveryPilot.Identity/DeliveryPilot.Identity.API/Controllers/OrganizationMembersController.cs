using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyMembership;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetOrganizationMember;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.ListOrganizationMembers;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.ChangeRoleAssignment;
using DeliveryPilot.Identity.API.Contracts.Organizations;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/members")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationMembersController(ISender sender) : ApiController(sender)
{
    [HttpPost("{membershipId:guid}/roles/{roleId:guid}/grant")]
    public Task<IActionResult> GrantAsync(Guid organizationId, Guid membershipId, Guid roleId, ChangeRoleAssignmentRequest request, CancellationToken cancellationToken)
        => ChangeRoleAsync(organizationId, membershipId, roleId, request, true, cancellationToken);

    [HttpPost("{membershipId:guid}/roles/{roleId:guid}/revoke")]
    public Task<IActionResult> RevokeAsync(Guid organizationId, Guid membershipId, Guid roleId, ChangeRoleAssignmentRequest request, CancellationToken cancellationToken)
        => ChangeRoleAsync(organizationId, membershipId, roleId, request, false, cancellationToken);

    [HttpGet("me")]
    public async Task<IActionResult> GetMineAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new GetMyMembershipQuery(organizationId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, [FromQuery] string status = "active", [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new ListOrganizationMembersQuery(organizationId, status, limit, cursor), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpGet("{membershipId:guid}")]
    public async Task<IActionResult> GetAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new GetOrganizationMemberQuery(organizationId, membershipId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    private async Task<IActionResult> ChangeRoleAsync(Guid organizationId, Guid membershipId, Guid roleId, ChangeRoleAssignmentRequest request, bool grant, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new ChangeRoleAssignmentCommand(organizationId, membershipId, roleId, request.ExpectedMembershipVersion, request.Reason, grant, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
