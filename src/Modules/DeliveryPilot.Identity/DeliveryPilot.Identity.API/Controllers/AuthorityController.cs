using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationAuthority;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckSession;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

/// <summary>
/// Internal authority checks for other DeliveryPilot hosts (ADR-001 D-02/D-03). Guarded by the integration
/// API key and never published through the Gateway. A denial is a 200 decision, not an error.
/// </summary>
public sealed class AuthorityController(ISender sender) : IntegrationApiController(sender)
{
    [HttpPost("session-checks")]
    [ProducesResponseType<SessionCheckResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SessionCheckResult>> CheckSessionAsync(
        [FromBody] SessionCheckRequest request,
        CancellationToken cancellationToken)
    {
        Result<SessionCheckResult> result = await Sender.SendAsync(
            new CheckSessionQuery(request.UserId, request.SessionId),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpPost("organization-checks")]
    [ProducesResponseType<OrganizationAuthorityCheckResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(
        [FromBody] OrganizationAuthorityCheckRequest request,
        CancellationToken cancellationToken)
    {
        Result<OrganizationAuthorityCheckResult> result = await Sender.SendAsync(
            new CheckOrganizationAuthorityQuery(
                request.UserId,
                request.OrganizationId,
                request.SessionId,
                request.RequiredPermissionCode),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
