using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListMyOwnershipNominations;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/me/ownership-nominations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyOwnershipNominationsController(ISender sender) : ApiController(sender)
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string status = "pending", [FromQuery] int limit = 50, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        string suppliedStatus = Request.Query.TryGetValue("status", out var statuses) ? statuses.ToString() : status;
        string? suppliedCursor = Request.Query.TryGetValue("cursor", out var cursors) ? cursors.ToString() : cursor;
        var result = await Sender.SendAsync(new ListMyOwnershipNominationsQuery(suppliedStatus, limit, suppliedCursor), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
