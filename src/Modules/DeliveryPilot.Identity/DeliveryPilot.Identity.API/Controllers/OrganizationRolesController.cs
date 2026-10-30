using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.Organizations;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Queries.ListOrganizationRoles;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.UpdateOrganizationRole;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/roles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationRolesController(ISender sender) : ApiController(sender)
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new ListOrganizationRolesQuery(organizationId, limit, cursor), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CreateAsync(Guid organizationId, CreateOrganizationRoleRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new CreateOrganizationRoleCommand(organizationId, request.Name, request.Description, request.PermissionCodes, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : HandleFailure(result);
    }

    [HttpPut("{roleId:guid}")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> UpdateAsync(Guid organizationId, Guid roleId, UpdateOrganizationRoleRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await Sender.SendAsync(new UpdateOrganizationRoleCommand(organizationId, roleId, request.Name, request.Description, request.PermissionCodes, request.ExpectedVersion, HttpContext.TraceIdentifier), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
