using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.Organizations;
using DeliveryPilot.Identity.Application.Features.Organizations.Commands.CreateOrganization;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationsController(ISender sender) : ApiController(sender)
{
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Guid attemptId = Guid.Empty;
        if (Request.Headers.TryGetValue("Idempotency-Key", out var keys) && keys.Count == 1 && Guid.TryParse(keys[0], out Guid parsedId))
        {
            attemptId = parsedId;
        }

        Result<CreateOrganizationResult> result = await Sender.SendAsync(
            new CreateOrganizationCommand(request.Name, attemptId, HttpContext.TraceIdentifier), cancellationToken);
        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return StatusCode(result.Value.IsReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created, result.Value.Snapshot);
    }
}
