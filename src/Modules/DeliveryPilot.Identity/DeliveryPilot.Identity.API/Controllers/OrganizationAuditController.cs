using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.OrganizationAudit;
using DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/organizations/{organizationId:guid}/audit-events")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationAuditController(ISender sender) : ApiController(sender)
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid organizationId, [FromQuery] ListOrganizationAuditEventsRequest request, CancellationToken cancellationToken)
    {
        // MVC converts empty query values to null; retain supplied empties for validation.
        string? Supplied(string name, string? fallback) => Request.Query.TryGetValue(name, out var values) ? values.ToString() : fallback;
        var query = new ListOrganizationAuditEventsQuery(
            organizationId,
            Supplied("actorUserId", request.ActorUserId),
            Supplied("membershipId", request.MembershipId),
            Supplied("targetId", request.TargetId),
            Supplied("action", request.Action),
            Supplied("from", request.From),
            Supplied("to", request.To),
            request.Limit,
            Supplied("cursor", request.Cursor));
        var result = await Sender.SendAsync(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
